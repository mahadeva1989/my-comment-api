using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using my_comment_api.Options;

namespace my_comment_api.Services;

public record ModerationResult(bool IsApproved, string Reason);

public class ModerationService(HttpClient httpClient, IOptions<ClaudeSettings> options)
{
    private readonly ClaudeSettings _settings = options.Value;
    private readonly string _apiKey = options.Value.ApiKey is { Length: > 0 } key
        ? key
        : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
          ?? throw new InvalidOperationException("Claude API key not configured");

    public async Task<ModerationResult> ModerateAsync(string content, CancellationToken cancellationToken = default)
    {
        object messageContent;

        if (!string.IsNullOrEmpty(_settings.ModerationPolicyFileId))
        {
            // Reference the uploaded policy file instead of inline rules
            messageContent = new object[]
            {
            new
            {
                type = "document",
                source = new
                {
                    type = "file_id",
                    file_id = _settings.ModerationPolicyFileId
                }
            },
            new
            {
                type = "text",
                text = $"Using the policy above, moderate this comment and respond ONLY with JSON:\n\n{content}"
            }
            };
        }
        else
        {
            // Fallback to inline prompt if no file uploaded yet
            messageContent = $$"""
            Moderate this comment and respond ONLY with JSON:
            {"approved": true, "reason": "reason"} or {"approved": false, "reason": "reason"}

            Comment: {content}
            """;
        }

        var requestBody = new
        {
            model = _settings.Model,
            max_tokens = 100,
            messages = new[]
            {
            new { role = "user", content = messageContent }
        }
        };


        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await httpClient.PostAsync(
            "https://api.anthropic.com/v1/messages", httpContent, cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Claude API error: {raw}");

        // Parse Claude's response
        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        // Parse the moderation JSON Claude returned
        using var modDoc = JsonDocument.Parse(text);
        var approved = modDoc.RootElement.GetProperty("approved").GetBoolean();
        var reason = modDoc.RootElement.GetProperty("reason").GetString() ?? "No reason provided";

        return new ModerationResult(approved, reason);
    }

    public async Task<List<BatchModerationResult>> ModerateAllAsync(
    List<(int Id, string? Content)> comments,
    CancellationToken cancellationToken = default)
    {
        // Build batch requests — one per comment
        var requests = comments.Select(c => new
        {
            custom_id = c.Id.ToString(),
            @params = new
            {
                model = _settings.Model,
                max_tokens = 100,
                messages = new[]
        {
            new
            {
                role = "user",
                content = !string.IsNullOrEmpty(_settings.ModerationPolicyFileId)
                    ? new object[]
                    {
                        new
                        {
                            type = "document",
                            source = new
                            {
                                type = "file_id",
                                file_id = _settings.ModerationPolicyFileId
                            }
                        },
                        new
                        {
                            type = "text",
                            text = $"Using the policy above, moderate this comment and respond ONLY with JSON, no markdown:\n\n{c.Content}"
                        }
                    }
                    : new object[]
                    {
                        new
                        {
                            type = "text",
                            text = $$"""
                                Moderate this comment and respond ONLY with JSON, no markdown:
                                {"approved": true, "reason": "reason"} or {"approved": false, "reason": "reason"}

                                Comment: {c.Content}
                                """
                        }
                    }
            }
        }
            }
        }).ToList();

        var batchBody = new { requests };
        var json = JsonSerializer.Serialize(batchBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        httpClient.DefaultRequestHeaders.Add("anthropic-beta", "message-batches-2024-09-24");

        // Submit batch
        var batchResponse = await httpClient.PostAsync("https://api.anthropic.com/v1/messages/batches", httpContent, cancellationToken);
        var batchRaw = await batchResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!batchResponse.IsSuccessStatusCode)
            throw new Exception($"Batch API error: {batchRaw}");

        using var batchDoc = JsonDocument.Parse(batchRaw);
        var batchId = batchDoc.RootElement.GetProperty("id").GetString()!;

        // Poll until batch is complete
        string status = "in_progress";
        while (status == "in_progress")
        {
            await Task.Delay(2000, cancellationToken);
            var pollResponse = await httpClient.GetAsync($"https://api.anthropic.com/v1/messages/batches/{batchId}", cancellationToken);
            var pollRaw = await pollResponse.Content.ReadAsStringAsync(cancellationToken);
            using var pollDoc = JsonDocument.Parse(pollRaw);
            status = pollDoc.RootElement.GetProperty("processing_status").GetString()!;
        }

        // Fetch results
        var resultsResponse = await httpClient.GetAsync($"https://api.anthropic.com/v1/messages/batches/{batchId}/results", cancellationToken);
        var resultsRaw = await resultsResponse.Content.ReadAsStringAsync(cancellationToken);

        // Results come back as JSONL (one JSON object per line)
        var results = new List<BatchModerationResult>();
        foreach (var line in resultsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var lineDoc = JsonDocument.Parse(line);
            var commentId = int.Parse(lineDoc.RootElement.GetProperty("custom_id").GetString()!);
            var resultType = lineDoc.RootElement.GetProperty("result").GetProperty("type").GetString();

            if (resultType != "succeeded") continue;

            var text = lineDoc.RootElement
                .GetProperty("result")
                .GetProperty("message")
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            // Strip markdown backticks if present
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                text = text.Split('\n', 2)[1];
                text = text[..text.LastIndexOf("```")].Trim();
            }

            // Try to find JSON object anywhere in the response
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart == -1 || jsonEnd == -1)
            {
                // Claude didn't return JSON — treat as rejected with explanation
                results.Add(new BatchModerationResult(commentId, false, $"Could not parse: {text[..Math.Min(50, text.Length)]}"));
                continue;
            }

            text = text[jsonStart..(jsonEnd + 1)];

            try
            {
                using var modDoc = JsonDocument.Parse(text);
                var approved = modDoc.RootElement.GetProperty("approved").GetBoolean();
                var reason = modDoc.RootElement.GetProperty("reason").GetString() ?? "";
                results.Add(new BatchModerationResult(commentId, approved, reason));
            }
            catch
            {
                results.Add(new BatchModerationResult(commentId, false, $"Parse error for response: {text[..Math.Min(50, text.Length)]}"));
            }
        }

        return results;
    }

    public async Task<string> UploadPolicyFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var uploadClient = new HttpClient();
        uploadClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        uploadClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        uploadClient.DefaultRequestHeaders.Add("anthropic-beta", "files-api-2025-04-14");

        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);

        // PDF media type
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        multipart.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await uploadClient.PostAsync(
            "https://api.anthropic.com/v1/files", multipart, cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"File upload error: {raw}");

        using var doc = JsonDocument.Parse(raw);
        var fileId = doc.RootElement.GetProperty("id").GetString()!;

        Console.WriteLine($"Uploaded! file_id: {fileId}");
        return fileId;
    }
    public record BatchModerationResult(int CommentId, bool Approved, string Reason);
}
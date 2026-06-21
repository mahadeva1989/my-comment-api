using Microsoft.Extensions.Primitives;
namespace my_comment_api.Options;

public class ClaudeSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-4-6";

    public string ModerationPolicyFileId { get; set; } = string.Empty;
}
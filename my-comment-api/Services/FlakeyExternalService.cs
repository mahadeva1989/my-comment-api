namespace my_comment_api.Services;

public class FlakeyExternalService(ILogger<FlakeyExternalService> logger)
{
    private int _attempt = 0;

    public async Task<string> FetchDataAsync(CancellationToken ct = default)
    {
        _attempt++;
        logger.LogInformation("Attempt #{Attempt}", _attempt);

        await Task.Delay(50, ct);

        if (_attempt % 4 != 0)
            throw new HttpRequestException($"Simulated failure on attemp #{_attempt}");

        logger.LogInformation("Succeeded on Attempt #{Attempt}", _attempt);
        _attempt = 0;
        return "Data fetched";
    }

    public async Task<string> AlwaysFailAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
        logger.LogWarning("AlwaysFailsAsync - throwing");
        throw new HttpRequestException("Service is down");

    }
}
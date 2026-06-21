using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using my_comment_api.Services;
using Polly;
using Polly.Registry;

namespace my_comment_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RetryDemoController(FlakeyExternalService flakeyExternalService, ResiliencePipelineProvider<string> resiliencePipeline) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Demo(CancellationToken ct)
    {
        var pipeline = resiliencePipeline.GetPipeline("flake-retry");
        var result = await pipeline.ExecuteAsync(async token => await flakeyExternalService.FetchDataAsync(ct));
        return Ok(result);
    }

    [HttpGet("circuit")]
    public async Task<IActionResult> Circuit(CancellationToken ct = default)
    {
        try
        {
            var pipeline = resiliencePipeline.GetPipeline("circuit-breaker");
            var result = await pipeline.ExecuteAsync(async token => await flakeyExternalService.AlwaysFailAsync(token), ct);
            return Ok(new { result });

        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            return StatusCode(503, new { state = "OPEN", message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new { state = "CLOSED", message = ex.Message });
        }

    }

}
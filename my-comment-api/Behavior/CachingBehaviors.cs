using MediatR;
using my_comment_api.CQRS;
using my_comment_api.Services;

namespace my_comment_api.Behavior;

public class CachingBehavior<TRequest, TResponse>(CacheService cache)
     : IPipelineBehavior<TRequest, TResponse>
     where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICacheable cacheable)
        {
            return await next();
        }

        var cached = await cache.GetAsync<TResponse>(cacheable.CacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var response = await next();

        await cache.SetAsync(cacheable.CacheKey, response, cancellationToken);
        return response;
    }
}
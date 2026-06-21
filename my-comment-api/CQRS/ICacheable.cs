namespace my_comment_api.CQRS;

public interface ICacheable
{
    string CacheKey { get; }
}
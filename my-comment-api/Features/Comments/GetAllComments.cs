using MediatR;
using Microsoft.EntityFrameworkCore;
using my_comment_api.Builders;
using my_comment_api.CQRS;
using my_comment_api.Data;
using my_comment_api.DTOs;

namespace my_comment_api.Features.Comments;

public record GetAllCommentsQuery() : IQuery<List<CommentResponse>>, ICacheable
{
    public string CacheKey => "comments:all";
}


public class GetAllCommentsHandler(AppDbContext context) : IQueryHandler<GetAllCommentsQuery, List<CommentResponse>>
{
    public async Task<List<CommentResponse>> Handle(GetAllCommentsQuery request, CancellationToken cancellationToken)
    {
        return await context.Comments.Select(c => new CommentResponseBuilder()
        .WithId(c.Id)
        .WithContent(c.Content)
        .WithAuthor(c.Author)
        .WithCreateAt(c.CreatedAt)
        .Build()
        ).ToListAsync(cancellationToken);
    }
}
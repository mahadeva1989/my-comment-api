using MediatR;
using Microsoft.AspNetCore.SignalR;
using my_comment_api.Builders;
using my_comment_api.CQRS;
using my_comment_api.Data;
using my_comment_api.DTOs;
using my_comment_api.Hubs;
using my_comment_api.Models;
using my_comment_api.Services;

namespace my_comment_api.Features.Comments;

public record CreateCommentCommand(int UserId, string Content, string? Author) : ICommand<CommentResponse>;

public class CreateCommentHandler(
    AppDbContext context,
    IHubContext<CommentHub> hubContext,
    ModerationService moderationService,
    CacheService cache) : ICommandHandler<CreateCommentCommand, CommentResponse>
{
    public async Task<CommentResponse> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    { 
        var moderation = await moderationService.ModerateAsync(request.Content, cancellationToken);
        if (!moderation.IsApproved)
        {
            throw new InvalidOperationException($"Comment rejected: {moderation.Reason}");
        }
        var comment = new Comment
        {
            UserId = request.UserId, 
            Content = request.Content,
            Author = request.Author,
            CreatedAt = DateTime.UtcNow
        };

        context.Comments.Add(comment);
        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync("comments:all", cancellationToken);
        var response = new CommentResponseBuilder()
        .WithId(comment.Id)
        .WithContent(comment.Content)
        .WithAuthor(comment.Author)
        .WithCreateAt(comment.CreatedAt).Build();

        await hubContext.Clients.All.SendAsync("NewComment", response, cancellationToken);

        return response;
    }
}
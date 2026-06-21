using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using my_comment_api.Models;

namespace my_comment_api.Authorization;

public class CommentOwnerHandler : AuthorizationHandler<CommentOwnerRequirement, Comment>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CommentOwnerRequirement requirement,
        Comment comment)
    {
        var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (comment.UserId == userId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

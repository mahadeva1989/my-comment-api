using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using my_comment_api.Authorization;
using my_comment_api.Data;
using my_comment_api.DTOs;
using my_comment_api.Features.Comments;
using my_comment_api.Services;

namespace my_comment_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommentController(AppDbContext _context, IMediator mediator, IAuthorizationService _authService, ModerationService _moderationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var comments = await mediator.Send(new GetAllCommentsQuery());
        return Ok(comments);
    }

    [Authorize(Roles = "Author")]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var comment = await _context.Comments.FindAsync(id)
        ?? throw new KeyNotFoundException($"Comment with {id} not found");

        return Ok(new CommentResponse
        {
            Id = comment.Id,
            Content = comment.Content,
            Author = comment.Author,
            CreatedAt = comment.CreatedAt
        });
    }

    [Authorize(Policy = "CreateComment")]
    [HttpPost]
    public async Task<IActionResult> Create(CommentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userName = User.FindFirstValue(ClaimTypes.Name);

        var commentResponse = await mediator.Send(new CreateCommentCommand(userId, request.Content, userName));
        return CreatedAtAction(nameof(Get), new { id = commentResponse.Id }, commentResponse);
    }

    [Authorize(Policy = "CreateComment")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var comment = await _context.Comments.FindAsync(id)
        ?? throw new KeyNotFoundException($"Comment with Id {id} is not found");

        var result = await _authService.AuthorizeAsync(User, comment, new CommentOwnerRequirement());
        if (!result.Succeeded) return Forbid();


        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Author")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CommentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var comment = await _context.Comments.FindAsync(id)
        ?? throw new KeyNotFoundException($"comment id {id} is not found");

        if (comment.UserId != userId)
            throw new UnauthorizedAccessException("User is not authorized to update");

        comment.Content = request.Content;

        await _context.SaveChangesAsync();

        return NoContent();

    }

    [HttpGet("moderate-all")]
    public async Task<IActionResult> ModerateAll(CancellationToken cancellationToken)
    {
        // Fetch all comments from DB
        var comments = await _context.Comments
            .Select(c => new { c.Id, c.Content })
            .ToListAsync(cancellationToken);

        if (!comments.Any())
            return Ok(new { message = "No comments to moderate" });

        var input = comments.Select(c => (c.Id, c.Content)).ToList();
        var results = await _moderationService.ModerateAllAsync(input, cancellationToken);

        return Ok(new
        {
            total = results.Count,
            approved = results.Count(r => r.Approved),
            rejected = results.Count(r => !r.Approved),
            details = results
        });
    }

    [HttpPost("upload-policy")]
    public async Task<IActionResult> UploadPolicy([FromQuery] string filePath, CancellationToken cancellationToken)
    {
        var fileId = await _moderationService.UploadPolicyFileAsync(filePath, cancellationToken);
        return Ok(new { fileId });
    }

}
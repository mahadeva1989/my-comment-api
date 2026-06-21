using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using my_comment_api.Data;
using my_comment_api.DTOs;
using my_comment_api.Models;
using my_comment_api.Tests.Helpers;

namespace my_comment_api.Tests.Controller;

public class CommentControllerTest
{
    private static async Task<(AppDbContext, int)> SeedUserAndComment(string content = "Hello world")
    {
        var context = DbContextFactory.Create();

        var user = new User { Username = "john", Email = "john@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var comment = new Comment { Content = content, Author = "john", UserId = user.Id, CreatedAt = DateTime.UtcNow };
        context.Comments.Add(comment);
        await context.SaveChangesAsync();

        return (context, user.Id);
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = CommentControllerFactory.Create(context);

        // Act
        var result = await controller.GetAll();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var comments = ok.Value.Should().BeAssignableTo<IEnumerable<CommentResponse>>().Subject;
        comments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WithComments_ReturnsAllComments()
    {
        // Arrange
        var (context, userId) = await SeedUserAndComment("First comment");
        var context2 = context;
        var user2 = new User { Username = "jane", Email = "jane@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        context2.Users.Add(user2);
        await context2.SaveChangesAsync();
        context2.Comments.Add(new Comment { Content = "Second comment", Author = "jane", UserId = user2.Id, CreatedAt = DateTime.UtcNow });
        await context2.SaveChangesAsync();

        var controller = CommentControllerFactory.Create(context2);

        // Act
        var result = await controller.GetAll();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var comments = ok.Value.Should().BeAssignableTo<IEnumerable<CommentResponse>>().Subject;
        comments.Should().HaveCount(2);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_WithValidId_ReturnsComment()
    {
        // Arrange
        var (context, _) = await SeedUserAndComment("Test content");
        var controller = CommentControllerFactory.Create(context);
        var commentId = context.Comments.First().Id;

        // Act
        var result = await controller.Get(commentId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CommentResponse>().Subject;
        response.Content.Should().Be("Test content");
        response.Author.Should().Be("john");
    }

    [Fact]
    public async Task Get_WithInvalidId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = CommentControllerFactory.Create(context);

        // Act & Assert
        await FluentActions.Invoking(() => controller.Get(999))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var user = new User { Username = "john", Email = "john@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CommentControllerFactory.Create(context, userId: user.Id, username: "john");
        var request = new CommentRequest { Content = "My new comment" };

        // Act
        var result = await controller.Create(request);

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<CommentResponse>().Subject;
        response.Content.Should().Be("My new comment");
        response.Author.Should().Be("john");
    }

    [Fact]
    public async Task Create_SavesCommentWithCorrectUserId()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var user = new User { Username = "john", Email = "john@example.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CommentControllerFactory.Create(context, userId: user.Id, username: "john");

        // Act
        await controller.Create(new CommentRequest { Content = "Test" });

        // Assert
        var saved = context.Comments.First();
        saved.UserId.Should().Be(user.Id);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_OwnComment_ReturnsNoContent()
    {
        // Arrange
        var (context, userId) = await SeedUserAndComment();
        var controller = CommentControllerFactory.Create(context, userId: userId);
        var commentId = context.Comments.First().Id;

        // Act
        var result = await controller.Delete(commentId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        context.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_AnotherUsersComment_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var (context, _) = await SeedUserAndComment();
        var commentId = context.Comments.First().Id;
        var controller = CommentControllerFactory.Create(context, userId: 999); // different user

        // Act & Assert
        await FluentActions.Invoking(() => controller.Delete(commentId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_NonExistentComment_ThrowsKeyNotFoundException()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = CommentControllerFactory.Create(context);

        // Act & Assert
        await FluentActions.Invoking(() => controller.Delete(999))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_OwnComment_ReturnsNoContentAndUpdatesContent()
    {
        // Arrange
        var (context, userId) = await SeedUserAndComment("Original content");
        var controller = CommentControllerFactory.Create(context, userId: userId);
        var commentId = context.Comments.First().Id;

        // Act
        var result = await controller.Update(commentId, new CommentRequest { Content = "Updated content" });

        // Assert
        result.Should().BeOfType<NoContentResult>();
        context.Comments.First().Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task Update_AnotherUsersComment_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var (context, _) = await SeedUserAndComment();
        var commentId = context.Comments.First().Id;
        var controller = CommentControllerFactory.Create(context, userId: 999); // different user

        // Act & Assert
        await FluentActions.Invoking(() => controller.Update(commentId, new CommentRequest { Content = "Hack" }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Update_NonExistentComment_ThrowsKeyNotFoundException()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = CommentControllerFactory.Create(context);

        // Act & Assert
        await FluentActions.Invoking(() => controller.Update(999, new CommentRequest { Content = "Test" }))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}

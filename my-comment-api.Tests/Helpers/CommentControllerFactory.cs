using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using my_comment_api.Controllers;
using my_comment_api.Data;
using my_comment_api.Services;

namespace my_comment_api.Tests.Helpers;

public static class CommentControllerFactory
{
    public static CommentController Create(AppDbContext context, int userId = 1, string username = "john")
    {
        var mediator = new Mock<IMediator>().Object;
        var authService = new Mock<IAuthorizationService>().Object;
        var controller = new CommentController(context, mediator, authService, null!);

        // Simulate an authenticated user by injecting claims into HttpContext
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }
}

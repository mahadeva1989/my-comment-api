using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace my_comment_api.Tests.Controller;

public class AuthControllerTest
{
    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_ReturnsOkWithToken()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        var request = new RegisterRequest { Username = "john", Email = "john@example.com", Password = "Secret1" };

        // Act
        var result = await controller.Register(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().NotBeNullOrEmpty();
        response.Username.Should().Be("john");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsArgumentException()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        var request = new RegisterRequest { Username = "john", Email = "john@example.com", Password = "Secret1" };
        await controller.Register(request);

        // Act & Assert
        await FluentActions.Invoking(() => controller.Register(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Email is already in use");
    }

    [Fact]
    public async Task Register_StoresUsernameAsLowercase()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        var request = new RegisterRequest { Username = "JOHN", Email = "JOHN@Example.com", Password = "Secret1" };

        // Act
        await controller.Register(request);

        // Assert
        var user = context.Users.First();
        user.Username.Should().Be("john");
        user.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task Register_StoresPasswordAsHash_NotPlainText()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        var request = new RegisterRequest { Username = "john", Email = "john@example.com", Password = "Secret1" };

        // Act
        await controller.Register(request);

        // Assert
        var user = context.Users.First();
        user.PasswordHash.Should().NotBe("Secret1");
        user.PasswordHash.Should().StartWith("$2");
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        await controller.Register(new RegisterRequest { Username = "john", Email = "john@example.com", Password = "Secret1" });
        var request = new LoginRequest { Username = "john", Password = "Secret1" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsKeyNotFoundException()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        await controller.Register(new RegisterRequest { Username = "john", Email = "john@example.com", Password = "Secret1" });
        var request = new LoginRequest { Username = "john", Password = "WrongPassword" };

        // Act & Assert
        await FluentActions.Invoking(() => controller.Login(request))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invalid email or password");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ThrowsKeyNotFoundException()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        var request = new LoginRequest { Username = "nobody", Password = "Secret1" };

        // Act & Assert
        await FluentActions.Invoking(() => controller.Login(request))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Invalid email or password");
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveForUsername()
    {
        // Arrange
        var context = DbContextFactory.Create();
        var controller = AuthControllerFactory.Create(context);
        await controller.Register(new RegisterRequest { Username = "John", Email = "john@example.com", Password = "Secret1" });
        var request = new LoginRequest { Username = "JOHN", Password = "Secret1" };

        // Act
        var result = await controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using my_comment_api.Data;
using my_comment_api.Models;
using my_comment_api.Services;


[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(AppDbContext database, TokenService tokenService) : ControllerBase
{

    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await database.Users.AnyAsync(u => u.Email == request.Email))
            throw new ArgumentException("Email is already in use");

        var user = new User
        {
            Username = request.Username.ToLowerInvariant(),
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        database.Users.Add(user);
        await database.SaveChangesAsync();

        var token = tokenService.GenerateToken(user);

        AppendTokenCookie(token);

        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await database.Users.FirstOrDefaultAsync(u => u.Username == request.Username.ToLower())
            ?? throw new KeyNotFoundException("Invalid email or password");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new KeyNotFoundException("Invalid email or password");

        var token = tokenService.GenerateToken(user);

        AppendTokenCookie(token);
        Response.Cookies.Append("access-token", token,
         new CookieOptions
         {
             HttpOnly = true,
             Secure = true,
             SameSite = SameSiteMode.Strict,
             Expires = DateTimeOffset.UtcNow
             .AddMinutes(15)
         });

        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username!
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("access_token");
        return NoContent();
    }

    private void AppendTokenCookie(string token) =>
        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,            // not readable by JS — blocks XSS token theft
            Secure = true,              // only sent over HTTPS
            SameSite = SameSiteMode.Strict, // blocks CSRF from cross-origin requests
            Expires = DateTimeOffset.UtcNow.AddMinutes(60)
        });
}

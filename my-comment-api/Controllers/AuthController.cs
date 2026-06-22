using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using my_comment_api.Data;
using my_comment_api.Services;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Registers a new user with the provided credentials and returns an authentication token.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email.ToLower());
        if (emailExists)
            throw new ArgumentException("Email is already in use");

        var user = new my_comment_api.Models.User
        {
            Username = request.Username.ToLower(),
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponse { Token = token, Username = user.Username });
    }

    /// <summary>
    /// Authenticates an existing user by username and password and returns an authentication token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username.ToLower());
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new KeyNotFoundException("Invalid email or password");

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponse { Token = token, Username = user.Username });
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using my_comment_api.Models;
using my_comment_api.Options;

namespace my_comment_api.Services;

public class TokenService(IOptions<JwtSettings> config)
{

    public string GenerateToken(User user)
    {
        // var jwtSettings = config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Value.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()!),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.Username!),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: config.Value.Issuer,
            audience: config.Value.Audience,
            signingCredentials: credentials,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(config.Value.ExpiryInMinutes)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);

    }
}
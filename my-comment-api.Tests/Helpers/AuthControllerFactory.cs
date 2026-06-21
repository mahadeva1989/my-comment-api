using Microsoft.Extensions.Options;
using my_comment_api.Data;
using my_comment_api.Options;
using my_comment_api.Services;

public static class AuthControllerFactory
{
    public static AuthController Create(AppDbContext context)
    {
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = "test-super-secret-key-32-characters!!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryInMinutes = 60
        });

        var tokenService = new TokenService(jwtSettings);
        return new AuthController(context, tokenService);
    }
}
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;

public class MinimumAgeRequirement : IAuthorizationRequirement
{
    private readonly int Years;
    public MinimumAgeRequirement(int _years)
    {
        Years = _years;
    }

}

public class MinimumAgeRequirementHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MinimumAgeRequirement requirement)
    {
        var ageVerified = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (ageVerified == "true")
        {
            context.Succeed(requirement);
        }
    }
}
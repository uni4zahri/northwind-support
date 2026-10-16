using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Presentation.Auth;
namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAgentCredentials credentials, JwtSettings settings, IClock clock) : ControllerBase
{
    public sealed record LoginRequest(string? Email, string? Password);
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            return BadRequest(new ProblemDetails { Status = 400, Detail = "Email and password are required (maximum 256 and 1024 characters)." });
        var agent = await credentials.AuthenticateAsync(request.Email, request.Password, ct);
        if (agent is null) return Unauthorized(new ProblemDetails { Status = 401, Detail = "Invalid email or password." });
        var now = clock.UtcNow;
        var expiry = now.AddMinutes(settings.LifetimeMinutes);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), new Claim("name", agent.FullName)],
            now, expiry, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc = expiry, user = agent });
    }
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var id)) return Unauthorized();
        var agent = await credentials.GetActiveAsync(id, ct);
        return agent is null ? Unauthorized() : Ok(agent);
    }
}

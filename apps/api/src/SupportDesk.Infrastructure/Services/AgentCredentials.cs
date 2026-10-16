using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
namespace SupportDesk.Infrastructure.Services;

public sealed class AgentCredentials(SupportDbContext db, IPasswordHasher<Agent> hasher) : IAgentCredentials
{
    public async Task<SignedInAgent?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToUpperInvariant();
        var agent = await db.Set<Agent>().SingleOrDefaultAsync(a => a.Email.ToUpper() == normalized, ct);
        if (agent?.PasswordHash is null || !agent.IsActive) return null;
        var verification = hasher.VerifyHashedPassword(agent, agent.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed) return null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            agent.SetPasswordHash(hasher.HashPassword(agent, password));
            await db.SaveChangesAsync(ct);
        }
        return new SignedInAgent(agent.Id, agent.FullName, agent.Email);
    }
    public Task<SignedInAgent?> GetActiveAsync(int id, CancellationToken ct) => db.Set<Agent>().AsNoTracking()
        .Where(a => a.Id == id && a.IsActive).Select(a => new SignedInAgent(a.Id, a.FullName, a.Email)).SingleOrDefaultAsync(ct);
}

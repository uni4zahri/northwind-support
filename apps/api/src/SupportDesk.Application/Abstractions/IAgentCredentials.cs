namespace SupportDesk.Application.Abstractions;
public sealed record SignedInAgent(int Id, string FullName, string Email);
public interface IAgentCredentials
{
    Task<SignedInAgent?> AuthenticateAsync(string email, string password, CancellationToken ct);
    Task<SignedInAgent?> GetActiveAsync(int id, CancellationToken ct);
}

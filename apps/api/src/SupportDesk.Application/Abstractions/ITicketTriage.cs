using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
namespace SupportDesk.Application.Abstractions;

public interface ITicketTriage
{
    Task<TriageResult> ApplyAsync(Ticket ticket, bool keepOwner, CancellationToken ct);
    Task<EscalationResult> EscalateAsync(int id, string reason, string actor, CancellationToken ct);
    Task<IReadOnlyList<TicketEscalation>> HistoryAsync(int id, CancellationToken ct);
}
public sealed record TriageResult(string PriorityReason, string SlaWindowReason, string AssignmentReason);
public sealed record EscalationResult(TicketDetailDto Ticket, TicketEscalation Escalation);

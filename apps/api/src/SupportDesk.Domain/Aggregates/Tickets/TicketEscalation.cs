namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed class TicketEscalation
{
    private TicketEscalation() { }
    public TicketEscalation(Ticket ticket, TicketPriority fromPriority, int? fromAgentId, DateTime? fromDueAtUtc, string reason, string actor, DateTime now)
    {
        TicketId = ticket.Id; FromPriority = fromPriority; ToPriority = ticket.Priority;
        FromAgentId = fromAgentId; ToAgentId = ticket.AssignedAgentId;
        FromDueAtUtc = fromDueAtUtc; ToDueAtUtc = ticket.DueAtUtc;
        Reason = reason; EscalatedBy = actor; EscalatedAtUtc = now;
    }
    public int Id { get; private set; }
    public int TicketId { get; private set; }
    public TicketPriority FromPriority { get; private set; }
    public TicketPriority ToPriority { get; private set; }
    public int? FromAgentId { get; private set; }
    public int? ToAgentId { get; private set; }
    public DateTime? FromDueAtUtc { get; private set; }
    public DateTime? ToDueAtUtc { get; private set; }
    public string Reason { get; private set; } = "";
    public string EscalatedBy { get; private set; } = "";
    public DateTime EscalatedAtUtc { get; private set; }
}

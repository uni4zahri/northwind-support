using SupportDesk.Domain.Aggregates.Customers;
namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed class SlaSettings
{
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = new()
    { [TicketPriority.Low] = 72, [TicketPriority.Medium] = 24, [TicketPriority.High] = 8, [TicketPriority.Critical] = 4 };
    public double PremiumCustomerMultiplier { get; set; } = 0.5;
    public double MinimumWindowHours { get; set; } = 1;
    public double AtRiskThresholdPercent { get; set; } = 25;
}

public sealed record AgentCandidate(int Id, bool Active, int OpenTickets, int Limit, bool Specialized);

/// <summary>Pure rules shared by creation and escalation; no database or wall clock.</summary>
public sealed class TriagePolicy(SlaSettings settings)
{
    public TicketPriority Priority(TicketPriority requested, bool forceCritical) => forceCritical ? TicketPriority.Critical : requested;
    public TimeSpan Window(TicketPriority priority, CustomerTier tier) => TimeSpan.FromHours(
        Math.Max(settings.MinimumWindowHours, settings.BaseWindowHours[priority] * (tier == CustomerTier.Premium ? settings.PremiumCustomerMultiplier : 1)));
    public int? SelectAgent(IEnumerable<AgentCandidate> candidates, bool specialistRequired, int? currentAgentId = null)
    {
        var eligible = candidates.Where(a => a.Active && a.OpenTickets < a.Limit && (!specialistRequired || a.Specialized)).ToList();
        if (currentAgentId is not null && eligible.Any(a => a.Id == currentAgentId)) return currentAgentId;
        return eligible.OrderBy(a => a.OpenTickets).ThenBy(a => a.Id).Select(a => (int?)a.Id).FirstOrDefault();
    }
}

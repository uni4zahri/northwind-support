using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;
namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TriagePolicyTests
{
    [Theory]
    [InlineData(TicketPriority.Low, 72)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Critical, 4)]
    public void WindowsRespectPriorityAndTier(TicketPriority priority, double hours)
    {
        var policy = new TriagePolicy(new SlaSettings());
        Assert.Equal(hours, policy.Window(priority, CustomerTier.Standard).TotalHours);
        Assert.Equal(hours / 2, policy.Window(priority, CustomerTier.Premium).TotalHours);
        Assert.Equal(TicketPriority.Critical, policy.Priority(priority, true));
        Assert.Equal(priority, policy.Priority(priority, false));
    }
    [Fact]
    public void FloorAndAgentEligibilityAreApplied()
    {
        var policy = new TriagePolicy(new SlaSettings { PremiumCustomerMultiplier = 0.01 });
        Assert.Equal(1, policy.Window(TicketPriority.Critical, CustomerTier.Premium).TotalHours);
        AgentCandidate[] agents = [new(1, false, 0, 5, true), new(2, true, 5, 5, true), new(3, true, 0, 5, false), new(5, true, 1, 5, true), new(4, true, 1, 5, true)];
        Assert.Equal(4, policy.SelectAgent(agents, true));
        Assert.Equal(3, policy.SelectAgent(agents, false));
        Assert.Equal(5, policy.SelectAgent(agents, true, 5));
        Assert.Equal(4, policy.SelectAgent(agents, true, 2));
        Assert.Null(policy.SelectAgent(agents.Take(3), true));
    }
    [Fact]
    public void ExactSlaBoundariesAndResolution()
    {
        var start = FixedClock.DefaultNow;
        var due = start.AddHours(4);
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, start, start));
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(due, null, start.AddHours(3).AddTicks(-1), start));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, start.AddHours(3), start));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, due, start));
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(due, null, due.AddTicks(1), start));
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(due, due, due.AddDays(1), start));
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(due, due.AddTicks(1), due, start));
    }
    [Theory]
    [InlineData(TicketPriority.Low, TicketStatus.New, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketStatus.Open, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketStatus.InProgress, TicketPriority.Critical)]
    public void EscalationRaisesExactlyOneLevel(TicketPriority before, TicketStatus status, TicketPriority after)
    {
        var ticket = new TicketBuilder().WithPriority(before).WithStatus(status).Build();
        ticket.EscalatePriority();
        Assert.Equal(after, ticket.Priority);
    }
    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New)]
    [InlineData(TicketPriority.Low, TicketStatus.Resolved)]
    [InlineData(TicketPriority.Low, TicketStatus.Closed)]
    public void EscalationRejectsInvalidState(TicketPriority priority, TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(status).Build();
        Assert.Throws<BusinessRuleViolationException>(ticket.EscalatePriority);
        Assert.Equal(priority, ticket.Priority);
    }
}

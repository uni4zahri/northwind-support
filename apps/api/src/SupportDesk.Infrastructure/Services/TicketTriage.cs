using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
namespace SupportDesk.Infrastructure.Services;

public sealed class TicketTriage(SupportDbContext db, IClock clock, TriagePolicy policy, ITicketQueries queries) : ITicketTriage
{
    public async Task<TriageResult> ApplyAsync(Ticket ticket, bool keepOwner, CancellationToken ct)
    {
        var category = await db.Set<Category>().AsNoTracking().SingleAsync(c => c.Id == ticket.CategoryId, ct);
        var customer = await db.Set<Customer>().AsNoTracking().SingleAsync(c => c.Id == ticket.CustomerId, ct);
        var candidates = await db.Set<Agent>().AsNoTracking().Select(a => new AgentCandidate(a.Id, a.IsActive,
            db.Set<Ticket>().Count(t => t.Id != ticket.Id && t.AssignedAgentId == a.Id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed),
            a.MaxOpenTickets, a.Specializations.Any(s => s.CategoryId == ticket.CategoryId))).ToListAsync(ct);
        var priority = keepOwner ? ticket.Priority : policy.Priority(ticket.Priority, category.ForcesCriticalPriority);
        var window = policy.Window(priority, customer.Tier);
        var agent = policy.SelectAgent(candidates, category.RequiresSpecialist, keepOwner ? ticket.AssignedAgentId : null);
        ticket.ApplyTriage(priority, agent, clock.UtcNow.Add(window), clock.UtcNow);
        return new TriageResult(keepOwner ? "Priority raised by one level." : category.ForcesCriticalPriority ? "Category requires Critical priority." : "Requested priority (default Medium) applied.",
            $"{window.TotalHours:g} hours for {priority} / {customer.Tier}.",
            agent is null ? "Unassigned because no active qualified agent has capacity." : $"Agent {agent}: eligible owner retained or fewest open tickets; ties use agent ID.");
    }
    public async Task<EscalationResult> EscalateAsync(int id, string reason, string actor, CancellationToken ct)
    {
        if (reason.Trim().Length is not (>= 5 and <= 500) || string.IsNullOrWhiteSpace(actor) || actor.Length > 100)
            throw new ValidationException(new Dictionary<string, string[]> { ["reason"] = ["Reason must be 5–500 characters and actor 1–100 characters."] });
        var ticket = await db.Set<Ticket>().SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Ticket", id);
        var priority = ticket.Priority; var owner = ticket.AssignedAgentId; var due = ticket.DueAtUtc;
        ticket.EscalatePriority();
        await ApplyAsync(ticket, true, ct);
        var history = new TicketEscalation(ticket, priority, owner, due, reason.Trim(), actor, clock.UtcNow);
        db.Add(history);
        await db.SaveChangesAsync(ct);
        return new EscalationResult((await queries.GetDetailAsync(id, ct))!, history);
    }
    public async Task<IReadOnlyList<TicketEscalation>> HistoryAsync(int id, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AnyAsync(t => t.Id == id, ct)) throw new NotFoundException("Ticket", id);
        return await db.Set<TicketEscalation>().AsNoTracking().Where(e => e.TicketId == id)
            .OrderByDescending(e => e.EscalatedAtUtc).ThenByDescending(e => e.Id).ToListAsync(ct);
    }
}

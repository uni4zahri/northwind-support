using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;
namespace SupportDesk.UnitTests.Infrastructure;
public sealed class TicketTriageTests
{
    [Fact]
    public async Task EscalationRestartsWindowKeepsEligibleOwnerAndPersistsHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new FixedClock();
        var category = new Category("Specialist", true, false);
        var customer = new Customer("Acme", "a@example.test", null, CustomerTier.Premium, clock.UtcNow);
        var agent = new Agent("Agent", "agent@example.test", 1, clock.UtcNow);
        db.AddRange(category, customer, agent); await db.SaveChangesAsync();
        agent.AddSpecialization(category.Id); await db.SaveChangesAsync();
        var ticket = Ticket.Raise("TCK-1", "Issue", "Details", customer.Id, category.Id, TicketPriority.Medium, clock.UtcNow);
        var settings = new SlaSettings();
        var service = new TicketTriage(db, clock, new TriagePolicy(settings), new TicketQueries(db, clock, settings));
        await service.ApplyAsync(ticket, false, default);
        Assert.Equal(agent.Id, ticket.AssignedAgentId);
        Assert.Equal(clock.UtcNow.AddHours(12), ticket.DueAtUtc);
        db.Add(ticket); await db.SaveChangesAsync();
        var previousDue = ticket.DueAtUtc;
        clock.Advance(TimeSpan.FromHours(2));
        var result = await service.EscalateAsync(ticket.Id, "Business impact increased", "Alex", default);
        Assert.Equal(TicketPriority.High, result.Ticket.Priority);
        Assert.Equal(clock.UtcNow.AddHours(4), result.Ticket.DueAtUtc);
        Assert.Equal(agent.Id, result.Ticket.AssignedAgent!.Id);
        var row = Assert.Single(await service.HistoryAsync(ticket.Id, default));
        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal(previousDue, row.FromDueAtUtc);
        Assert.Equal(result.Ticket.DueAtUtc, row.ToDueAtUtc);
        Assert.Equal("Alex", row.EscalatedBy);
        Assert.Equal(clock.UtcNow, row.EscalatedAtUtc);
        agent.Deactivate(); await db.SaveChangesAsync();
        var next = await service.EscalateAsync(ticket.Id, "Impact increased again", "Alex", default);
        Assert.Null(next.Ticket.AssignedAgent);
        Assert.Equal(2, (await service.HistoryAsync(ticket.Id, default)).Count);
        db.Remove(result.Escalation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}

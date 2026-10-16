using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class TicketFilteringTests
{
    [Fact]
    public async Task FiltersBeforeCountingAndPaging()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var customer = new Customer("Acme Customer", "acme@example.test", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        db.AddRange(customer, category);
        await db.SaveChangesAsync();
        db.AddRange(Ticket.Raise("TCK-0001", "First", "First issue", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow),
            Ticket.Raise("TCK-0002", "Second", "Second issue", customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow));
        await db.SaveChangesAsync();
        var queries = new TicketQueries(db, new FixedClock(), new SlaSettings());
        var priority = await queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High }, default);
        Assert.Equal(1, priority.TotalCount);
        Assert.Equal(TicketPriority.High, Assert.Single(priority.Items).Priority);
        var combined = await queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High, Status = TicketStatus.New, UnassignedOnly = true, CustomerId = customer.Id, CategoryId = category.Id }, default);
        Assert.Equal(1, combined.TotalCount);
        var search = await queries.GetPagedAsync(new TicketQuery { Search = "  Acme Customer  ", PageSize = 1 }, default);
        Assert.Equal(2, search.TotalCount);
        Assert.Equal(2, search.TotalPages);
        Assert.Single(search.Items);
        Assert.Empty((await queries.GetPagedAsync(new TicketQuery { AssignedAgentId = 999, UnassignedOnly = true }, default)).Items);
    }
}

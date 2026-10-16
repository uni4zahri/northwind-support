using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
namespace SupportDesk.UnitTests.Infrastructure;
public sealed class SlaSqlTranslationTests
{
    [Fact]
    public void SlaFilterTranslatesToSqlBeforePaging()
    {
        using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
        var queries = new TicketQueries(db, new FixedClock(), new SlaSettings());
        // Inspect the actual query pipeline without requiring a running SQL Server.
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var source = typeof(TicketQueries).GetMethod("TicketsWithLabels", flags)!.Invoke(queries, null);
        var filtered = (IQueryable)typeof(TicketQueries).GetMethod("ApplyFilters", flags)!.Invoke(queries,
            [source, new TicketQuery { SlaStatus = SlaStatus.AtRisk, Priority = TicketPriority.High }])!;
        var sql = filtered.ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains("CASE", sql);
        Assert.Contains("DATEADD", sql);
        Assert.Contains("DATEDIFF", sql);
        Assert.Contains("Priority", sql);
    }
}

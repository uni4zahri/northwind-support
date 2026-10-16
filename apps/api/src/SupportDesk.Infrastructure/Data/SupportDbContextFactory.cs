using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace SupportDesk.Infrastructure.Data;

// Schema generation should not require a JWT secret or a running web host.
public sealed class SupportDbContextFactory : IDesignTimeDbContextFactory<SupportDbContext>
{
    public SupportDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost,1433;Database=SupportDesk;Integrated Security=True;TrustServerCertificate=True";
        return new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlServer(connection).Options);
    }
}

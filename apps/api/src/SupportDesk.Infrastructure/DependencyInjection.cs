using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        var sla = new SupportDesk.Domain.Aggregates.Tickets.SlaSettings();
        configuration.GetSection("Sla").Bind(sla);
        if (sla.MinimumWindowHours <= 0 || sla.PremiumCustomerMultiplier <= 0 || sla.AtRiskThresholdPercent < 0 || sla.AtRiskThresholdPercent > 100
            || Enum.GetValues<SupportDesk.Domain.Aggregates.Tickets.TicketPriority>().Any(p => !sla.BaseWindowHours.TryGetValue(p, out var h) || h <= 0))
            throw new InvalidOperationException("Invalid SLA configuration.");
        services.AddSingleton(sla);
        services.AddSingleton<SupportDesk.Domain.Aggregates.Tickets.TriagePolicy>();
        services.AddScoped<ITicketTriage, TicketTriage>();
        services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<SupportDesk.Domain.Aggregates.Agents.Agent>, Microsoft.AspNetCore.Identity.PasswordHasher<SupportDesk.Domain.Aggregates.Agents.Agent>>();
        services.AddScoped<IAgentCredentials, AgentCredentials>();
        services.AddScoped<SupportDbSeeder>();

        return services;
    }
}

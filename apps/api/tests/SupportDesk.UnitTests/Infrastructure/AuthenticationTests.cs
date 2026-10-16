using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;
namespace SupportDesk.UnitTests.Infrastructure;

public sealed class AuthenticationTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public Factory() => connection.Open();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", "Only_An_Integration_Test_Key_01234567890123456789");
            builder.UseSetting("ConnectionStrings:Default", "Server=unused;Database=unused");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SupportDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
                services.AddDbContext<SupportDbContext>(options => options.UseSqlite(connection));
            });
        }
        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            await db.Database.EnsureCreatedAsync();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Agent>>();
            var active = new Agent("Alex", "alex@example.test", 5, FixedClock.DefaultNow);
            active.SetPasswordHash(hasher.HashPassword(active, "LocalTestPassword!"));
            var inactive = new Agent("Inactive", "inactive@example.test", 5, FixedClock.DefaultNow);
            inactive.SetPasswordHash(hasher.HashPassword(inactive, "LocalTestPassword!"));
            inactive.Deactivate();
            db.AddRange(active, inactive); await db.SaveChangesAsync();
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
    }
    [Fact]
    public async Task WrongPasswordUnknownEmailAndInactiveAgentReturnIdentical401WithoutToken()
    {
        using var factory = new Factory(); using var client = factory.CreateClient(); await factory.SeedAsync();
        string? expected = null;
        foreach (var (email, password) in new[] { ("alex@example.test", "wrong"), ("missing@example.test", "wrong"), ("inactive@example.test", "LocalTestPassword!") })
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.TryGetProperty("token", out _));
            var detail = body.GetProperty("detail").GetString();
            expected ??= detail; Assert.Equal(expected, detail);
        }
    }
    [Fact]
    public async Task ProtectedEndpointsRequireValidTokenAndMeReturnsUser()
    {
        using var factory = new Factory(); using var client = factory.CreateClient(); await factory.SeedAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = " ALEX@example.test ", password = "LocalTestPassword!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/agents")).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Alex", me.GetProperty("fullName").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agents")).StatusCode);
    }
    [Fact]
    public void PasswordHasherUsesSaltAndRejectsWrongPassword()
    {
        var agent = new Agent("Alex", "alex@example.test", 5, FixedClock.DefaultNow);
        var hasher = new PasswordHasher<Agent>();
        var first = hasher.HashPassword(agent, "LocalTestPassword!");
        Assert.NotEqual(first, hasher.HashPassword(agent, "LocalTestPassword!"));
        Assert.NotEqual("LocalTestPassword!", first);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(agent, first, "LocalTestPassword!"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(agent, first, "wrong"));
    }
}

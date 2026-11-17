using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MinGo.IdentityKit.IntegrationTests;

public sealed class HostOwnedIdentityTests
{
    private const string Email = "operator@example.test";
    private const string Password = "Legacy!1";
    private const string Role = "Operator";
    private const string CookieName = "Host.Auth";

    [Fact]
    public async Task HostStoreRetainsExistingIdentityAndSupportsRoleProtectedRequests()
    {
        await using var database = new SqliteConnection("Data Source=:memory:");
        await database.OpenAsync();
        var userId = await CreateLegacyAccountAsync(database);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddDbContext<HostDbContext>(options => options.UseSqlite(database));
        builder.Services.AddIdentityKit()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<HostDbContext>();
        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        });
        builder.Services.ConfigureApplicationCookie(options => options.Cookie.Name = CookieName);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPost("/api/auth/login", async (LoginInput input, UserManager<IdentityUser> users,
            SignInManager<IdentityUser> signIn) =>
        {
            var user = await users.FindByEmailAsync(input.Email);
            if (user is null)
                return Results.Unauthorized();

            var result = await signIn.PasswordSignInAsync(user, input.Password, true, false);
            return result.Succeeded ? Results.Ok() : Results.Unauthorized();
        });
        app.MapGet("/api/protected", (ClaimsPrincipal user) =>
                TypedResults.Ok(user.FindFirstValue(ClaimTypes.NameIdentifier)))
            .RequireAuthorization(policy => policy.RequireRole(Role));
        app.MapPost("/api/auth/logout", async (SignInManager<IdentityUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        await app.StartAsync();

        Assert.Equal(8, app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Password.RequiredLength);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var anonymous = await client.GetAsync("/api/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginInput(Email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith(CookieName + "="));
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';', 2)[0]);
        Assert.Equal(userId, await client.GetFromJsonAsync<string>("/api/protected"));

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var expired = Assert.Single(logout.Headers.GetValues("Set-Cookie"), value => value.StartsWith(CookieName + "="));
        Assert.Equal(CookieName + "=", expired.Split(';', 2)[0]);
        client.DefaultRequestHeaders.Remove("Cookie");
        using var signedOut = await client.GetAsync("/api/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
    }

    private static async Task<string> CreateLegacyAccountAsync(SqliteConnection database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HostDbContext>(options => options.UseSqlite(database));
        services.AddIdentity<IdentityUser, IdentityRole>(options => options.Password.RequiredLength = 8)
            .AddEntityFrameworkStores<HostDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HostDbContext>().Database.EnsureCreatedAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(Role))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "legacy-operator", Email = Email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, Role)).Succeeded);
        return user.Id;
    }

    private sealed record LoginInput(string Email, string Password);

    private sealed class HostDbContext(DbContextOptions<HostDbContext> options) : IdentityDbContext<IdentityUser>(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<IdentityUser>().ToTable("Users");
            builder.Entity<IdentityRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        }
    }
}

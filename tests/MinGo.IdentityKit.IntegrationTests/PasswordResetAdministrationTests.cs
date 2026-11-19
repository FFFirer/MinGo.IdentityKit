using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MinGo.IdentityKit.IntegrationTests;

public sealed class PasswordResetAdministrationTests
{
    [Fact]
    public async Task ExistingGuidIdentityUsesHostPolicyAndNativeTokensWithoutChangingDefaults()
    {
        await using var database = new SqliteConnection("Data Source=:memory:");
        await database.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<GuidContext>(options => options.UseSqlite(database));
        services.AddAuthentication(options => options.DefaultScheme = "HostScheme");
        services.AddAuthorizationBuilder().AddPolicy("HostReset",
            policy => policy.RequireRole("HostAdministrator"));
        services.AddIdentityCore<IdentityUser<Guid>>(options => options.Password.RequiredLength = 9)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<GuidContext>()
            .AddDefaultTokenProviders()
            .AddPasswordResetAdministration(options => options.AuthorizationPolicy = "HostReset");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<GuidContext>().Database.EnsureCreatedAsync();
        var users = sp.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var user = new IdentityUser<Guid>("target") { Id = Guid.NewGuid() };
        Assert.True((await users.CreateAsync(user, "Initial!123")).Succeeded);
        var feature = sp.GetRequiredService<PasswordResetAdministration<IdentityUser<Guid>>>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var member = new ClaimsPrincipal(new ClaimsIdentity([], "Host"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "HostAdministrator")], "Host"));
        Assert.Equal(PasswordResetTokenStatus.Forbidden,
            (await feature.GenerateTokenAsync(anonymous, user.Id.ToString())).Status);
        Assert.Equal(PasswordResetTokenStatus.Forbidden,
            (await feature.GenerateTokenAsync(member, user.Id.ToString())).Status);
        Assert.Equal(PasswordResetTokenStatus.UserNotFound,
            (await feature.GenerateTokenAsync(admin, Guid.NewGuid().ToString())).Status);
        var result = await feature.GenerateTokenAsync(admin, user.Id.ToString());
        Assert.Equal(PasswordResetTokenStatus.Succeeded, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.True((await users.ResetPasswordAsync(user, result.Token!, "Changed!456")).Succeeded);
        Assert.True(await users.CheckPasswordAsync(user, "Changed!456"));
        Assert.False(await users.CheckPasswordAsync(user, "Initial!123"));
        Assert.False((await users.ResetPasswordAsync(user, result.Token!, "Replay!789")).Succeeded);
        Assert.Equal("HostScheme", sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme);
        Assert.Equal(9, sp.GetRequiredService<IOptions<IdentityOptions>>().Value.Password.RequiredLength);
        Assert.DoesNotContain(sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value.Schemes,
            scheme => scheme.Name == IdentityConstants.ApplicationScheme);
    }

    [Fact]
    public void MissingHostPolicyFailsOptionsValidation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityCore<IdentityUser>().AddPasswordResetAdministration(_ => { });
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<PasswordResetAdministrationOptions>>().Value);
    }

    [Fact]
    public async Task SuperUserInitializationUsesHostRoleAndIsIdempotent()
    {
        await using var database = new SqliteConnection("Data Source=:memory:");
        await database.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HostContext>(options => options.UseSqlite(database));
        services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<HostContext>();
        services.Configure<IdentityKitOptions>(options =>
        {
            options.SuperUser.Enabled = true;
            options.SuperUser.UserName = "host-admin";
            options.SuperUser.InitialPassword = "Initial!123";
        });
        await using var provider = services.BuildServiceProvider();
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<HostContext>().Database.EnsureCreatedAsync();
            Assert.True((await setup.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
                .CreateAsync(new IdentityRole("HostAdmin"))).Succeeded);
        }
        var grants = 0;
        async Task Grant(IServiceProvider sp, IdentityUser user)
        {
            grants++;
            Assert.True((await sp.GetRequiredService<UserManager<IdentityUser>>()
                .AddToRoleAsync(user, "HostAdmin")).Succeeded);
        }
        Task<bool> Verify(IServiceProvider sp, IdentityUser user) =>
            sp.GetRequiredService<UserManager<IdentityUser>>().IsInRoleAsync(user, "HostAdmin");
        await provider.InitializeSuperUserAsync<IdentityUser, HostContext>(Grant, Verify);
        await provider.InitializeSuperUserAsync<IdentityUser, HostContext>(Grant, Verify);
        Assert.Equal(1, grants);
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await users.FindByNameAsync("host-admin");
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin, "HostAdmin"));
        Assert.DoesNotContain(await users.GetClaimsAsync(admin),
            claim => claim.Type == IdentityKitRegistration.AdministratorClaim);
    }

    [Fact]
    public async Task OptionalApiUsesHostAuthorizationAndRejectsMissingCsrf()
    {
        await using var database = new SqliteConnection("Data Source=:memory:");
        await database.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddDbContext<HostContext>(options => options.UseSqlite(database));
        builder.Services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<HostContext>()
            .AddDefaultTokenProviders()
            .AddPasswordResetAdministration(options => options.AuthorizationPolicy = "HostReset");
        builder.Services.AddAuthorizationBuilder().AddPolicy("HostReset",
            policy => policy.RequireRole("HostAdmin"));
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-HOST-CSRF");
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPasswordResetAdministrationApi<IdentityUser>();
        app.MapPost("/test-login", async (SignInManager<IdentityUser> signIn,
            UserManager<IdentityUser> users) =>
        {
            await signIn.SignInAsync((await users.FindByNameAsync("host-admin"))!, false);
            return Results.NoContent();
        });
        string userId;
        await using (var setup = app.Services.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<HostContext>().Database.EnsureCreatedAsync();
            var users = setup.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser("host-admin");
            Assert.True((await users.CreateAsync(user, "Initial!123")).Succeeded);
            Assert.True((await setup.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
                .CreateAsync(new IdentityRole("HostAdmin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "HostAdmin")).Succeeded);
            userId = user.Id;
        }
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        var endpoint = "/api/account-administration/users/" + userId + "/password-reset-token";
        using var anonymous = await client.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var login = await client.PostAsync("/test-login", null);
        var loginCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(".AspNetCore.Identity.Application=")).Split(';', 2)[0];
        client.DefaultRequestHeaders.Add("Cookie", loginCookie);
        using var missingCsrf = await client.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        using var csrf = await client.GetAsync("/api/account-administration/csrf");
        var token = await csrf.Content.ReadFromJsonAsync<CsrfResponse>();
        var csrfCookie = Assert.Single(csrf.Headers.GetValues("Set-Cookie")).Split(';', 2)[0];
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", loginCookie + "; " + csrfCookie);
        client.DefaultRequestHeaders.Add("X-HOST-CSRF", token!.RequestToken);
        using var allowed = await client.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.True(allowed.Headers.CacheControl!.NoStore);
        var payload = await allowed.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        Assert.False(string.IsNullOrWhiteSpace(payload!["token"]!.GetValue<string>()));
    }
    private sealed class HostContext(DbContextOptions<HostContext> options)
        : IdentityDbContext<IdentityUser>(options);
    private sealed class GuidContext(DbContextOptions<GuidContext> options)
        : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options);
}



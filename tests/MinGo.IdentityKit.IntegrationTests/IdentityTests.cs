using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MinGo.IdentityKit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MinGo.IdentityKit.IntegrationTests;

public sealed class IdentityTests : IDisposable
{
    private const string Password = "Local-Test!1234";
    private const string Prefix = "/api/identity";
    private readonly string directory = Directory.CreateTempSubdirectory("local-identity-").FullName;

    [Fact]
    public async Task EmbeddedApiRegistersSignsInReadsCurrentUserAndSignsOut()
    {
        using var host = Host<Samples.EmbeddedHttpApi.Program>("embedded", true);
        using var client = Client(host);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            Prefix + "/register", new RegisterRequest("alice", Password, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, await Post(client, "/register", new RegisterRequest("alice", Password, null)));
        Assert.Equal(HttpStatusCode.Unauthorized, await Post(client, "/login", new LoginRequest("alice", "incorrect")));
        Assert.Equal(HttpStatusCode.NoContent, await Post(client, "/login", new LoginRequest("alice", Password)));
        Assert.Equal("alice", (await client.GetFromJsonAsync<CurrentUserResponse>(Prefix + "/me"))!.UserName);

        Assert.Equal(HttpStatusCode.NoContent, await Post(client, "/logout", new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task LockoutRejectsCorrectPasswordAfterFiveFailures()
    {
        using var host = Host<Samples.EmbeddedHttpApi.Program>("lockout", true);
        using var client = Client(host);
        Assert.Equal(HttpStatusCode.NoContent, await Post(client, "/register", new RegisterRequest("alice", Password, null)));

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, await Post(client, "/login", new LoginRequest("alice", "incorrect")));

        Assert.Equal(HttpStatusCode.Unauthorized, await Post(client, "/login", new LoginRequest("alice", Password)));
        await using var scope = host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByNameAsync("alice"))!));
    }

    [Fact]
    public async Task CentralHostIsIndependentAndHonorsRegistrationPolicy()
    {
        using var embedded = Host<Samples.EmbeddedHttpApi.Program>("embedded", true);
        using var central = Host<Samples.CentralizedIdentityHost.Program>("central", false, Password);
        using var embeddedClient = Client(embedded);
        using var centralClient = Client(central);
        Assert.Equal(HttpStatusCode.NoContent, await Post(embeddedClient, "/register", new RegisterRequest("alice", Password, null)));
        Assert.Equal(HttpStatusCode.Forbidden, await Post(centralClient, "/register", new RegisterRequest("alice", Password, null)));
        Assert.Equal(HttpStatusCode.Unauthorized, await Post(centralClient, "/login", new LoginRequest("alice", Password)));
        Assert.Equal(HttpStatusCode.NoContent, await Post(centralClient, "/login", new LoginRequest("admin", Password)));
        Assert.Equal("admin", (await centralClient.GetFromJsonAsync<CurrentUserResponse>(Prefix + "/me"))!.UserName);
    }

    [Fact]
    public async Task MigrationAndSuperUserInitializationAreRepeatable()
    {
        using (var initial = Host<Samples.CentralizedIdentityHost.Program>("central", false, Password))
        {
            await initial.Services.InitializeIdentityKitAsync();
            await using var scope = initial.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IdentityKitDbContext>();
            Assert.Single(await context.Database.GetAppliedMigrationsAsync());
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = Assert.Single(await users.Users.ToListAsync());
            Assert.Contains(await users.GetClaimsAsync(admin), c => c.Type == "identity_kit_administrator" && c.Value == "true");
        }

        using var restarted = Host<Samples.CentralizedIdentityHost.Program>("central", false, "Changed-Test!1234");
        using var client = Client(restarted);
        Assert.Equal(HttpStatusCode.NoContent, await Post(client, "/login", new LoginRequest("admin", Password)));
    }

    [Fact]
    public async Task RenderingUsesNativeCookieAndRejectsDirectRegistrationWhenClosed()
    {
        using var host = Host<Samples.EmbeddedServerRendering.Program>("rendering", false, Password);
        using var client = Client(host);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Prefix + "/csrf")).StatusCode);
        Assert.Contains("Public registration is closed.", await (await Form(client, "Register")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Form(client, "Login")).StatusCode);
        Assert.Contains("Signed in as admin", await client.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.Redirect, (await Form(client, "Logout")).StatusCode);
        Assert.DoesNotContain("Signed in as", await client.GetStringAsync("/"));
    }

    private WebApplicationFactory<T> Host<T>(string name, bool registration, string? adminPassword = null)
        where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
                services.AddDataProtection().UseEphemeralDataProtectionProvider());
            builder.UseSetting("ConnectionStrings:Identity", $"Data Source={Path.Combine(directory, name + ".db")};Pooling=False");
            builder.UseSetting("IdentityKit:Registration:Enabled", registration.ToString());
            builder.UseSetting("IdentityKit:SuperUser:Enabled", (adminPassword is not null).ToString());
            if (adminPassword is not null)
            {
                builder.UseSetting("IdentityKit:SuperUser:UserName", "admin");
                builder.UseSetting("IdentityKit:SuperUser:InitialPassword", adminPassword);
            }
        });

    private static HttpClient Client<T>(WebApplicationFactory<T> host) where T : class =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static async Task<HttpStatusCode> Post<T>(HttpClient client, string path, T body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>(Prefix + "/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, Prefix + path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> Form(HttpClient client, string handler)
    {
        var html = await client.GetStringAsync("/");
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)" """.TrimEnd());
        Assert.True(token.Success);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = "admin",
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token.Groups[1].Value
        });
        return await client.PostAsync("/?handler=" + handler, form);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}

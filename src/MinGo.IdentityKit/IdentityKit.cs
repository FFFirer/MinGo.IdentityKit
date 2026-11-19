using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinGo.IdentityKit;

public sealed class IdentityKitOptions
{
    public RegistrationOptions Registration { get; set; } = new();
    public SuperUserOptions SuperUser { get; set; } = new();
}

public sealed class RegistrationOptions
{
    public bool Enabled { get; set; }
}

public sealed class SuperUserOptions
{
    public bool Enabled { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? InitialPassword { get; set; }
}

public static class IdentityKitRegistration
{
    public const string AdministratorPolicy = "IdentityKitAdministrator";
    public const string AdministratorClaim = "identity_kit_administrator";

    public static IdentityBuilder AddIdentityKit(
        this IServiceCollection services, Action<IdentityKitOptions>? configure = null) =>
        services.AddIdentityKitLocalAccounts<IdentityUser>(configure);

    public static IdentityBuilder AddIdentityKit<TUser>(
        this IServiceCollection services, Action<IdentityKitOptions>? configure = null)
        where TUser : IdentityUser, new() =>
        services.AddIdentityKitLocalAccounts<TUser>(configure);

    /// <summary>Installs the complete local-account preset with default IdentityUser.</summary>
    public static IdentityBuilder AddIdentityKitLocalAccounts(
        this IServiceCollection services, Action<IdentityKitOptions>? configure = null) =>
        services.AddIdentityKitLocalAccounts<IdentityUser>(configure);

    /// <summary>Installs local Cookie authentication, native managers and preset policies.</summary>
    public static IdentityBuilder AddIdentityKitLocalAccounts<TUser>(
        this IServiceCollection services, Action<IdentityKitOptions>? configure = null)
        where TUser : IdentityUser, new()
    {
        services.AddOptions<IdentityKitOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        services.AddAuthorizationBuilder().AddPolicy(AdministratorPolicy, policy =>
            policy.RequireAuthenticatedUser().RequireClaim(AdministratorClaim, "true"));
        services.AddProblemDetails();
        services.AddScoped<PublicRegistration<TUser>>();

        return services.AddIdentityCore<TUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
        }).AddSignInManager().AddDefaultTokenProviders();
    }
}

public sealed class PublicRegistration<TUser>(
    UserManager<TUser> users, IOptionsMonitor<IdentityKitOptions> options)
    where TUser : IdentityUser, new()
{
    public const string RegistrationClosed = nameof(RegistrationClosed);

    public Task<IdentityResult> RegisterAsync(string userName, string password, string? email = null)
    {
        if (!options.CurrentValue.Registration.Enabled)
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = RegistrationClosed,
                Description = "Public registration is closed."
            }));

        return users.CreateAsync(new TUser { UserName = userName, Email = email }, password);
    }
}

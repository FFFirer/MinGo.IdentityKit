using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinGo.IdentityKit;

public static class SuperUserInitialization
{
    public static Task InitializeSuperUserAsync<TUser, TContext>(
        this IServiceProvider services, CancellationToken cancellationToken = default)
        where TUser : IdentityUser, new()
        where TContext : DbContext =>
        services.InitializeSuperUserAsync<TUser, TContext>(
            async (provider, user) =>
            {
                var result = await provider.GetRequiredService<UserManager<TUser>>()
                    .AddClaimAsync(user, new(IdentityKitRegistration.AdministratorClaim, "true"));
                RequireSuccess(result);
            },
            async (provider, user) => (await provider.GetRequiredService<UserManager<TUser>>()
                .GetClaimsAsync(user)).Any(claim =>
                    claim.Type == IdentityKitRegistration.AdministratorClaim && claim.Value == "true"),
            cancellationToken);

    /// <summary>The host supplies privilege assignment and verifies the resulting access.</summary>
    public static async Task InitializeSuperUserAsync<TUser, TContext>(
        this IServiceProvider services,
        Func<IServiceProvider, TUser, Task> grantAccess,
        Func<IServiceProvider, TUser, Task<bool>> hasAccess,
        CancellationToken cancellationToken = default)
        where TUser : IdentityUser, new()
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(grantAccess);
        ArgumentNullException.ThrowIfNull(hasAccess);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var options = provider.GetRequiredService<IOptions<IdentityKitOptions>>().Value.SuperUser;
        if (!options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.UserName))
            throw new InvalidOperationException("Superuser initialization requires a username.");

        var users = provider.GetRequiredService<UserManager<TUser>>();
        var database = provider.GetRequiredService<TContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MinGo.IdentityKit.SuperUser");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var existing = await users.FindByNameAsync(options.UserName);
        if (existing is not null)
        {
            if (!await hasAccess(provider, existing))
                throw new InvalidOperationException("The configured superuser name is occupied by another account.");

            logger.LogInformation("The configured superuser is already initialized.");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.InitialPassword))
            throw new InvalidOperationException("Superuser creation requires an initial password.");
        if (!string.IsNullOrWhiteSpace(options.Email) && await users.FindByEmailAsync(options.Email) is not null)
            throw new InvalidOperationException("The configured superuser email is already in use.");

        var user = new TUser { UserName = options.UserName, Email = options.Email };
        RequireSuccess(await users.CreateAsync(user, options.InitialPassword));
        await grantAccess(provider, user);
        if (!await hasAccess(provider, user))
            throw new InvalidOperationException("The initialized account does not have the required host access.");
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("The configured superuser was created.");
    }

    private static void RequireSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Superuser initialization failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}

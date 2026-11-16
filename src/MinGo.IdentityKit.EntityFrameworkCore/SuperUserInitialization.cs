using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinGo.IdentityKit;

public static class SuperUserInitialization
{
    public static async Task InitializeSuperUserAsync<TUser, TContext>(
        this IServiceProvider services, CancellationToken cancellationToken = default)
        where TUser : IdentityUser, new()
        where TContext : DbContext
    {
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
            var claims = await users.GetClaimsAsync(existing);
            if (!claims.Any(c => c.Type == IdentityKitRegistration.AdministratorClaim && c.Value == "true"))
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
        RequireSuccess(await users.AddClaimAsync(user, new(
            IdentityKitRegistration.AdministratorClaim, "true")));
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

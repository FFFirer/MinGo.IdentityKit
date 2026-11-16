using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace MinGo.IdentityKit;

public sealed class IdentityKitDbContext(DbContextOptions<IdentityKitDbContext> options)
    : IdentityUserContext<IdentityUser>(options);

public static class SqliteRegistration
{
    public static IdentityBuilder UseSqlite(this IdentityBuilder identity, string connectionString)
    {
        if (identity.UserType != typeof(IdentityUser))
            throw new InvalidOperationException("Use a host-owned Identity DbContext for custom users.");

        identity.Services.AddDbContext<IdentityKitDbContext>(options => options.UseSqlite(connectionString));
        return identity.AddEntityFrameworkStores<IdentityKitDbContext>();
    }

    public static async Task InitializeIdentityKitAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityKitDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
        }

        await services.InitializeSuperUserAsync<IdentityUser, IdentityKitDbContext>(cancellationToken);
    }
}

internal sealed class SqliteDesignFactory : IDesignTimeDbContextFactory<IdentityKitDbContext>
{
    public IdentityKitDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<IdentityKitDbContext>().UseSqlite("Data Source=identity.db").Options);
}

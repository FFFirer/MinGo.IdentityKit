using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MinGo.IdentityKit;

/// <summary>Host-owned authorization for password recovery administration.</summary>
public sealed class PasswordResetAdministrationOptions
{
    public string AuthorizationPolicy { get; set; } = string.Empty;
}

public static class IdentityKitFeatures
{
    /// <summary>Adds only password reset administration to an existing Identity registration.</summary>
    public static IdentityBuilder AddPasswordResetAdministration(
        this IdentityBuilder identity, Action<PasswordResetAdministrationOptions> configure)
    {
        identity.Services.AddOptions<PasswordResetAdministrationOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.AuthorizationPolicy),
                "A host authorization policy is required.")
            .ValidateOnStart();
        identity.Services.TryAddScoped(typeof(PasswordResetAdministration<>));
        return identity;
    }
}

/// <summary>Uses the host policy and native token provider. The host owns token delivery.</summary>
public sealed class PasswordResetAdministration<TUser>(
    UserManager<TUser> users,
    IAuthorizationService authorization,
    IOptions<PasswordResetAdministrationOptions> options)
    where TUser : class
{
    public async Task<PasswordResetTokenResult> GenerateTokenAsync(ClaimsPrincipal actor, string userId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (actor.Identity?.IsAuthenticated != true)
            return new(PasswordResetTokenStatus.Forbidden);

        var allowed = await authorization.AuthorizeAsync(actor, userId, options.Value.AuthorizationPolicy);
        if (!allowed.Succeeded)
            return new(PasswordResetTokenStatus.Forbidden);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new(PasswordResetTokenStatus.UserNotFound);

        return new(PasswordResetTokenStatus.Succeeded,
            await users.GeneratePasswordResetTokenAsync(user));
    }
}

public enum PasswordResetTokenStatus { Succeeded, Forbidden, UserNotFound }

public sealed record PasswordResetTokenResult(PasswordResetTokenStatus Status, string? Token = null);

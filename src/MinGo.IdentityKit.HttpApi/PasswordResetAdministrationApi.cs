using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MinGo.IdentityKit;

public static class PasswordResetAdministrationApi
{
    /// <summary>The host registers antiforgery and chooses cookie settings using native options.</summary>
    public static RouteGroupBuilder MapPasswordResetAdministrationApi<TUser>(
        this IEndpointRouteBuilder endpoints, string prefix = "/api/account-administration")
        where TUser : class
    {
        var group = endpoints.MapGroup(prefix).RequireAuthorization();
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "no-store";
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            if (!HttpMethods.IsGet(context.Request.Method) &&
                !await antiforgery.IsRequestValidAsync(context))
                return Results.Problem(statusCode: 400, title: "Invalid CSRF token.");
            return await next(invocation);
        });
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new CsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken!)));
        group.MapPost("/users/{userId}/password-reset-token",
            async Task<Results<Ok<PasswordResetTokenResponse>, NotFound, ForbidHttpResult>> (string userId, HttpContext context, PasswordResetAdministration<TUser> passwords) =>
            {
                var result = await passwords.GenerateTokenAsync(context.User, userId);
                return result.Status switch
                {
                    PasswordResetTokenStatus.Succeeded => TypedResults.Ok(new PasswordResetTokenResponse(result.Token!)),
                    PasswordResetTokenStatus.UserNotFound => TypedResults.NotFound(),
                    _ => TypedResults.Forbid()
                };
            });
        return group;
    }
}

public sealed record PasswordResetTokenResponse(string Token);


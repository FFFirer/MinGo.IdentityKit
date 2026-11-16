using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MinGo.IdentityKit;

public static class IdentityApi
{
    public static IdentityBuilder AddHttpApi(this IdentityBuilder identity)
    {
        identity.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
        identity.Services.AddOpenApi();
        return identity;
    }

    public static RouteGroupBuilder MapIdentityKitApi(
        this IEndpointRouteBuilder endpoints, string prefix = "/api/identity") =>
        endpoints.MapIdentityKitApi<IdentityUser>(prefix);

    public static RouteGroupBuilder MapIdentityKitApi<TUser>(
        this IEndpointRouteBuilder endpoints, string prefix = "/api/identity")
        where TUser : IdentityUser, new()
    {
        var group = endpoints.MapGroup(prefix).WithTags("Local identity");
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new CsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken!)))
            .AllowAnonymous();

        var writes = group.MapGroup("").AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            if (!context.Request.Headers.ContainsKey("X-CSRF-TOKEN"))
                return TypedResults.Problem(statusCode: 400, title: "A CSRF token is required.");

            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(context))
                return TypedResults.Problem(statusCode: 400, title: "Invalid CSRF token.");

            return await next(invocation);
        });

        writes.MapPost("/register", async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> (
            RegisterRequest request, PublicRegistration<TUser> registration) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
                return TypedResults.Problem(statusCode: 400, title: "Username and password are required.");

            var result = await registration.RegisterAsync(request.UserName, request.Password, request.Email);
            if (result.Succeeded)
                return TypedResults.NoContent();
            if (result.Errors.Any(error => error.Code == PublicRegistration<TUser>.RegistrationClosed))
                return TypedResults.Problem(statusCode: 403, title: "Public registration is closed.");

            return TypedResults.ValidationProblem(result.Errors.GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }).AllowAnonymous();

        writes.MapPost("/login", async Task<Results<NoContent, ProblemHttpResult>> (
            LoginRequest request, SignInManager<TUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
                return TypedResults.Problem(statusCode: 401, title: "Unable to sign in.");

            var result = await signIn.PasswordSignInAsync(request.UserName, request.Password,
                isPersistent: false, lockoutOnFailure: true);
            if (result.Succeeded)
                return TypedResults.NoContent();

            return TypedResults.Problem(statusCode: 401, title: "Unable to sign in.");
        }).AllowAnonymous();

        writes.MapPost("/logout", async (SignInManager<TUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return TypedResults.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> (
            HttpContext context, UserManager<TUser> users) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var user = await users.GetUserAsync(context.User);
            if (user is null)
                return TypedResults.Unauthorized();

            return TypedResults.Ok(new CurrentUserResponse(user.Id, user.UserName, user.Email));
        }).RequireAuthorization();

        return group;
    }
}

/// <summary>Local account credentials.</summary>
public sealed record LoginRequest(string UserName, string Password);

/// <summary>Details for creating a local account.</summary>
public sealed record RegisterRequest(string UserName, string Password, string? Email);

/// <summary>The current user's public account fields.</summary>
public sealed record CurrentUserResponse(string Id, string? UserName, string? Email);

/// <summary>Request token to send in the X-CSRF-TOKEN header.</summary>
public sealed record CsrfResponse(string RequestToken);

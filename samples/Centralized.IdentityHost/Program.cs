using MinGo.IdentityKit;

namespace Samples.CentralizedIdentityHost;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Configure<IdentityKitOptions>(builder.Configuration.GetSection("IdentityKit"));
        builder.Services.AddIdentityKit()
            .UseSqlite(builder.Configuration.GetConnectionString("Identity") ?? "Data Source=Centralized.IdentityHost.db")
            .AddHttpApi();


        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapIdentityKitApi();
        app.MapOpenApi();
        await app.Services.InitializeIdentityKitAsync();
        await app.RunAsync();
    }
}

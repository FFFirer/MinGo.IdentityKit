# MinGo.IdentityKit

ASP.NET Core 10 本地账户认证整合。当前交付范围为 [M0](docs/START.md)。

## 快速开始

需要 .NET 10 SDK。运行 `dotnet dev-certs https --trust` 信任本地开发证书。

```powershell
$env:IdentityKit__Registration__Enabled = "true"
dotnet run --project samples/Embedded.ServerRendering -- --urls https://localhost:7443
```

在浏览器打开 https://localhost:7443，创建账户并登录。也可选择下面的宿主运行相同的认证服务。

| 示例 | 入口 |
| --- | --- |
| Embedded.ServerRendering | Razor Pages `/` |
| Embedded.HttpApi | `/api/identity` 和 `/openapi/v1.json` |
| Centralized.IdentityHost | 独立宿主，使用相同 HTTP 契约 |

默认关闭公开注册。超级用户通过 `IdentityKit:SuperUser:Enabled`、`UserName`、可选的 `Email` 和 `InitialPassword` 配置。首次创建时从宿主的 Secret 或环境变量注入密码，例如 `IdentityKit__SuperUser__InitialPassword`。

## 接入宿主

引用 `MinGo.IdentityKit.EntityFrameworkCore.Sqlite` 即可使用默认 SQLite 存储和核心服务。HTTP API 宿主额外引用 `MinGo.IdentityKit.HttpApi`。

```csharp
builder.Services.Configure<IdentityKitOptions>(
    builder.Configuration.GetSection("IdentityKit"));
builder.Services.AddIdentityKit()
    .UseSqlite(builder.Configuration.GetConnectionString("Identity")!)
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
```

Razor Pages 宿主调用 `AddRazorPages()` 和 `MapRazorPages()`，并通过原生 `SignInManager` 登录和登出。公开注册入口使用 `PublicRegistration<TUser>`。

运行验证：

```powershell
dotnet test MinGo.IdentityKit.slnx
```

参见 [架构与部署](docs/architecture.md)、[HTTP API](docs/api.md) 和 [路线图](docs/roadmap.md)。

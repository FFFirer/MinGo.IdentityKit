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

## 发布 NuGet 包

发布通过 GitHub Actions OIDC 和 [NuGet.org Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) 完成。工作流在推送包之前获取短期凭据。

首次发布前，在 [NuGet.org Trusted Publishing](https://www.nuget.org/account/trustedpublishing) 添加 GitHub 策略。

| 字段 | 值 |
| --- | --- |
| Repository Owner | `FFFirer` |
| Repository | `MinGo.IdentityKit` |
| Workflow File | `publish-nuget.yml` |
| Environment | 留空 |

策略的包范围须覆盖 `MinGo.IdentityKit`、`MinGo.IdentityKit.EntityFrameworkCore`、`MinGo.IdentityKit.EntityFrameworkCore.Sqlite` 和 `MinGo.IdentityKit.HttpApi`，并允许首次创建包和发布新版本。在 GitHub 仓库的 Settings → Secrets and variables → Actions 中添加仓库级 Secret `NUGET_USER`，值为 NuGet.org 个人资料用户名。

策略配置完成后，推送版本标签会启动工作流。Actions 先运行集成测试，再发布 `src/` 下的四个库。整个发布使用同一版本，包括包之间的依赖。

```powershell
git tag v0.1.0
git push origin v0.1.0
```

预发布版本可使用 `v0.1.0-preview.1`。重复运行同一标签时跳过已发布的包。

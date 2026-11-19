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

### 接入已有 Identity 数据库的宿主

ReverseProxy 等已有 `IdentityDbContext` 的应用可以使用原生角色和宿主存储。保留宿主现有的 `AppDbContext` 注册和迁移程序集，然后接线：

```csharp
builder.Services.AddIdentityKit()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "MinGo.Auth";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});
```

这套接法由宿主管理数据库迁移和首个账号初始化。迁移已有部署时，从一致备份恢复到独立新库，再让宿主连接新库。从零部署时，使用宿主迁移初始化空库。已有邮箱登录、路由和响应格式由宿主的认证入口提供。旧 Cookie 延续还依赖原有 Data Protection 密钥及应用标识。参见 [MinGo 登录迁移计划](docs/mingo-login-migration-plan.md)。

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

## 已有宿主按需接入

已有 Identity 的宿主可在自己的 IdentityBuilder 上选择功能。

```csharp
builder.Services.AddIdentity<AppUser, AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddPasswordResetAdministration(options =>
        options.AuthorizationPolicy = "ManageAccountPasswords");
```

PasswordResetAdministration<TUser> 使用宿主授权策略，以目标用户 ID 作为授权资源，
再调用原生 UserManager 生成密码重置令牌。支持 IdentityUser<Guid> 等宿主用户模型。
宿主负责账号管理 UI、令牌交付、防伪、审计及速率限制。令牌属于敏感凭据，
HTTP 响应应设置 Cache-Control: no-store，日志应避免记录令牌。

AddIdentityKitLocalAccounts<TUser>() 是完整本地账号 Preset 的显式入口，
AddIdentityKit() 保留兼容行为。完整 Preset 提供默认 Cookie、密码和管理员 claim 策略。
功能扩展只注册自己的服务和选项，默认认证方案、Cookie、角色与存储由宿主装配。
宿主应在启动时注册所指定的授权策略和原生密码重置 token provider。

宿主可将 ManageAccountPasswords 映射到现有角色、claim 或资源权限。
IdentityKitAdministrator 是完整 Preset 中的可选默认策略。
首个管理员初始化属于部署流程，现有 InitializeSuperUserAsync 继续使用其专用 claim。
使用宿主管理员体系的应用应沿用宿主的初始化与授权流程。


### 首个管理员使用宿主授权

InitializeSuperUserAsync 的新增重载接收 grantAccess 和 hasAccess 两个委托。
grantAccess 使用同一作用域中的宿主服务为新账号授予角色或权限，
hasAccess 检查账号是否实际获得管理权限。创建和数据库内授权在同一事务中完成。
重复执行只验证已有账号的权限，遇到同名但无权限的账号会报错。
原有重载继续采用默认管理员 claim。
外部权限系统的副作用由宿主负责幂等和补偿。

可选 HTTP API 使用 MapPasswordResetAdministrationApi<TUser>()。
宿主显式 AddAntiforgery，并通过原生 AntiforgeryOptions 配置 HeaderName 和 Cookie。
端点要求登录，重置令牌生成进一步执行宿主操作策略，写请求校验防伪令牌，
所有响应设置 no-store。已有 Controller 的宿主可直接调用同一个功能服务。

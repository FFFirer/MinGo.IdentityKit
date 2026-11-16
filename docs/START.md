# 项目初始化提示词：ASP.NET Core Local Identity Integration

## 一、项目目标

创建一个基于 ASP.NET Core Identity 的本地身份认证整合包。

目标是为多个 ASP.NET Core 应用提供统一、开箱即用的本地身份认证能力，避免每个应用重复实现 Identity 注册、数据库配置、认证配置、用户初始化、登录流程和账户管理等基础架构代码。

本项目不是重新实现 ASP.NET Core Identity，也不是开发一套新的身份认证框架。

核心原则：

1. 优先使用 ASP.NET Core Identity 原生实体、接口、Manager 和服务。
2. 优先使用 ASP.NET Core Authentication 原生认证机制。
3. 优先使用 EF Core 原生持久化能力。
4. 仅补充原生能力之上缺失的集成、配置和通用业务功能。
5. 不重复定义已有的框架接口、实体和业务逻辑。
6. 通过一个整合包提供统一的注册和使用体验。
7. 支持多个应用复用同一套实现。
8. 支持应用内嵌和中心化两种部署形态。
9. 支持前后端分离和服务端渲染两种应用模式。
10. 第一版只实现本地身份认证，不提前实现其他身份和授权体系。

## 二、第一版功能范围

### 2.1 必须实现

- 本地用户管理。
- 用户注册。
- 可配置的注册开关。
- 用户登录。
- 用户登出。
- 当前登录用户信息查询。
- 密码修改。
- 密码重置所需的基础能力。
- 用户锁定及安全策略。
- 超级用户初始化。
- SQLite 持久化。
- Cookie Authentication。
- 前后端分离 HTTP API 模式。
- 服务端渲染模式。
- 应用内嵌部署。
- 中心化身份服务部署。
- 配置化集成。
- 默认安全策略。
- 基础测试和使用文档。

### 2.2 明确不在第一版实现

- 外部 IdP。
- OIDC 外部登录。
- OAuth 授权服务器。
- OpenIddict。
- Token Exchange。
- OBO。
- 跨应用授权。
- 动态 Permission。
- 自定义 RBAC。
- 组织与部门。
- 多租户。
- Feature Management。
- 身份管理 UI。
- 自定义身份认证协议。
- 独立的用户管理框架。

允许保留未来扩展点，但不提前实现相关实体、接口和业务逻辑。

## 三、技术基础

使用：

- ASP.NET Core Identity。
- ASP.NET Core Authentication。
- ASP.NET Core Authorization。
- Entity Framework Core。
- SQLite。
- ASP.NET Core Minimal API。
- ASP.NET Core MVC / Razor Pages 集成能力。
- Dependency Injection。
- Options Pattern。

优先使用以下原生组件：

- IdentityUser / IdentityRole（只有确实需要角色能力时才注册角色支持）。
- UserManager。
- SignInManager。
- IUserStore。
- IUserEmailStore（需要邮箱能力时）。
- IPasswordHasher。
- IPasswordValidator。
- IUserValidator。
- IdentityOptions。
- IdentityConstants。
- AuthenticationBuilder。
- CookieAuthenticationOptions。
- ClaimsPrincipal。
- IAuthorizationService。
- EF Core DbContext。
- IEntityTypeConfiguration。

不得为了统一接口重新封装 UserManager、SignInManager 等原生服务。

如需自定义用户实体，必须继承 IdentityUser 或其适当的泛型版本，并明确自定义字段的必要性。

## 四、两种部署模式

必须从第一版开始考虑两种部署形态。

### 4.1 Embedded Mode：应用内嵌模式

身份认证能力直接运行在业务应用内部。

结构：

```text
Business Application
    |
    +-- Local Identity Integration
    |       |
    |       +-- ASP.NET Core Identity
    |       +-- Authentication
    |       +-- User Management
    |       +-- EF Core
    |
    +-- Application Database
```

特点：

- 每个应用独立运行。
- 每个应用可以拥有自己的用户数据库。
- 用户管理与身份认证在应用内部完成。
- 不依赖外部身份服务。
- 可独立部署和升级。
- 不强制暴露 HTTP API。
- 支持服务端渲染直接调用 Identity 服务。

### 4.2 Centralized Mode：中心化身份服务模式

身份认证能力运行在独立的 Identity Host 中，其他应用通过 HTTP API 使用身份认证服务。

结构：

```text
Application A ----\
                   \
Application B ------> Identity Host
                   /       |
Application C ----/        |
                            +-- ASP.NET Core Identity
                            +-- Authentication
                            +-- User Management
                            +-- SQLite / EF Core
```

第一版的中心化模式只负责本地用户认证和用户管理。

不实现 OAuth/OIDC，也不实现跨应用 Token 交换。

必须明确：

- 中心化身份服务自身负责用户认证。
- 业务应用通过 HTTP API 调用中心化服务。
- 认证状态由中心化服务管理。
- 用户凭据不应复制到业务应用。
- 中心化服务与业务应用之间必须有明确的通信安全策略。
- 不允许直接通过共享数据库访问用户密码或 Identity 内部表。

中心化模式需要设计独立的身份服务 HTTP API 契约。

## 五、两种应用集成模式

部署模式和应用交互模式是两个独立维度。

必须支持以下组合。

### 5.1 Embedded + Server Rendering

适用：

- Razor Pages。
- MVC。
- Blazor Server。
- Blazor Web App Interactive Server。

特点：

- 身份认证运行在应用内部。
- 直接使用 SignInManager 和 UserManager。
- 使用 Cookie Authentication。
- 不要求暴露身份认证 HTTP API。
- 页面和服务端组件直接调用 Application Service 或原生 Identity 服务。

### 5.2 Embedded + HTTP API

适用：

- React。
- Vue。
- Blazor WebAssembly。
- 独立 SPA。
- 移动端或自定义客户端。

特点：

- 身份认证运行在应用内部。
- 暴露 HTTP API。
- API 直接使用 Identity 原生服务。
- 使用适合浏览器的安全 Cookie 会话方案。
- 支持跨域部署时的 CORS、Cookie 和 CSRF 安全配置。

### 5.3 Centralized + HTTP API

适用：

- 多个独立应用共享一个身份服务。
- 多个前端连接统一身份服务。
- 用户和账户统一管理。

特点：

- 独立 Identity Host。
- 暴露标准身份管理和认证 HTTP API。
- 业务应用不直接访问 Identity 数据库。
- 明确用户会话、Cookie 域、跨域和跨站部署的安全限制。

### 5.4 Centralized + Server Rendering

服务端渲染应用通过服务端调用中心化身份服务。

需要明确：

- 登录页面由哪个应用承载。
- 用户凭据提交给哪个服务。
- 中心化服务如何建立认证会话。
- 业务应用如何安全地维护自己的登录状态。
- 登出如何处理两个服务的会话。
- 不允许假设中心化服务的 Cookie 能自动成为其他域名应用的认证 Cookie。

如果需要跨应用统一 SSO，应在后续阶段引入标准 OIDC 协议，而不是在第一版通过共享 Cookie 或自定义 Token 机制模拟完整 SSO。

## 六、配置设计

整合包应提供统一 Options 配置。

建议配置项：

### IdentityOptions

- UserNameRequired。
- EmailRequired。
- RequireUniqueEmail。
- PasswordPolicy。
- LockoutPolicy。
- CookieOptions。
- SessionOptions。

优先映射 ASP.NET Core Identity 原生 Options，不重复设计已有配置。

### RegistrationOptions

- EnableRegistration。
- RequireConfirmedAccount。
- DefaultRegistrationEnabled。

注册默认策略应明确，不能因为没有配置而意外开放公开注册。

### SuperUserOptions

- EnableInitialization。
- UserName。
- Email。
- InitialPassword。
- RequirePasswordChange。
- Roles（只有启用角色支持时）。

超级用户初始化必须是显式配置，不得自动生成不可控的默认账户和固定密码。

### HostingOptions

- HostingMode：Embedded / Centralized。
- IntegrationMode：ServerRendering / HttpApi。
- EndpointPrefix。
- EnableEndpoints。
- EnableHealthChecks。

这些是 Toolkit 的集成配置，不要将已有 IdentityOptions 重复定义一遍。

## 七、注册与初始化

目标使用体验：

### 7.1 最简内嵌模式

```csharp
builder.Services.AddLocalIdentity(options =>
{
    options.Registration.Enabled = false;
})
.UseSqlite(connectionString);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
```

### 7.2 内嵌 + HTTP API

```csharp
builder.Services.AddLocalIdentity(options =>
{
    options.Registration.Enabled = true;
})
.UseSqlite(connectionString)
.AddHttpApi();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapLocalIdentityApi();

app.Run();
```

### 7.3 服务端渲染

```csharp
builder.Services.AddLocalIdentity(options =>
{
    options.Registration.Enabled = false;
})
.UseSqlite(connectionString)
.AddServerRendering();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
```

服务端渲染模式不应因为使用身份认证而强制映射 HTTP API。

### 7.4 超级用户初始化

```csharp
builder.Services.AddLocalIdentity(options =>
{
    options.SuperUser.Enabled = true;
    options.SuperUser.UserName = "admin";
    options.SuperUser.Email = "admin@example.local";
})
.UseSqlite(connectionString);
```

超级用户密码必须通过安全配置源提供，例如 Secret、环境变量或安全配置文件。

不得把生产环境初始密码硬编码到源码、默认配置或日志中。

初始化要求：

- 仅在显式启用时执行。
- 确保幂等。
- 不覆盖已有用户密码。
- 不覆盖已有账户的安全状态。
- 处理用户名和邮箱冲突。
- 输出明确初始化结果。
- 不在每次启动时重置超级用户密码。
- 超级用户初始化必须支持数据库首次创建后的安全执行。
- 如果启用角色支持，应确保管理员角色和超级用户关联正确。
- 记录必要的审计信息，但绝不记录密码。

以上为目标 API，最终 API 以实际架构验证为准。

## 八、注册关闭机制

注册功能必须能够独立控制。

当 EnableRegistration=false：

- 禁止公开注册 API。
- 服务端注册业务入口也必须拒绝注册。
- 不允许仅通过隐藏前端按钮实现关闭。
- 管理员仍然可以通过受保护的管理能力创建用户。
- 不影响已有用户登录。
- 不影响超级用户初始化。
- 注册状态应能通过配置控制。

错误响应应明确说明注册功能当前未开放。

## 九、HTTP API 设计

HTTP API 是可选功能。

仅在 AddHttpApi() 后注册对应 Endpoint。

建议初始 API：

```text
/api/identity/
    POST login
    POST logout
    GET me
    POST register
    POST password/change
    POST password/forgot
    POST password/reset

/api/identity/management/
    GET users
    GET users/{id}
    POST users
    PUT users/{id}
    DELETE users/{id}
    POST users/{id}/lock
    POST users/{id}/unlock
```

注意：

- 第一版可以根据实际范围裁剪管理 API。
- API DTO 不直接暴露 Identity 实体。
- 管理 API 需要授权保护。
- 登录失败不泄露账户是否存在等敏感信息。
- 注册关闭时返回明确的业务错误。
- 密码修改和重置必须使用 Identity 原生机制。
- 登录、登出和密码变更应处理 CSRF 和会话安全。
- API 应提供 OpenAPI 文档。
- API 应有统一错误响应格式。
- 不要因为存在 API 就自行设计一套不必要的 JWT 认证体系。

浏览器 SPA 优先评估 Cookie Authentication + CSRF 防护方案。

非浏览器客户端的认证需求应作为独立设计问题，不能将 Cookie 方案误认为通用 Token 认证方案。

## 十、服务端渲染模式

服务端渲染模式不暴露身份认证 HTTP API。

提供：

- 原生 UserManager。
- 原生 SignInManager。
- 可选的薄 Application Service。
- Cookie Authentication 配置。
- 当前用户获取。
- 受保护页面集成。
- 登录与登出业务服务。

不得强制使用默认登录 UI。

Razor Pages、MVC、Blazor Server 和 Blazor Web App 可以自行设计 UI，但底层身份认证逻辑保持一致。

如果某个框架组件已经能直接使用 Identity 原生服务，就不要增加重复封装。

## 十一、持久化设计

第一版优先使用 EF Core + SQLite。

要求：

- 使用 Identity 原生实体。
- 支持自定义 ApplicationUser。
- 支持可选 IdentityRole。
- 提供 SQLite Provider。
- 提供 Migration。
- 提供数据库初始化机制。
- 不使用 EnsureCreated 代替生产迁移。
- 支持已有宿主 DbContext 的集成。
- 明确 DbContext 生命周期。
- 明确事务策略。
- 明确迁移执行策略。

重点考虑：

- 内嵌模式可以使用业务应用现有数据库。
- 中心化模式拥有自己的身份数据库。
- 身份数据库不应被业务应用直接访问。
- 持久化实现不应该污染 Application 层。
- 后续可以增加其他数据库 Provider。

## 十二、项目结构

建议保持精简，不为了未来功能过度拆分。

```text
LocalIdentity.sln

src/
├── LocalIdentity/
│   ├── LocalIdentity.Core/
│   ├── LocalIdentity.EntityFrameworkCore/
│   ├── LocalIdentity.Sqlite/
│   └── LocalIdentity.HttpApi/
│
└── LocalIdentity.AspNetCore/
    └── LocalIdentity.AspNetCore.csproj

samples/
├── Embedded.ServerRendering/
├── Embedded.HttpApi/
└── Centralized.IdentityHost/

tests/
├── LocalIdentity.UnitTests/
├── LocalIdentity.IntegrationTests/
└── LocalIdentity.ArchitectureTests/

docs/
├── architecture.md
├── integration-modes.md
├── configuration.md
├── persistence.md
├── api.md
├── security.md
├── development.md
└── roadmap.md
```

项目拆分需要通过依赖关系验证后确定。

最终应提供一个方便应用引用的整合包，例如：

LocalIdentity.AspNetCore

它负责组合：

- Core。
- EF Core。
- SQLite（如果使用 SQLite）。
- 可选 HTTP API。

但不能让所有模式都被迫引入不需要的依赖。

如果需要多个整合包，应明确最简引用方式：

- LocalIdentity.AspNetCore：基础整合。
- LocalIdentity.AspNetCore.HttpApi：增加 HTTP API。
- LocalIdentity.EntityFrameworkCore.Sqlite：SQLite Provider。

具体包结构以实际依赖关系为准。

## 十三、M0 初始化任务

本次先完成项目架构、骨架和本地认证最小闭环。

### M0.1 原生能力调研

分析 ASP.NET Core Identity 原生注册、登录、Cookie、UserManager、SignInManager、EF Core Store 和 API 能力。

明确哪些直接使用，哪些由整合包补充。

### M0.2 集成架构设计

输出：

- 整合包边界。
- 模块和项目结构。
- DI 注册方案。
- Options 设计。
- Embedded / Centralized 部署方案。
- ServerRendering / HttpApi 集成方案。
- DbContext 组合方案。
- 超级用户初始化方案。

### M0.3 最小本地认证

实现：

- Identity 注册。
- SQLite Store。
- Cookie Authentication。
- 用户创建。
- 登录。
- 登出。
- 当前用户查询。
- 注册关闭机制。
- 超级用户初始化。

### M0.4 两种交互模式验证

建立：

1. Embedded.ServerRendering。
2. Embedded.HttpApi。

验证同一身份认证核心在两种模式下工作。

### M0.5 中心化模式设计与最小 Host

建立独立 Identity Host，提供 HTTP API，验证用户注册策略、登录和当前用户查询。

第一版不要求实现跨域 SSO 或 OAuth/OIDC。

### M0.6 测试

至少验证：

- 正常登录。
- 错误密码。
- 锁定用户。
- 注册开启。
- 注册关闭。
- 管理员初始化。
- 重复初始化。
- 初始化不覆盖已有密码。
- Cookie 登录状态。
- 登出后会话失效。
- 当前用户查询。
- 服务端渲染模式不映射 HTTP API。
- HTTP API 模式正常工作。
- SQLite Migration。
- 内嵌模式和中心化模式的独立性。

## 十四、后续里程碑

第一版完成后再按需求扩展：

### M1：本地认证完善

- 完整用户管理。
- 邮箱确认。
- 密码找回。
- 登录安全增强。
- 审计。
- 账户安全策略。

### M2：角色与授权

- Identity 原生 Roles。
- Claims 管理。
- 静态 Policy。
- 通用管理 API。

### M3：外部身份源

- OIDC。
- 多 IdP。
- 外部账户关联。
- 本地身份映射。

### M4：集中式身份服务增强

- 标准 OIDC/OAuth。
- OpenIddict。
- 多应用接入。
- SSO。

### M5：跨应用授权

- Client Credentials。
- Token Exchange。
- OBO。
- 服务身份。

上述规划只作为未来方向，不得提前实现。

## 十五、执行约束

1. 先调研原生 Identity 能力，再决定新增代码。
2. 优先提供最少的集成 API。
3. 不重新定义已有接口和实体。
4. 不为了统一而封装所有原生 Manager。
5. 不提前实现动态权限。
6. 不提前实现外部 IdP。
7. 不提前实现 OAuth/OIDC。
8. 不提前实现 UI。
9. 不强制所有应用暴露 HTTP API。
10. 不强制所有应用使用同一个数据库。
11. 不允许关闭注册仅依赖前端判断。
12. 超级用户初始化必须安全、幂等。
13. 不允许每次启动重置超级用户密码。
14. 不允许通过共享数据库实现中心化身份认证。
15. 不允许把 Cookie 共享误当成跨应用 SSO。
16. 不允许在未经测试时宣称功能完成。

## 十六、最终交付

本次任务最终提交：

1. 原生 ASP.NET Core Identity 能力分析。
2. 最终整合包设计。
3. Solution 和项目结构。
4. DI 注册与 Options API。
5. Embedded 模式实现。
6. Centralized 模式最小 Host。
7. Server Rendering 示例。
8. HTTP API 示例。
9. 注册开关实现。
10. 超级用户初始化实现。
11. SQLite 数据库迁移。
12. 完整自动化测试结果。
13. 快速开始文档。
14. 后续里程碑规划。

最终验收标准：

一个新的 ASP.NET Core 应用只需引用整合包、配置 SQLite 和少量选项，就能完成本地身份认证集成。

开发者可以根据项目形态选择服务端渲染或 HTTP API，也可以选择身份能力内嵌或由中心化 Identity Host 提供。

所有身份认证逻辑仍然基于 ASP.NET Core Identity 原生能力，整合包负责消除重复的架构配置和通用业务实现。

**第一版只做好本地身份认证整合，不为尚未实现的未来功能引入额外架构复杂度。**
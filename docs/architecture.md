# 架构与部署

## 原生能力与整合边界

- `AddIdentityCore` 注册原生用户校验、密码哈希和 `UserManager`。`AddSignInManager` 处理登录、账户锁定检查和会话建立，`AddIdentityCookies` 负责 Cookie 方案和安全戳验证。原生 token providers 提供密码重置令牌基础能力。[Identity 文档](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- `AddEntityFrameworkStores` 连接 EF Core 原生存储。默认 SQLite 上下文使用 `IdentityUserContext<IdentityUser>`，每个请求使用 scoped DbContext。自定义用户可使用 `AddIdentityKit<ApplicationUser>()`，再通过 `AddEntityFrameworkStores<HostDbContext>()` 接入宿主上下文，迁移由宿主负责。[Identity 模型与迁移](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)
- ASP.NET Core 的 `MapIdentityApi` 提供自带的接口与 Cookie、Bearer 登录选项。M0 的 HTTP 扩展提供独立契约，使用原生 Manager 应用公开注册开关、Cookie 和防伪策略。[Identity API 文档](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)

项目按依赖逐层组合。

- `MinGo.IdentityKit` 提供原生 Identity 服务注册、Cookie 与公开注册策略。
- `MinGo.IdentityKit.EntityFrameworkCore` 引用核心包，加入原生 EF Core Store 依赖及事务化超级用户初始化。
- `MinGo.IdentityKit.EntityFrameworkCore.Sqlite` 引用 EF Core 包，加入 SQLite Provider、默认上下文及初始迁移。
- `MinGo.IdentityKit.HttpApi` 引用核心包，加入可选路由、防伪验证和 OpenAPI，可与宿主选择的 Store 组合。

密码、锁定、身份验证和 Cookie 设置通过原生 Options 调整。

## 数据库与管理员

示例在启动时执行 `MigrateAsync`，随后在独立作用域初始化超级用户。部署时先完成迁移和初始化再开放流量。自定义上下文在宿主迁移完成后调用 `InitializeSuperUserAsync<TUser, TContext>()`，并确保事务使用的上下文就是 Identity Store 使用的上下文。

超级用户创建和管理员声明在同一事务提交。重复启动保留现有账户密码和安全状态；普通账户占用目标用户名或邮箱时，初始化以明确错误终止。宿主可用 `IdentityKitAdministrator` 策略保护自行实现的管理操作。

## 集成组合

- 内嵌 + 服务端渲染。宿主拥有数据库、页面和 Cookie。Razor Pages 和 MVC 的登录使用普通 HTTP POST；交互式 Blazor 可跳转到宿主的 Razor Page 执行写 Cookie 的登录操作。
- 内嵌 + HTTP API。业务宿主映射路由，浏览器以同源方式调用。
- 中心化 + HTTP API。独立 Identity Host 拥有数据库与 Cookie。业务前端通过 HTTPS 请求身份服务，可在每个业务域以同源反向代理暴露身份路由。代理仅向可信 Identity Host 转发凭据和 Cookie。
- 中心化 + 服务端渲染。业务应用承载登录页，经 HTTPS 调用 Identity Host。要维护本地登录状态，业务应用可在服务端会话存储中按用户保管上游 Cookie，签发自己的 Cookie 并在登出时调用上游登出后清除本地会话。M0 提供 HTTP 契约与设计说明，此组合的客户端实现留给接入方。

跨源浏览器部署需显式允许具体 Origin 和凭据，前端使用 `credentials: "include"`。跨站时还需评估 Cookie SameSite 设置、第三方 Cookie 限制和防伪 Cookie 的发送条件。示例默认使用 HTTPS、同源请求和 Lax 登录 Cookie。

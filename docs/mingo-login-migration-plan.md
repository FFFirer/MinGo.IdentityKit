# MinGo 应用登录替换评估与交付计划

盘点日期为 2026-11-17。范围为 IdentityKit 同级目录下 35 个本地 `MinGo.*` 目录，含大小写变体。依据生产入口、登录调用、授权策略、存储接线与前端契约进行静态评估。Demo 和归档源码用于辨别入口，验收以实际运行宿主为准。AdminTemplates 和 OpenDashboard 在本次扫描范围内没有可分析源码。Quartz 的认证中间件处于注释状态，QuartManager 等的 Agent 身份与 AppliationPortal 的 Portainer 客户端认证不计入人类登录迁移。GitOps 中的 Identity 部署配置作为集中式切换依赖处理。

## 当前能力与替换边界

IdentityKit 的 M0 提供原生 UserManager、SignInManager、Identity Cookie、锁定策略、注册开关、初始化管理员、默认 SQLite 迁移和可选 HTTP API。API 包含 csrf、login、logout、me、register，登录输入为用户名，成功返回 204，me 只返回 ID、用户名、邮箱。默认 SQLite 使用无角色的 IdentityUserContext，自定义用户由宿主提供 DbContext。管理员初始化写入专用声明，目标应用常用的 Admin 角色需要独立配置。密码修改和找回有原生 token provider 作为基础，完整入口与投递流程待补。

独立身份宿主目前只建立自身 Cookie 会话。各应用替换优先采用内嵌模式复用宿主的数据库、实体和业务授权。CertManager 的外部登录迁移和 MinGo.Identity 的发行方替换分别交付。角色、项目成员关系、资源所有权、服务 API Key 与 SDK 访问均按现有边界接入。

## 项目矩阵

| 项目 | 最小验收闭环 | 依赖、实现偏差与切入点 |
| --- | --- | --- |
| ReverseProxy | 邮箱登录、刷新控制台恢复会话、调用受保护控制面 API、登出后 401 | 低至中。已有 IdentityUser、IdentityRole、SQLite 和 Cookie。保留 AuthController 的邮箱、DTO 与已授权注册行为，先替换 Program 的 DI。校验控制面和数据面凭据隔离。 |
| reverseproxy2 | 同上，并在实际使用的旧 Blazor 入口验证认证状态 | 低至中。控制面同为 Identity 和 SQLite，但注册契约及另一个 Blazor AuthService 需各自核对。复用 ReverseProxy 的适配方式，分别测试，归档宿主不作验收入口。 |
| WebShell | 邮箱密码登录、刷新后 Blazor SSR/WASM 状态一致、进入受保护页面、登出；使用已有 2FA 的账户仍走原流程 | 低。已有 AppUser、Npgsql、Account Razor 页面、重验证与状态序列化。保留宿主 DbContext、Account 页面、邮件发送和已启用的账户操作；先换 DI。 |
| AdminStartup | 已确认的管理员登录，处理用户和角色并进入菜单；普通用户正确受限 | 中。已有 IdentityUser、IdentityRole、AppDbContext、AdminOnly 与 Blazor 状态传递。保留账号确认要求、宿主数据库接线、角色管理和菜单授权。新账号获取确认的路径计入验收。 |
| AIWorkbench | 邮箱登录、恢复用户及 Admin/Member 角色、访问有成员资格的项目、管理员改角色或重置密码 | 中。保留 ApplicationUser、SQLite、两套表单/API 入口、项目成员关系和动态注册设置。导入现有 Admin；新库由显式初始化建立首管，注册固定授予 Member，避免首用户竞争，迁移时保持既有 ID。 |
| StaticPages | 后台登录、按现有角色进入项目和存储管理、登出；独立 API Key 仍可授权 | 中。现为自有用户/哈希、Cookie 与 API Key scheme。迁移账号和角色，旧哈希兼容后升级或走重置；保留页面和 JSON 登录入口，验证鉴权 scheme 转发。 |
| LightConfig | 后台登录、会话过期处理、读写配置、登出；SDK 继续以 API Key/AppId 换取其专用 token | 中。已有 IdentityUser/SQLite，人用 WebUiJwt 和轮换 refresh cookie，另有 SdkJwt。先换账户注册和密码校验接线，保留现有 WebUI token 契约；若切同源 Cookie，须联合变更前端、后台策略、CSRF 与会话续期并验证 SDK scheme 隔离。 |
| CPS | 注册、登录、读写本人资料、访问本人订单/推广/佣金、退出 | 高。SQL Server、自有用户、PBKDF2 哈希格式、自有 JWT middleware、业务依赖 Guid。保留业务用户 ID/资料，验证旧哈希后升级或重置。迁移初期保持 token 契约；替换 middleware 前先验证跨用户拒绝。 |
| AgentsPlayground | 创建实际持久化账户或受控建号、登录、刷新后用户 ID 一致、按 Admin/Reviewer/User 进入对应功能 | 中。PostgreSQL 现存业务库，当前 AuthService 有演示口令、临时 Guid 和 JWT。用 Identity Store 替换演示逻辑，先保留前端 token/role 契约；选择稳定 Guid 或显式映射并核对业务引用。 |
| CertManager | 本地与已启用的 GitHub、Google、SimpleIdServer 登录，回调后建立本地会话并保留关联和角色，访问现有权限内的证书；服务 API Key 回归 | 高。SQLite、ApplicationUser、自定义 UserManager、外部登录临时 Cookie 与角色分配。先替换本地 DI，保留 providers 与回调。共用关联逻辑时按 provider/key 查找，邮箱关联验证所有权，覆盖首次回调、重复回调、无邮箱和锁定。 |
| Identity | 若替代现有发行方，至少真实客户端发现、授权码登录、回调、业务 API 验签与登出 | 协议级。SimpleIdServer、PostgreSQL、Admin 客户端、realm、迁移与 SCIM 配置。先导出现用客户端、issuer、claims/scopes、回调、密钥和实际启用的协议，再用成熟 OIDC 服务端接 IdentityKit 账户。现有发行方继续服务直到客户端及运维依赖通过切换验收。 |

源码复核入口使用同级仓库相对路径。IdentityKit 的基线见 [IdentityKit.cs](../src/MinGo.IdentityKit/IdentityKit.cs) 与 [IdentityApi.cs](../src/MinGo.IdentityKit.HttpApi/IdentityApi.cs)。各项目入口如下。

- [ReverseProxy](../../MinGo.ReverseProxy/src/MinGo.ControlPlane.Api/Controllers/AuthController.cs) · [reverseproxy2](../../mingo.reverseproxy2/src/MinGo.ControlPlane.Api/Controllers/AuthController.cs) · [WebShell](../../MinGo.WebShell/src/WebHost/Program.cs)
- [AdminStartup](../../MinGo.AdminStartup/src/MinGo.AdminStartup.Web/Program.cs) · [AIWorkbench](../../MinGo.AIWorkbench/src/MinGo.AIWorkbench.Web/Endpoints/AccountEndpoints.cs) · [AIWorkbench 用户管理](../../MinGo.AIWorkbench/src/MinGo.AIWorkbench.Web/Endpoints/UserManagementEndpoints.cs)
- [StaticPages](../../MinGo.StaticPages/src/MinGo.StaticPages.Web/Program.cs) · [LightConfig](../../MinGo.LightConfig/src/MinGo.LightConfig.Server/Extensions/EndpointMappingExtensions.cs) · [CPS](../../MinGo.CPS/src/MinGo.CPS.Api/Controllers/AuthController.cs)
- [AgentsPlayground](../../MinGo.AgentsPlayground/src/backend/src/MinGo.AgentsPlayground.Core/Services/AuthService.cs) · [CertManager](../../MinGo.CertManager/src/MinGo.CertManager.Web/Pages/ExternalLogin.cshtml.cs) · [Identity](../../MinGo.Identity/src/MinGo.IdServer.Admin/Program.cs)

## 数据库迁移策略

- **已有数据的应用**：先记录源数据库、迁移版本与部署版本，再制作数据库引擎支持的一致备份并验证可恢复。以该备份恢复到独立的新数据库或新文件，使用独立连接配置，所有迁移、验收和切换后的读写均落在新库。保留原库和备份供回退。若源库在演练期间仍在写入，正式切换前暂停写入并重新制作最终备份，或验证增量同步完整后再切换。
- **从零开始的应用**：创建独立的空库，由目标宿主的迁移建立结构，再通过显式配置和 Secret 初始化首个账号。纯 IdentityKit 宿主可使用默认 SQLite；已有业务模型的宿主由自身的 DbContext 和迁移创建业务与身份表。
- **切换与回退**：演练通过后更新连接配置，验证新业务写入只出现在新库。开放新库写入前可以恢复旧版本与旧连接；开放写入后，回退前须先暂停写入并核对如何保留或回放新库增量。

### ReverseProxy

已有实例使用 SQLite 在线备份或停写后的一致备份，将完整业务库恢复为独立的新文件，例如 `gateway.identitykit.db`。SQLite 处于 WAL 模式时，单独复制主文件可能遗漏已提交事务。目标宿主通过 `ConnectionStrings:DefaultConnection` 指向新文件，继续使用 `AppDbContext`、原有表映射和 EF 迁移历史。备份覆盖身份及路由、集群、证书等控制面数据。恢复后核对 SQLite 完整性、外键、表结构、迁移历史和业务记录数，再用旧账号登录并验证用户 ID、查询旧路由、执行一个受保护的写操作及登出。验证写操作只落在新文件。

全新实例将同一连接配置指向空文件，由 ReverseProxy 的业务迁移建立表结构，再显式创建首个账号。验收建号、登录、业务读写、重启后持久化和登出。两条路径都保留现有邮箱登录、Cookie 和前端 API 契约，分别验证已签发 Cookie 与新会话的行为。

## 开发顺序与完成条件

1. **内嵌接线与真实样板。** 增加可选原生角色接线，明确默认 Cookie scheme 与宿主 scheme 的组合、宿主已有 DbContext 接入、初始管理员角色的显式映射。用 ReverseProxy 和 WebShell 跑通 HTTP API、Blazor 两种真实宿主。保留旧路由和 DTO 作为宿主适配，邮箱登录和 RememberMe 只在使用方接通后提炼。对写接口验证 CSRF，对页面跳转验证本地 return URL。通过后复制到 reverseproxy2。
2. **账号管理与角色。** 接入 AdminStartup、AIWorkbench，补齐已登录改密、管理员受保护重置或建号，以及角色和当前用户契约。邮件确认和自助恢复沿用宿主发送方，在存在实际调用者时补全共用流程。变更角色或密码后验证会话更新，验证注册关闭仍允许管理员建号。项目成员与菜单策略在宿主测试。
3. **旧账号与会话迁移。** 优先让 AgentsPlayground 用真实账号取代演示入口，再做 StaticPages、LightConfig、CPS。每个迁移用现有结构的副本核对用户 ID、密码哈希、角色及业务引用；可验证旧哈希的账号登录后升级，不兼容账号走重置。先替换账号和密码验证，按客户端证据决定是否切换 token 契约。若引入 Cookie，所有依赖该 Cookie 的业务写操作要同时纳入 CSRF 验证。退出后对选定会话策略验证失效行为。
4. **外部登录。** 完成 CertManager 本地 DI 后，逐个用已配置 provider 验证真实回调。只有回调、账号关联、安全策略在多个宿主确有重复时才提炼共用代码。验收登录成功后能看自己的证书且服务凭据认证依然通过。
5. **集中身份切换。** 以 CertManager 的 SimpleIdServer 登录作为首个真实客户端。核实使用中的 discovery、issuer/realm、签名密钥轮换、scope、claims、退出和相关后台操作，逐项验证。记录各客户端改配及回退顺序，先并行验证再切换；缺少等价能力的旧客户端继续使用原发行方。SCIM 或其他协议在确认有实际依赖时纳入该迁移的验收条件。

## 逐应用验收记录

每个应用记录适用的空库启动或旧库恢复迁移。两种形态均支持时，分别验收。用真实前端或实际客户端完成建号或初始化 → 登录 → 刷新或重启应用后恢复约定状态 → 完成一个受保护业务操作 → 登出或到期后拒绝访问。额外记录错误密码或锁定、普通账户越权、业务用户 ID 稳定，以及该应用特有的 SDK、API Key 或外部回调回归。若迁移改变 Cookie、token 或数据库，记录预期重新登录行为和可操作的回退步骤，并实际演练。只有具备实际宿主验证记录的项目才算闭环完成。

本次交付为静态盘点与开发计划。应用运行、实际数据库与部署客户端清单仍需在各阶段验证。

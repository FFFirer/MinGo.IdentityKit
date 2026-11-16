# HTTP API

默认前缀为 `/api/identity`。示例提供 `/openapi/v1.json`。

| 方法 | 路径 | 结果 |
| --- | --- | --- |
| GET | /csrf | 200，返回 `requestToken` 并设置防伪 Cookie |
| POST | /register | 204，校验失败为 400，注册关闭为 403 |
| POST | /login | 204 并设置会话 Cookie，登录失败为 401 |
| GET | /me | 200，返回 `id`、`userName`、`email`，无有效会话为 401 |
| POST | /logout | 204 并清除 Cookie，无有效会话为 401 |

注册 JSON 包含 `userName`、`password` 和可选的 `email`。登录 JSON 包含 `userName` 和 `password`。写请求先获取防伪令牌，同时发送防伪 Cookie 和 `X-CSRF-TOKEN`。登录后以当前会话重新获取令牌，再调用登出。

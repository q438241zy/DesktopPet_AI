# 八家模型接口核对

2026-10-07；适用于 CompanionV01 HTML Demo 和原生 v0.4。所有角色都能使用所选服务；角色性格由独立 persona 提示定义，不强制角色和服务一一绑定。

| 服务 | 默认基础地址 | 请求及文字回包 | 官方依据 |
| --- | --- | --- | --- |
| GPT / OpenAI | `https://api.openai.com/v1` | Bearer；`/chat/completions`；developer + user/assistant；`choices[0].message.content` | [Chat Completions](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create) |
| Claude / Anthropic | `https://api.anthropic.com/v1` | `x-api-key`、`anthropic-version: 2023-06-01`；`/messages`；顶层 system、max_tokens；提取 content 中 text 块 | [Messages](https://platform.claude.com/docs/en/api/messages/create)、[鉴权与工作区](https://platform.claude.com/docs/en/manage-claude/authentication) |
| Gemini / Google | `https://generativelanguage.googleapis.com/v1beta` | `x-goog-api-key`；`/models/{model}:generateContent`；systemInstruction、contents 的 user/model；candidates 的文本 parts | [generateContent](https://ai.google.dev/api/generate-content)、[鉴权](https://ai.google.dev/api) |
| Grok / xAI | `https://api.x.ai/v1` | Bearer；`/chat/completions`；system + user/assistant；choices 文字 | [Chat Completions](https://docs.x.ai/developers/model-capabilities/legacy/chat-completions) |
| DeepSeek | `https://api.deepseek.com` | Bearer；`/chat/completions`，也接受用户填写 `/v1`；system + user/assistant；choices 文字 | [首次调用](https://api-docs.deepseek.com/) |
| Qwen / 阿里云百炼 | `https://dashscope.aliyuncs.com/compatible-mode/v1` | Bearer；`/chat/completions`；system + user/assistant；choices 文字 | [OpenAI 兼容接口](https://help.aliyun.com/zh/model-studio/compatibility-of-openai-with-dashscope) |
| GLM / 智谱 | `https://open.bigmodel.cn/api/paas/v4` | Bearer；`/chat/completions`；system + user/assistant；choices 文字 | [对话补全](https://docs.bigmodel.cn/api-reference/模型-api/对话补全) |
| Kimi / Moonshot | `https://api.moonshot.ai/v1` | Bearer；`/chat/completions`；system + user/assistant；choices 文字 | [Quickstart](https://platform.kimi.ai/docs/overview) |

模型名称可编辑，除沿用已存在的 DeepSeek 默认名外，不自动替用户选择计费模型；其他厂商的型号示例只作为输入占位提示，实际可用性取决于用户账户。Qwen 旧域名在官方文档中仍可用，同时接受各地域及业务空间专属域名；Key 必须匹配地域。Kimi 可填写对应账户使用的服务地址，包含 `.cn` 或 `.ai`。不跟随 HTTP 重定向携带密钥。

Claude 可填写多工作区密钥需要的 Workspace ID；网页直连声明与 [Anthropic 官方 TypeScript SDK](https://github.com/anthropics/anthropic-sdk-typescript/blob/main/src/client.ts) 一致。此声明不能解决其他服务的 CORS 或网络限制。

API Key 不进入本机存储、URL、请求正文或报告。切换服务及编辑地址时清空输入；退出账号或刷新页面或重启原生程序后清除会话密钥。基础地址或完整端点仅补齐一次，拒绝错用 Responses/Messages/generateContent 路径；Gemini 完整端点的模型须与模型输入一致。历史只保留 user/assistant 文本并合并连续同角色消息。回包不展示 Gemini thought 或 Claude thinking 块。

工作模式由每个角色六条预设提醒起步，不要求用户撰写；配置 API 且本次会话已填写密钥时尝试生成一句提醒。这个请求只发送角色性格、专注间隔及提醒主题，不附带聊天历史。模型失败继续显示明确标注的预设提醒，避免丢失定时提醒；关闭开关、关闭提醒、换角色、暂停陪伴或切换接口会取消未返回请求。

验证使用 Node 的协议断言和 Chromium 的实际表单/聊天/测试连接，HTTP 请求被测试路由截获，核对八家端点、字段、认证头和回包解析。**没有八家真实密钥，未验证真实账号权限、额度、跨域、网络及服务响应；不声称八家线上全部连通。** 用户可通过“测试连接”用自己的配置执行真实请求。原生适配在 CompanionProviders.cs；核心检查用内存 HttpMessageHandler 验证八家请求和响应，不发送真实网络请求。

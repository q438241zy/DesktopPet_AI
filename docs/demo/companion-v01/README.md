# 云朵伙伴 v0.1 Demo

2026-10-07，14 项功能和界面调整的第一轮 HTML 审阅。尚未接入日常原生程序；本页使用独立 Demo 存档，不会改动用户的宠物、账号、服装或收藏。原生日常程序仍为已发布的 Preview28。

## 打开与审阅

部署后打开 `Release/win-x64/Demo/CompanionV01/index.html`。桌面原有 `DeepSeek-動作Demo.lnk` 仍指向 CloudClub，顶部新增本页入口。

- 我的伙伴：选择八位角色，直接在人物上方聊天。右键角色卡片进入档案；“伙伴档案”按钮与 Shift+F10 提供等效入口。
- 档案：我们的默契、风格预览、聊天性格三个分页。每个角色分别记录 −100 至 +100 的关系值和可见页面上的陪伴时间；Q版与3D真人共用该角色的记录。原装/运动服/泳装/婚纱可预览、切换。
- 躲藏：默认闲置一分钟后触发，或点击“躲藏”；先走向随机边缘，再留在边缘等左键找到。右键只切换菜单，不返回。正在输入草稿、打开分页或对话框时不自动躲藏。
- 设定：工作提醒间隔 1–180 分钟，默认 10 分钟；支持自定义提醒内容、试提醒和停止。每轮到点以当前人物的笑脸与语气提醒，隐藏中通过独立提醒卡出现，不把躲藏人物拉回。暂停陪伴会暂停倒计时；恢复后继续剩余时间。浏览器关闭不提供后台服务。
- 合照：邀请其他任一角色，沿用当前风格和各自所选服装；输入最多 60 字，选择暖云/浅玉/奶油相框，生成 1200×1400 PNG。保存时优先调用浏览器保存选择器并定位桌面；不支持此能力的浏览器只能下载，页面会说明实际结果。取消保存不会产生成功提示。
- 陪伴日常：本地日期每日一次打卡、当周记录、连续/累计天数；球区、食物区、故事区。20 种随机礼物沿用项目原始清单，非食物日常物品保存在“其他小物”。三篇 7–8 句原创故事读到最后并确认后收藏。
- 会员中心：登录、注册分页；本机 Demo 账号，随机盐与 PBKDF2 摘要，密码不存明文，刷新后重新登录。未提供云端账号服务。
- 顶部 Demo 按钮提供“模拟闲置一分钟”“推进一个提醒周期”和当前角色关系值滑杆。只改变测试存档，不冒充真实陪伴时长。

## 待审规则

八位角色的口吻定义保存在 `personas.js`，同一份定义同时用于本机多轮对白和 API 提示词。每次回复至少思考一秒；慢接口持续等待到回复或 45 秒超时，错误不伪装成本机成功回复。切换角色、风格、接口或退出账号会取消旧请求。

好感暂按打卡 +3、有效聊天 +1、摸头 +1、共读 +2、礼物 +2、安静陪伴五分钟 +1 试行。正向每天上限 20，有交互冷却；十秒内连续摸头六次触发温和边界与 −2，最低 −100，最高 +100。漏签和关闭程序不扣分。服装门槛暂提议原装 0、运动服 20、泳装 50、婚纱 80，未达成显示笑脸小锁；**测试期所有风格仍开放**。这些数值属于本轮方案，未经用户拍板不得强制用于正式解锁。

工作模式、人物关系和合照是浏览器中可操作的设计；原生后台调度、桌面默认保存、存档迁移与新 UI 接入须在本轮批准后完成。角色工房在本方案中彻底移除；原生工房及发布内容的删除留给对应接入阶段，不能宣称运行中的程序已删除。

## 接口检查

目前原生 `CompanionChat.cs` 已使用 POST、Bearer、`model/messages/stream:false` 和 `choices[0].message.content`；填写 OpenAI `/v1` 或完整端点可用，但只填写 OpenAI 根地址时缺 `/v1`。Demo 补齐该识别，接受根地址、`/v1` 和完整端点，避免重复拼接。DeepSeek 使用兼容的 Chat Completions 格式；OpenAI 提示用 developer，DeepSeek 用 system。API Key 只留在当前页面内存，不写本机存储。

- [OpenAI Chat Completions 官方参考](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create)：`https://api.openai.com/v1/chat/completions`。
- [DeepSeek 官方入门](https://api-docs.deepseek.com/)：`https://api.deepseek.com/chat/completions`，也接受 `/v1` 基础地址。2026-10-07 页面示例模型为 `deepseek-flash`；用户可改成账号可用的其他模型。

自动检查截获请求并返回模拟回复，验证端点、消息角色、认证头、内容解析与思考时长；没有调用付费模型，也没有声称实际 API Key 已验证。浏览器直接访问用户填写的接口可能受 CORS 限制，错误有明确反馈；原生接入阶段继续使用现有 HttpClient。

## 设计参考

打卡参考 [Streaks 官方介绍](https://streaksapp.com/) 的简短任务反馈、日期记录与连续天数；其 [App Store 页面](https://apps.apple.com/us/app/streaks/id963034692) 在查询时显示 4.8/5。参考 [Finch App Store 页面](https://apps.apple.com/us/app/finch-self-care-pet/id1528595748) 的简短日常打卡与伙伴陪伴反馈，查询时 4.9/5。采用交互原则，不复制两者的界面素材、图标或文案。

## 构建与验证

```powershell
python tools/build-companion-demo.py
./tools/install-companion-demo.ps1
node tools/verify-companion-demo.cjs Release/win-x64/Demo/CompanionV01/index.html
node tools/verify-companion-boundaries.cjs
```

`data.js` 和 `.generated/photos` 由现有角色清单、原始 PNG/WebP 与 `Version.props` 生成，不提交重复的图像编码。所有图片像素不变；透明边界测量只生成定位与固定缩放元数据。合照按需加载原图编码，解决 file 页面画布无法导出的问题。合照图片缓存最多 8 张，其余预览最多 48 张，退出或刷新释放。

验证脚本使用真正 Chromium file 页面、鼠标点击、键盘输入和 PNG 导出；计时长周期通过 Demo 时钟推进检查。截图与报告输出到 `.artifacts/companion-v01-review`，检查前后核对日常 exe 和 state 文件哈希。

# v1.9 控制面板 Demo

2026-10-11。本轮按「先 HTML Demo、拍板后拓展」进行。v1.9 是待审预览，日常 Windows 程序仍为已批准的 v1.8。未运行原生发布、未修改用户存档或桌面快捷方式，未制作正式安装包，Gitee 继续暂停。

## 本轮变化

1. **我的伙伴**：八张卡各自随机播放站立、坐下、托腮、伸展、笑脸，随机间隔 1–2 秒。图库预览与当前伙伴的固定姿态、互动和草稿解耦；暂停预览、全局暂停、减少动态、页面不可见及模态框仍会停止。恢复后重新计时，不追赶遗漏动作。
2. **行事历**：签到与日程合到同一张月／周历；今日突出显示，已签到及补签有标记。点击今天签到，重复点击只查看当日事项；过去漏签仍须有补签卡并确认。右侧展示所选日的事项，未来日期可以浏览。日程整理放在「记一件事」折叠入口，原 API 确认、重复、延后和补提醒规则保留。
3. **陪伴日常**：动作／图鉴收集两页。动作包含 15 项玩耍和 12 项互动，点击使用当前角色及服装进入原动作流程。图鉴保留 10 种球、6 种食物、4 件小物和10篇故事；已读故事可重读。没有凭空授予收藏或补签卡。
4. **设定**：陪伴与提醒／AI 接口／账号与会员三分页；不再有独立会员侧栏。账号仍为本机 Demo 登录注册，PBKDF2 摘要，不增加云端会员承诺。八家 API 原协议保留，Key 只在当前会话内存中。
5. **云朵 Logo**：原创透明 SVG 云朵宠物，奶油白云团、耳朵、手脚、粉色脸颊和胸口爱心，另有小尺寸简化稿。仅替换 Demo 品牌与 favicon，未替换 Windows 图标。

角色卡问题并非缺图。旧版在自动模式下 64 套均能切换，但固定姿态锁住图库卡；原生还把预览时钟放在桌面宠物可见时的 Tick 中。此轮只在 Demo 解耦图库时钟。后续获得批准后，原生须另设控制面板预览时钟，不得把 Demo 结果当作原生已修复。

没有生成新人物、拉伸身体或改动 PNG；使用本套服装原帧与共同基线。五种待机属于已有姿态轮换，未补全连续起身或坐下动画。

## 参考与取舍

- [TickTick 官方：在日历中展示习惯打卡](https://help.ticktick.com/articles/7055781805944733696)：采用日期承载打卡与日程的方式，保留月／周视图，避免两个日历。
- [Todoist 官方：设定中的订阅分页](https://www.todoist.com/help/account-and-billing/plans/set-up-a-todoist-pro-subscription-PLxdCDaH9)：借鉴账号相关设定集中分区；本项目不增加订阅购买或未经批准的收费限制。
- [TickTick 美国 App Store 评价页](https://apps.apple.com/us/app/ticktick-to-do-list-calendar/id626144601?platform=iphone&see-all=reviews)：作为本次高评价产品的筛选参考，2026-10-11 查阅。具体视觉为本项目原创实现。

## 验证

全部浏览器使用隔离上下文和测试存档，只截 Demo 页面。API 测试拦截请求，0 次真实或付费 API 调用。

| 范围 | 结果 | 证据 |
| --- | --- | --- |
| 角色卡真实时间 | 12 组，64 套全部五姿态，每套 11–18 种实际像素状态；固定偏好、草稿、点击、暂停恢复及减少动态通过 | `.artifacts/companion-v19-cards/final/` |
| 统一日历 | 模块9组、完整页面9组；打卡去重、补签取消/确认、重开保存、月周、跨年、AI分页与手机布局通过 | `.artifacts/companion-v19-calendar/` |
| 页面整合 | 5组；27个动作、收藏与故事回读、旧存档保留、会员注册登录、键盘分页、320/390/1280宽度通过 | `.artifacts/companion-v19-layout/` |
| 原日程回归 | 10组；8家接口合同、确认后保存、取消/过期、重复和延后提醒、重开恢复通过 | `.artifacts/companion-v19-agenda/` |
| 模型与工作提醒 | 15组；8家协议模拟、1秒思考、工作开关、失败提示、取消迟到回复、Key不持久化通过 | `.artifacts/companion-v19-providers/` |
| 原玩耍回归 | 11组；64外观×礼物/阅读/追蝶共192组合、互动输入、取消及自然移动通过 | `.artifacts/companion-v19-play/` |
| UI目视 | 1280×900与390×844共14状态；修正击掌/猜拳图标映射与选中图标可见性后定向复核4图通过 | `.artifacts/companion-v19-ui/` |

真实时间采样得到442个完整换姿间隔，约0.965–2.071秒；调度器范围1000–2000ms，采样与浏览器调度有抖动，未使用加速时钟冒充自然播放。图库自动换姿不刷新用户闲置时间。

日常 EXE SHA256 前后相同：`8C920071F07CE12205D99070ABC0BA4DF5BBC07C494D78AE03A1383EA3004D96`。

```powershell
node tools/deploy-companion-preview.cjs
$env:CARD_IDLE_OUTPUT='.artifacts/companion-v19-cards/final'
node tools/verify-card-idle-v19.cjs --expect-independent
node tools/verify-unified-calendar-v19.cjs
node tools/verify-companion-layout-v19.cjs
$env:AGENDA_REVIEW_DIRECTORY='.artifacts/companion-v19-agenda'
node tools/verify-pet-agenda.cjs
$env:COMPANION_REVIEW_DIRECTORY='.artifacts/companion-v19-providers'
node tools/verify-companion-providers.cjs
$env:PLAY_REVIEW_DIRECTORY='.artifacts/companion-v19-play'
node tools/verify-play-preview.cjs
```

首次旧测试仍直接操作已移入隐藏分页的控件而超时；适配新导航后复测。补签夹具也改为隔离上下文初始化时注入，避免被旧页卸载存档覆盖。这些测试修订不涉及用户数据。

## 入口与后续

`Release/win-x64/Demo/CompanionV01/index.html?review=layout-v19`。原桌面 `DeepSeek-動作Demo.lnk` 所到 CloudClub 顶部入口已更新为 v1.9 待审；原 `云朵伙伴.lnk` 仍运行 v1.8。

获准后再把图库独立时钟、页面分区、统一日历和 Logo 接入 Windows，并实际通过原桌面入口验证；不把本次 HTML 检查作为该步骤已完成的证据。

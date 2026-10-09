# v1.3 待机节奏与闲置躲藏复核

2026-10-09。本轮按既定“新行为先 HTML Demo”规则交付 30 秒待机切换，同时审计既有原生自动躲藏。源码与 Demo 为 v1.3；日常桌面程序仍为 v1.2，未替换执行文件，未生成正式安装包。

## Demo

入口仍为 `Release/win-x64/Demo/CompanionV01/index.html`。在“我的伙伴”当前角色卡选择“自动 · 30秒”，待机满 30 秒后切换站立/坐下。固定站立、固定坐下仍可选，角色偏好保留。

- 使用既有服装原图，不重生成、不拉伸、不改变角色比例；没有新增连续起身/落座图集。
- 用户操作重新开始该角色的待机周期。宠物自动换姿势不会更新用户闲置时间。
- 暂停、减少动态、隐藏页面、对话框、当前互动、菜单、聊天草稿和待确认日程期间不覆盖角色动作；恢复后重新计满 30 秒，不补播停留期间的切换。
- 独立互动预览仍闲置 60 秒后走向边缘，等待左键找到；主页角色卡不会自行走开。

`tools/verify-idle-postures.cjs` 在空白无头 Chrome 配置中通过 7 组检查：29,999/30,000 毫秒边界、偏好持久化、暂停及恢复、姿势与躲藏的独立计时、互动和草稿保护、64 套外观、手机布局及日常 exe 不变。浏览器长周期使用测试时钟推进；可见性保护使用合成 visibilitychange，不冒称真实浏览器后台调度测试。

记录：`.artifacts/idle-v13-browser/report.json`。Q版主页、边缘躲藏、3D真人运动服、390 像素手机页面四张截图已目视检查，无桌面截图。

行事历回归 `tools/verify-pet-agenda.cjs` 10 组通过，记录 `.artifacts/idle-v13-agenda-regression`。八家接口共 14 次模拟调用，真实 API 调用为 0；确认保存、离线到点、延后、重复和重新加载均通过。本轮未重复完整的合照导出矩阵，合照代码未改。

## 原生自动躲藏

现有 `PetCompanion.TickCompanion` 固定以最后一次用户活动后 60 秒触发，自动散步不会刷新这个计时。目前没有可设置的 N 分钟字段。控制面板、合照、头顶聊天、菜单、拖动、主动休息及其他互动期间会暂缓；隐藏/穿透状态也不触发。关闭控制面板重新计算闲置时间。减少动态时不执行走向边缘躲藏。

新增 `--verify-idle-hide --data-dir <隔离目录>` 审计入口，只增加验证代码，未改原生宠物行为实现。测试单独编译当前源码；日常版本使用的躲藏和散步代码未变。控制面板、聊天保护和关闭开关的 3 项检查用过去活动时间触发；以下两个试验各自等待真实的一分钟，没有推进应用时钟：

| 外观 | 开始躲藏 | 抵达边缘 | 留在边缘后的观察 |
| --- | --- | --- | --- |
| DeepSeek Q版运动服 | 60.026 秒 | 66.073 秒 | 再等 12 秒仍躲藏 |
| DeepSeek 3D真人运动服 | 60.083 秒 | 63.697 秒 | 再等 12 秒仍躲藏 |

两次试验都观察到先前自动散步，并验证右键只打开菜单、左键找到后返回和显示“被你找到啦！”。点击以 WPF 路由事件发给真实输入面，未重跑系统 SendInput。共 15 项通过；其余 62 套外观本轮未重复原生分钟计时。

复现：

```powershell
dotnet build src/DesktopPet.App/DesktopPet.App.csproj -c Release --no-restore
./src/DesktopPet.App/bin/Release/net10.0-windows/DesktopPet.exe --verify-idle-hide --data-dir "$PWD/.artifacts/idle-hide-review"
node tools/verify-idle-postures.cjs
```

原生证据：`.artifacts/idle-v13-native/idle-hide-check.txt` 和 `idle-hide-timing.json`。编译零警告、零错误。日常 exe 的 SHA256 仍为 `88D1212E460BD2DF94DB1270BF892734BCE4CF0A4C33BA9F794A9C3348347BDD`。测试使用隔离数据，不写日常存档；运行中的日常程序自身仍会正常保存陪伴时间。

源码继续推送 GitHub，Gitee 暂停，v1.0 重要版本标签保留。30 秒待机接入原生仍待本轮 Demo 批准。

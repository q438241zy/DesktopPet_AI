# Shift 松手下落 · Preview 15 验证

2026-10-01，用户确认的交互已接入 Windows 宠物与两个长期 Demo：普通松手固定在选定位置，按住 Shift 松手才下落。起手位置、移动速度和停留时间不改变结果；距任务栏 24px 内沿用吸附。

下落途中再次抓住会立即停止重力，保留当前位置和提起姿势；普通松手即可停住。松手下落及重抓不会重播提起动作的第一帧。落地后完成一次缓冲，恢复待机并保存；减少动态效果时直接落地并清除提起状态。失去捕获、焦点或取消拖动不当作下落请求。

## 已完成检查

| 检查 | 结果 |
|---|---|
| 核心与聊天测试 | 40 项通过 |
| 发布包素材解码 | 16 个角色条目、936 项图片定义通过 |
| 原生放置与头顶聊天 | 727 项通过，覆盖 48 套外观 |
| 原生摇晃与恢复 | 792 项通过，覆盖 48 套外观 |
| 原生散步 | 2887 项通过 |
| 原生既有互动 | 403 项通过 |
| 独立提起 Demo | 165 项通过，含真实 JS 输入处理器、重抓、减少动态及离线素材 |
| 新动作样例 | 5293 项回归通过 |
| 常态 Demo 模板 | Shift 下落、动画延续、捕获丢失、取消、摇晃和散步逻辑通过 |
| 新导出的常态 Demo | Preview 15；6 套外观、153 段动作、3524 张无损 WebP；1248 行清单的 Shift 与普通放置说明一致 |
| 发布程序启动 | 独立旧3D存档迁移、服装、进度和位置检查通过，无错误日志 |
| 构建 | 无警告、无错误 |

Windows 检查运行的是 `Release/win-x64/DesktopPet.exe`，使用 D 盘独立数据目录 `artifacts/shift-release/release-verification`，调用实际 `PetWindow` 方法、WPF 渲染器与计时器。浏览器检查执行页面实际处理器；截图使用独立 Chrome 测试配置，未改用户浏览器配置。测试没有覆盖物理鼠标的人工操作或所有多屏幕摆放方式。

原生报告为该目录下的 `placement-check.txt`、`shake-check.txt`、`walk-check.txt`、`interaction-check.txt`、`asset-check.txt`。GPT 3D真人的程序渲染证据为 `gpt-shift-drop.png`、`gpt-caught-placed.png`、`gpt-shift-landed.png`；DeepSeek 浏览器截图位于 `artifacts/shift-release/browser`。

```powershell
dotnet run --project tests/DesktopPet.Tests -c Release
./tools/publish.ps1
./Release/win-x64/DesktopPet.exe --verify-assets --data-dir <D盘独立验证目录>
./Release/win-x64/DesktopPet.exe --verify-placement --verify-shake --verify-walk --verify-interactions --data-dir <D盘独立验证目录>
./tools/demo.ps1 -Executable ./Release/win-x64/DesktopPet.exe
node tools/verify-demo.cjs ./Release/win-x64/Demo/DeepSeek-demo.html
node tools/verify-taskbar-lift-demo.cjs
node tools/verify-motion-study.cjs
```

工程、运行包和 Demo 素材仍在 `D:\VibeCoding\Character`。桌面的 `DeepSeek-動作Demo.lnk` 继续指向 D 盘 `Demo/MotionStudy/index.html`，页内可进入提起 Demo 与全风格常态 Demo。更新前备份用户存档到 `artifacts/shift-release/user-state-backup/state-before-update.json`；隔离测试未覆盖用户存档。

检查期间用户存档与备份逐字节一致。全部检查后已正常启动 D 盘发布程序，保留当前 DeepSeek Q版原装、4 次打卡、0 件收藏及桌面位置；启动后快照保存到同目录 `state-after-startup.json`。

这次没有新增或替换美术图。Q版婚纱的现有悬空末帧仍偏坐姿，保留在后续美术修正项中。五款舞蹈与十个待机仍属于 MotionStudy 独立样例，没有借这次交互修改宣称已接入原生宠物。

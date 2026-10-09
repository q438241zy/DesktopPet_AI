# v1.4 · 原生每 30 秒待机姿势

2026-10-09，用户对 v1.3 HTML 待机 Demo 回复“同意”。本轮接入原生桌面宠物与“我的伙伴”角色卡；2–8 位的多人合照改版仍为独立 Demo，原生保留现有双人合照。

## 行为

- 当前角色卡新增“自动 · 30秒 / 站立 / 坐下”。默认自动，八位角色交错站坐；每个角色分别保存模式，Q版与3D真人共用同一角色的偏好，换衣不改变模式。
- 自动模式只有连续待机满 30 秒才换站坐，暂停或打断后重新计满一个周期，不连播错过的姿势。固定站立/坐下只约束待机，不会阻止聊天、散步或用户提起。
- 使用独立 `IdlePostureClock`；换姿势不调用用户活动记录，不推迟一分钟自动躲藏，也不重置自动散步的时钟。
- 提起、落下、散步、躲藏、主动休息、聊天、照顾、玩耍、菜单和提醒期间不会覆盖当前动作。隐藏/暂停、减少动态、合照对话框暂停自动切换；恢复显示时，角色卡和当前宠物都重新等待 30 秒。
- “伙伴档案”的风格预览保留原图静态对照，不随待机轮换。

## 图片与尺寸

`IdlePosture.Resolve` 对应已批准 Demo 的原图、帧号和参考尺寸。64 套外观、128 个站坐姿势使用原装、运动服、泳装、婚纱各自图片；没有生成新图，也没有跨服装借图。部分 Q版保留批准过的侧面支撑站姿，3D真人采用原有清醒坐姿；不是新做的连续起身动画。

原生按图集的统一参考尺寸缩放，姿态间不按各自包围盒重新撑满。角色卡为站坐预留共同空间；桌面以可见图像的脚底严格对齐地面，避免个别美术锚点亚像素偏差造成悬空。输入面始终随实际显示的身体更新。

无配套站坐图的旧导入角色继续使用其自身服装的原图，不借用其他服装。旧存档没有 `postures` 字段时自动补为空偏好；原角色、服装、收藏、阅读和打卡数据保留。

## 验证

使用隔离 WPF 进程和数据目录，没有操作日常存档，也没有捕获用户桌面。四份全角色服装姿势总览、Q版与3D真人原生控制面板均已检查；最终运动服总览与控制面板再次复核。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 核心时钟、旧存档兼容、服装回退及既有核心功能 | 79 项通过 | `.artifacts/idle-v14-core.txt` |
| 64 套原生站坐与已批准 HTML 的图片/帧/比例对照；30 秒边界、互动、暂停恢复、模式保存和真实控件 | 784 项通过 | `.artifacts/idle-v14-final-r3/postures/idle-posture-check.txt` |
| 控制面板、风格档案、关系、工作提醒、聊天、原合照与躲藏 | 364 项通过 | `.artifacts/idle-v14-final-r3/companion/companion-check.txt` |
| 提起、普通放置、Shift 下落、重新抓住、内嵌聊天 | 969 项通过 | `.artifacts/idle-v14-final-r3/placement/placement-check.txt` |
| 64 套外观双方向行走中断 | 912 项通过 | `.artifacts/idle-v14-final-r3/walk-interrupt/walk-interrupt-check.txt` |
| AI 行事历确认、队列、到点、延后、接口与保存回归 | 173 项通过 | `.artifacts/idle-v14-final-r3/agenda/agenda-check.txt` |
| Q版与3D真人的真实 30 秒待机 → 散步 → 60 秒躲藏，以及面板/聊天/开关保护 | 17 项通过 | `.artifacts/idle-v14-final-r3/hide/idle-hide-check.txt` |

待机边界使用应用测试时钟；真实分钟试验中，DeepSeek Q版运动服于 30.172 秒换姿势、60.021 秒开始躲藏，3D真人运动服分别为 30.105 秒、60.088 秒。两者走到边缘后再等 12 秒仍躲藏，右键打开菜单不返回，左键找到才出来。其余 62 套未重复原生一分钟实测。现有躲藏间隔保持固定一分钟；控制面板或聊天开着会暂缓，关闭控制面板后重新计时。

鼠标互动回归走真实 WPF 输入面和手势逻辑，本轮未重跑物理 SendInput。行事历接口为本机模拟服务，真实付费 API 调用为 0。本轮 HTML 仅更新版本数据，几何和图片字段逐项保持不变，未重复上轮已通过的七组浏览器姿态检查。

复现已批准图片对照：

```powershell
node tools/export-idle-posture-reference.cjs .artifacts/idle-posture-review
dotnet run --project src/DesktopPet.App -c Release -- --verify-idle-postures --data-dir "$PWD/.artifacts/idle-posture-review"
```

独立原生真实计时：

```powershell
./Release/win-x64/DesktopPet.exe --verify-idle-hide --data-dir "$PWD/.artifacts/idle-hide-review"
```

## 发布

已替换并启动日常原生 v1.4。1,367 份现行运行素材与候选发布目录逐字节相同，图片变化和缺失均为 0；部署只替换执行文件、对应调试符号和文档。正式路径继续使用 `Release/win-x64/DesktopPet.exe`。不制作新的正式安装包，Gitee 暂停，源码继续 GitHub，v1.0 重要版本标签保留。

已验证执行文件 SHA256：`853A9DDD4F7B33DEB363333618D2D9522F65505DE288E9A9AFBE27C76A2CFBC2`。

旧程序与日常数据备份在 `.artifacts/release-before-v14-20261009-214849`；替换期间用户文件哈希一致。实际启动后核对 21 项稳定存档字段及 3 个已有伙伴的关系、时间和互动记录保留，未新增错误日志；运行记录 `.artifacts/idle-v14-release-record.json`。陪伴时间在正常运行中继续累积，不要求运行前后整个 state.json 哈希一致。

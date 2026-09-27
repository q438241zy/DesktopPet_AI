# DesktopPet · 桌边伙伴

一个真正待在 Windows 桌面上的小宠物。它会回应触摸、陪你玩球、散步找小礼物，也会安静地睡觉。无需登录或 API Key。

![宠物之家：九位伙伴与独立更衣室](docs/images/pet-home.png)

由 **QQ奶茶大神** 的 UsageDashboard_AI 与 Character_Generator 整合而来，互动和九位角色画稿来自 DS Go。沿用原生 WPF 透明窗口、托盘与快捷键；取消举看板、余额采集与平台登录。

## 开始使用

下载 [最新绿色版](https://github.com/q438241zy/DesktopPet_AI/releases/latest)，完整解压后双击 `DesktopPet.exe`。保留旁边的 `Assets` 与 `Studio` 文件夹。绿色版包含 .NET 运行时，无须另装 SDK，不会设置开机启动。

- 点击睡着的宠物：打卡、叫醒并吃早饭。同一天只计算一次。
- 点击头、脸、身体：摸头、戳脸、挠痒。拖动可抱起和放置，左右甩动有轻甩、大力甩及两档晕眩反应。
- 右键宠物：打开照顾、玩耍、动作、休息与设置菜单；也可用 Tab、Enter 操作菜单。
- 玩球：从菜单拿球，拖动球后松开投出。砸中与落空使用不同动画。
- 搭积木：逐块堆起来，再踢散。散步探索会带回永久保存的小礼物。
- 休息：挥手后收成月亮按钮，点击即可回来。休息时偶尔会探头。
- 系统托盘双击：打开“宠物之家”。关闭设置窗口不会退出宠物。

| 快捷键 | 操作 |
| --- | --- |
| Ctrl+Alt+U | 显示或隐藏 |
| Ctrl+Alt+S | 打开宠物之家 |
| Ctrl+Alt+L | 解除鼠标穿透并找回宠物 |
| Esc | 取消当前手势、菜单或玩具 |

快捷键被其他程序占用时，系统托盘仍可操作。

## 九位伙伴与更衣室

小埋、GPT、Claude、Gemini、Grok、DeepSeek 鲸鱼娘、Qwen、GLM、Kimi。旧看板项目的其他内置角色不包含在本项目中；小埋另保留原看板的“经典小埋”坐姿及原始素材。

每位角色有六姿势图集、独立坐姿晕眩图和十七组动作。八位 AI 角色可选择原装、泳装、婚纱，各自记住服装选择。婚纱有独立走路与摸头动画；缺少服装专用画稿的动作使用原装动画，结束后恢复服装。正餐、扑抱、戳脸、挠痒和踢积木沿用 DS Go 当前的动作回退规则，不声称已有尚未绘制的新动画。

亲密度按累计打卡增长，有九级称号，中断不会扣减。保存领养日、纪念日、打卡记录及散步收藏；离开三天以上再次打开，会得到欢迎回来的回应。不设饥饿惩罚、疾病、清洁事务、麦克风或强迫照顾。

## 角色工坊

原 Character_Generator 保留在 `skills/character-storyboard-generator`，完整保留 30 种姿势与 40 种表情的定义、出图规范及校验脚本。

应用内“角色工坊”可生成和复制出图提示词、导出 `pet.json` 模板，并导入图稿。**工坊本身不调用图片生成服务**：请将提示词与参考图交给支持出图的工具。

支持三种导入方式：

1. 含 `pet.json` 的完整角色包：可带六帧图集、各动作时间与服装。
2. 原生成器的标准 `__P01-…__E01-…` 命名 PNG 成果目录：将能识别的姿势映射为静态动作。
3. 单张 PNG、WebP、JPEG：作为静态宠物，支持拖动和互动入口。

单张图不会变成真实逐帧动画。完整规范见 [角色包格式](docs/character-packs.md)。

## 本地数据

存档和导入角色位于 `%LOCALAPPDATA%/DesktopPetAI/`。存档原子写入；损坏存档会保留带时间戳的副本。测试可通过 `--data-dir <独立目录>` 使用独立数据，避免改变日常存档。

程序不读取旧看板的 Cookie、账号令牌或 Codex 凭据。互动台词是本地文案，不是在线 AI 对话。当前支持 Windows x64；macOS/Linux 没有在这一版移植。

## 开发与验证

需要 Windows 与 .NET 10 SDK。

```powershell
dotnet run --project src/DesktopPet.App
dotnet run --project tests/DesktopPet.Tests -c Release
./tools/check.ps1
./tools/publish.ps1
```

`check.ps1` 运行行为检查、WPF 构建、全部运行素材解码与真实窗口集成回归，使用隔离存档。`publish.ps1` 生成 `Release/win-x64/DesktopPet.exe` 及其素材目录。

```text
src/DesktopPet.Core/    角色清单、动画选帧、打卡存档、摇晃与球碰撞
src/DesktopPet.App/     WPF 桌面窗口、互动菜单、宠物之家、角色工坊
skills/                原角色生成器 Skill
artwork/sources/        用户指定目录的 16 张婚纱动作 PNG 原稿
tests/                 独立行为检查
docs/                  角色包规范、迁移来源、测试记录
```

本项目独立运行，不修改 DS Go 的工程或用户数据。DS Go 的会话归档、模型完成通知和余额服务属于宿主功能，没有连接到本项目。

## 授权

沿用 [个人非商业许可](LICENSE)，保留 QQ奶茶大神署名。DS Go 移植部分的 MIT 许可、SkiaSharp 和美术来源见 [第三方说明](THIRD_PARTY_NOTICES.md)。角色同人形象不代表对应厂商官方吉祥物；第三方角色和商标的权利仍属于原权利方。

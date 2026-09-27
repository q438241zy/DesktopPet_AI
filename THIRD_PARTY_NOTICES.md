# 来源与第三方声明

## UsageDashboard_AI

- 作者：QQ奶茶大神。
- 原仓库：`https://github.com/q438241zy/UsageDashboard_AI`。
- 导入版本：`a09cc84d5f9cd14232d65178a2c245e3529b53b6`。
- 使用范围：WPF 透明桌面应用组织方式、原生快捷键与托盘能力的重写，以及小埋原始六姿势素材。
- 许可：保留根目录 `LICENSE` 的个人非商业许可。余额采集、账号登录、其他旧角色与 Avalonia 工程未导入。

## Character_Generator

- 作者：QQ奶茶大神。
- 原仓库：`https://github.com/q438241zy/Character_Generator`。
- 导入版本：`b147334b04e2b280a5ab171a67918d3904dab378`。
- 使用范围：`Skill/character-storyboard-generator` 的完整副本；应用读取其姿势和表情定义，导入器识别其规范文件名。
- 原仓库没有独立开源许可证；由同一仓库所有者明确授权整合。原角色范例目录未导入。

## DS Go / DeepSeek Harness

九位角色的姿势、动作、换衣画稿与玩法定义来自用户提供的本地 DS Go 工程。动画时长、九级亲密度门槛、摇晃阈值与部分回退语义移植到 C#。原始代码许可为 MIT，见 `docs/DeepSeek-LICENSE.txt`。

`artwork/sources/wedding` 为用户指定 `.artifacts/pet-wardrobe-motion-pack/sources/wedding` 的原稿副本，没有覆盖原稿。运行文件来自同一 DS Go 工程已经对齐的 `packages/client/pet/src/assets`。

相关 AI 同人形象参考信息记载于 DS Go 的 `ai-companion-prompts.json`：Claude、GPT、Gemini、Qwen、GLM 与 Kimi 参考 MiRuru 作品中的角色特征；Grok 是补充的原创形象。小埋和各品牌、角色、商标的权利属于其原权利方。代码许可不授予第三方角色或图像的商业使用权。

## SkiaSharp

SkiaSharp 3.119.4（Microsoft / Mono contributors），MIT 许可证。用于读取 PNG/WebP 并向 WPF 提供像素；上游：<https://github.com/mono/SkiaSharp>。其原生 Skia 依赖包含 BSD 等许可，随 NuGet 分发的声明仍适用。

Windows 与 .NET 运行时属于其各自权利方。绿色包内运行时依照 .NET 分发许可提供。

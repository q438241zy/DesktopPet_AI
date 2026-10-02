# 原装 Q版点心动作

八位原装 Q版角色此前把 `motions/feed.webp` 魔法棒动作当成零食。现在 `eat` 和 `feed` 均使用当前角色的 `motions/snack-continuity.png`：八个不同姿势，持饼干、抬手、送到嘴边、咬下、咀嚼和放松。早餐仍使用原来的米饭动作。

使用内置 imagegen，按各角色的原装参考图绘制；未使用 API CLI 或人工像素编辑。最终原始 RGBA 画稿保存在本目录 `sources/`，程序使用的副本在 `src/DesktopPet.App/Assets/Characters/<id>/motions/snack-continuity.png`。两者 SHA256 相同。

最终提示词集合位于 `prompts/`。共同要求是保留角色身份、服装、头身比例，八帧手指持真实饼干并送到嘴边，后续画出缺口和咀嚼，禁止魔法棒、额外食物、场景、文字。GPT、DeepSeek、Grok 最终使用各自 `*-spacious.txt`；Claude、Qwen、Kimi 使用原始提示词加 `layout.txt`；Gemini、GLM 使用初始提示词。`results/` 记录最终生成文件与迭代来源。

`tools/install-snack-correction.py --install` 检查真实透明通道、八个独立角色、无外边截断、不同源姿势，然后原样复制文件、写入单帧范围、固定像素比例与 3000ms 非循环时序。它只读像素做测量，不修改图像。

重新安装后执行 `tools/calibrate-action-scale.py --install`，再运行 `--verify-scale`、`--verify-chibi`、`--verify-care`、`--verify-interactions`、`--verify-walk`、`--verify-dance` 并重新导出常态 Demo。实际 WPF 尺寸与抗锯齿保留在渲染检查中验证。

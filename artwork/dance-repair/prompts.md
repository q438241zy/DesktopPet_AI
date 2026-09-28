# 真人舞蹈底图修正

制作日期：2026-09-29。使用 Codex 内置 `image_gen` 编辑工具，启用透明背景；未使用 API/CLI。输入为项目已有角色图及本轮准备姿势参考，保持各角色身份、配色、配饰与原装／泳装／婚纱的覆盖范围。

最终为 8 张 1536×1024 RGBA 图集，每张含 3 套服装，共 24 个舞蹈准备姿势。它们是程序动画的底图，不是 24 张独立逐帧舞蹈图。源图原样复制进项目，透明通道未重画。运行、发布和重新校准均可直接读取 D 槽文件，不依赖 C 槽生成缓存。

| 角色 | 已保存的最终图集 | 最终编辑提示词 |
|---|---|---|
| DeepSeek | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/deepseek-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/deepseek-adult-separated.txt) |
| GPT | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/gpt-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/gpt-adult-separated.txt) |
| Claude | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/claude-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/claude-adult-separated.txt) |
| Gemini | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/gemini-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/gemini-adult-separated.txt) |
| Grok | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/grok-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/grok-adult-separated.txt) |
| Qwen | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/qwen-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/qwen-adult-separated.txt) |
| GLM | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/zhipu-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/zhipu-adult-separated.txt) |
| Kimi | [dance-base-atlas.png](../../src/DesktopPet.App/Assets/Characters/kimi-adult/motions/dance-base-atlas.png) | [完整提示词](prompts/kimi-adult-separated.txt) |

提示词的共同要求：正面准备姿势、双脚分开且脚底同高、手臂自然向外放下；长发与头纱收在背后，给手臂留出透明空间；服装顺序为原装、泳装、婚纱，保留完整人物与清晰手指。`prompts/*-atlas.txt` 记录第一轮，`*-separated.txt` 记录针对头发、袖口和裙摆重叠的最终修图。`reference-dance-ready.png` 是准备姿势参考，不参与运行。

`manifest.json` 保存最终图集的尺寸、裁切区域与 SHA-256；`calibration.json` 保存每套服装在源图上的 17 个关节、腰线、裙边和 5 个衣料宽度。`tools/install-dance-art.py` 只测量透明轮廓、复制原图并写入角色清单，不修改源图像素。清单中的 UV 坐标相对各自裁切区域。

舞蹈参考了 [Pittsburgh Ballet Theatre 的基础姿态说明](https://pbt.org/community/resources-audience-members/ballet-101/basic-ballet-positions/)中屈膝、脚底接地与圆润手臂的原则，以及 [Royal Academy of Dance 关于姿态、重心与控制的课程主题](https://www.royalacademyofdance.org/event/ballet-basics-posture-weight-placement-and-control/)。编排为本项目自己的 16 拍轻侧步、点地、小幅展臂和收势；未复制舞者影像，也没有使用动作捕捉。

当前是二维图片的轻幅程序舞蹈。手臂活动幅度控制在底图能够自然表现的范围内；长婚纱采用更短步幅，保持衣料连贯。真人版提供舞蹈，Q 版与 3D 版沿用既定范围。

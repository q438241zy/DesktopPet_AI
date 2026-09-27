# 服装与画风的动作连续性

日期：2026-09-27。工具：内置 `image_gen`，透明背景；参考图均先查看后使用。未调用外部绘图 API，未对输出图进行离线像素编辑。运行时按清单裁帧、显示和对齐。

本批新增 56 张 1536×1024 PNG，每张为 3 列 × 2 行的六帧向右行走：

- DeepSeek、GPT、Claude、Gemini、Grok、Qwen、GLM、Kimi 的 Q 版泳装：8 张。
- 八位角色的 3D 与真人形象，各有原装、泳装、婚纱：48 张。

Q 版使用同角色的泳装图与原装行走图作为服装及动作参考。3D / 真人版使用当前画风、当前服装的已确认立绘作为参考。原有 Q 版原装、婚纱行走仍使用此前素材。

`requests.json` 保存本批提示词模板、逐项参考及目标路径。核心约束为：同一角色身份与服装；维持已有画风及头身比例；六个不同且衔接的步态；统一向右；完整身体、透明边距和一致比例；不举牌、不附文字或场景。边缘不足的图以原图再次编辑，修正提示词记录在相应结果中。最初 DeepSeek 六张使用较紧的留白要求，后续模板加强为每格上下约 64 像素留白；实际输出仍经过逐格轮廓检查。

`results/` 记录生成原文件及补充修正；`manifest.json` 记录最终采用文件的 SHA-256。原生成文件保留在 Codex 图片目录中。运行时资源位于：

```text
src/DesktopPet.App/Assets/Characters/<id>/motions/walk.png
src/DesktopPet.App/Assets/Characters/<id>/outfits/swim/walk.png
src/DesktopPet.App/Assets/Characters/<id>/outfits/wedding/walk.png
```

每帧 160 毫秒，原图朝向 `right`，向左时由程序统一翻转。程序从 alpha 轮廓读取脚底、人物高度及头部水平位置，以保持站立与行走衔接。这些素材是二维序列图，不是实时三维模型。

`tools/verify-motion-art.py` 只读检查尺寸、透明度、六格完整边界、帧与整图去重。WPF 集成检查覆盖全部 24 个形象、72 套外观的动作解析、实际显示、双向行走、换帧及中途换装。美术逐帧仍可能存在细节差异；未绘制的非行走互动保持当前服装姿势，不跨服装或画风补图。

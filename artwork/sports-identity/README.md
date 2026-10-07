# W2 运动服逐动作换衣复核

用户已批准拓展八位角色的 Q版与3D真人。此目录保存按各自原图进行换衣编辑的原始 PNG、提示词与参考来源；日常程序仍为 **v0.4**，本轮运动服尚未全量安装。

运动服固定为上臂短袖、膝上短裤。对照采用共同画布和同一等比缩放，不按各帧身体包围盒缩放，也不拉伸腿或改造角色。透明区域在浅色和深色背景复核。

## 当前进度

- DeepSeek 两种风格：动作图完成目视对照，在 `.artifacts/sports-identity-stage` 隔离运行。Q版543项、真人547项运动服检查通过；五项互动5902项、尺寸4541项、行走3847项、未打卡地面散步960项通过。全64套回归中只有 DeepSeek 两套使用本轮新图，其余62套仍是原素材，不能由此声称全量新图通过。
- GPT 两种风格：动作候选图已生成，正在修订 Q版残留袖口、擦脸道具突变与吃零食坐姿；尚未完成原生运行验收。
- Claude：基本姿势、步行和真人立绘已对照，Q版动作逐组生成和复核中。
- Gemini、Grok、Qwen、GLM、Kimi：基本姿势、步行和真人立绘已有审阅记录；其余运动服动作待补齐。

`results/*.json` 记录逐张选用状态与源/候选 SHA256；`review-decisions.json` 是明确的目视结论。`selected` 只表示候选稿通过当前静态对照，不代表已经安装或用户重新批准。原生测试记录见 `docs/verification-sports-identity-progress.md`。

## 对照与隔离检查

1. 内置 ImageGen 编辑原图，原始 PNG 不经代码重绘，保存提示词与来源。
2. `python tools/install-sports-review-demo.py` 部署逐帧对照页。
3. `node tools/verify-sports-review.cjs --changed` 检查新/改变的对照并保存证据。缓存以源图、候选图、帧定义、对照窗口及渲染脚本指纹约束。
4. 实际查看并排图后记录明确结论，再运行 `python tools/apply-sports-reviews.py`。源图改变会使旧审阅失效，不能只更新哈希延用结论。
5. `python tools/stage-sports-identity.py <character-id>` 只写项目 `.artifacts` 下的隔离资产。保持原动作时序和原像素校正，手部/泡泡接触点另行核对；侧车光流只驱动显示，不覆盖原 PNG。
6. 隔离原生动作、尺度、步行和互动检查通过后，再按发布流程更新日常目录与长期 Demo。

入口：`Release/win-x64/Demo/WardrobeIdentity/motions.html`。此页的功能检查与图片目视判断分开记录。未通过旧稿保留来源并明确标注“未采用”。

# W2 运动服逐动作换衣复核

用户已批准拓展八位角色的 Q版与3D真人。2026-10-09已补齐16套的基础姿态、行走、全部动作及五项互动，共305份选用图稿；原生全量验证通过并接入源码，v0.5日常目录已更新并正常启动，长期Demo同步完成。

运动服固定为上臂短袖、膝上短裤。以各自原图换衣，采用共同画布和同一等比尺度，不按单帧身体轮廓缩放、不拉伸腿。图稿、提示词、参考来源和拒绝理由保留；只有选用稿进入运行清单。

最终原生矩阵为8600项运动服、4576项尺度、4082项画面、3847项行走、960项未打卡落地、5902项五项互动及912项行走中断，16角色1898图片定义解码通过。80张五项截图及各套18动作总览已目视查看；Claude Q礼盒接触点修订后重跑全部检查。证据见`.artifacts/sports-final-r2-20261009`、`installation.json`和`docs/verification-v05.md`。

`results/*.json`的`selected`仅表示静态稿选用，安装必须另查`installation.json`与原生运行文件。原始PNG始终不被程序重绘，显示时使用归属掩码隔离相邻人物。完整过程见`docs/verification-sports-identity-progress.md`。

## 对照与隔离检查

1. 内置 ImageGen 编辑原图，原始 PNG 不经代码重绘，保存提示词与来源。
2. `python tools/install-sports-review-demo.py` 部署逐帧对照页。
3. `node tools/verify-sports-review.cjs --changed` 检查新/改变的对照并保存证据。缓存以源图、候选图、帧定义、对照窗口及渲染脚本指纹约束。
4. 实际查看并排图后记录明确结论，再运行 `python tools/apply-sports-reviews.py`。源图改变会使旧审阅失效，不能只更新哈希延用结论。
5. `python tools/stage-sports-identity.py <character-id>` 只写项目 `.artifacts` 下的隔离资产。保持原动作时序和原像素校正，手部/泡泡接触点另行核对；侧车光流只驱动显示，不覆盖原 PNG。
6. 隔离原生动作、尺度、步行和互动检查通过后，再按发布流程更新日常目录与长期 Demo。

入口：`Release/win-x64/Demo/WardrobeIdentity/motions.html`。此页的功能检查与图片目视判断分开记录。未通过旧稿保留来源并明确标注“未采用”。

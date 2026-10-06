# 短袖＋短裤运动服

用户在 2026-10-02 确认扩展全部八位角色的 Q版和3D真人。服装固定为上臂短袖、膝上运动短裤、短袜和运动鞋。已撤销的长袖/长裤批次在 `../sports/superseded-long-pants`，不可重新安装。

绘图使用内置 image_gen；每张图的原始输出、提示词、参考、实际帧排列与目视检查记录在 `results/`。选定 PNG 存在对应 `src/DesktopPet.App/Assets/Characters/<id>/outfits/sports/`；程序不依赖 C 盘生成缓存。Python 只读取像素测量裁框与光流，写入 JSON，不修图或缩小原图。

关键检查：每帧短袖短裤、角色身份、完整身体、同尺度；吃东西和梳头擦脸使用手中真实画稿道具；`hands` 与 `bubbleSources` 必须与实际掌心和泡泡环相符。新增 `sports` 外观保持独立，不回退到原装、泳装或婚纱。

安装选定记录：`python tools/install-sports-shorts.py --key <record-key>`；完整资产检查：`python tools/install-sports-shorts.py --check`。原生 `--verify-sports` 验证十六套服装、动画源、体型平面、触点和泡泡环；衣服与解剖仍必须实际观看。最终发布证据放在 D 盘 `.artifacts`，尚未通过完整矩阵时不能把记录数量当作完成。

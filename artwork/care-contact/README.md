# 原装、泳装、婚纱的梳头与擦脸

范围：16位角色的48套旧服装，96个动作。运动服照顾图属于独立 sports 素材流程。

- 每个动作读取该外观自己声明的 `motions/cloud-care-*.png`，图内已经绘制梳子或毛巾，`bakedProps` 禁止再次叠加道具。
- 原图均为 imagegen 输出的逐字节副本；`results/` 保存完整提示词、参考路径、原始来源、审核状态。失败候选 `selected:false` 不安装。
- `tools/care-contact.py` 复用 `sports-art.py` 测量独立透明裁框与双向光流。`referenceGroups` 以每个实际动作行的中立姿势高度为量尺，补偿原图排版上下行的大小差异，动作内保持固定量尺。旧Q图是坐姿，原待机也是坐姿；真人图均为站姿。最终体型仍须由原生截图复核。
- GPT、Claude、Gemini Q及DeepSeek Q组合图中，擦脸开场改用已经拿毛巾的第7帧，避开第4帧仍拿梳子的错误图。
- Qwen婚纱及Kimi原装Q的第6帧头饰被镜像，明确排除；擦脸使用4/5/7三个正确姿势，总时长4200ms。完整梳头总时长4800ms。
- Grok Q泳装使用2列4行8格原图，其他单套主要为4列2行。真实裁框/索引决定播放，未假定统一网格。

验证与维护：

```powershell
python tools/care-contact.py --verify
python tools/care-contact.py --measure
# 仅在已协调角色清单写入权后重新安装；每次安装读取最新 pet.json
python tools/care-contact.py --install --ids whale deepseek-adult
```

`coverage.json` 是从最终已安装清单重新读取的48套覆盖与PNG SHA-256结果，`installed.json` 列出选定原图。原生 `--verify-club` 对所有64套（包括运动服）强制验证真实角色动作视觉与单一道具，不允许道具编排回退代替照顾素材。原生验证使用独立D盘 `--data-dir`，不改用户存档。

2026-10-03 原生目检返修：Gemini/Grok 真人旧24格虽然总高度稳定，但出现大头短腿，已废弃。三套服装分别改为独立8格：Gemini原装v7（2×4），其余Gemini/Grok v6（4×2）。这些原图保持成人长腿比例、两只手和原鞋，重新测量接入；最终比例须复核重拍的原生截图。Gemini原装v6因下排脚被裁保留为拒收记录。

# 3D 真人短袖短裤运动服

本批完成 DeepSeek、GPT、Claude、Gemini、Grok、Qwen、GLM（zhipu）、Kimi 的 `*-adult` 八项兼容 ID。每人七张透明图集，共 56 张选用原图；保留日常运动服动作。原图保持内置 ImageGen 输出字节，不通过脚本绘制、抠图或修改像素。短袖在上臂结束，短裤在膝盖以上，配短袜和运动鞋；每张选用图均检查完整身体、身份、服装和手部道具接触。

## 已安装内容

| 图集 | 每人姿势数 | 用途 |
| --- | ---: | --- |
| portrait | 12 | 待机、触摸、情绪、头晕；最后两格旧敲槌姿势不再播放 |
| body | 16 | 聊天、思考、跳跃落地、坐卧、积木、提起、撞球和漏接 |
| walk | 8 | 右向完整双步周期，包含双腿交替及弯膝过脚 |
| catch | 12 | 零食 4 格、米饭 4 格、掌心准备和接住 4 格 |
| club | 20 | 数星星、吹泡泡、伸懒腰，按实际物理格映射 |
| care | 8 | 梳头 4 格、擦脸 4 格，手部真实接触 |
| bonk | 3 | 中性站立、举槌、轻敲；修正旧图为容纳槌子而压短腿的问题 |

所有服装素材位于 `src/DesktopPet.App/Assets/Characters/{id}/outfits/sports/`，运行不依赖 C 盘生成缓存。`results/*-adult-shorts-*.json` 的 `selected=true` 是最终选用依据；`source` 指向 D 盘已安装的同字节 PNG，`originalSource` 只保留生成来源。第一次生成及返修提示词均保存在本目录的 `realistic-*-prompts.json` 与逐图记录中，使用工具为内置 ImageGen，透明背景开启。

## 动作与校准

- 行走使用 8 个不同关键姿势，每格 145 ms，完整周期 1160 ms。v2 光流元数据连续插值并连接最后一格到第一格，重复格不计为新增步态。`walkStride` 按首格前后脚跟投影的手工像素估测与站立轮廓高度计算；原像素坐标与公式在记录内，可复核后调整。
- 身体图、坐姿、蜷起和睡眠共用站立参考像素高度，不把坐卧轮廓拉成站立高度。新版 bonk 为独立完整比例的三姿图集，原 portrait 记录已移除 bonk 定义，防止重装时覆盖。
- `hands` 按 catch 图第 8–11 个物理格的掌心、两掌跨度实测，接触点随姿势插值。`ball-hit` 使用 body 第 14 格的受击反应，球反弹；`ball-hold` 使用接住姿态。受击不伪造肘部接球点。
- 泡泡发射点以各图物理格 8、9、10、11、12、15 的圆环中心计入 `bubbleSources`。club 为实际 20 格，动作帧序列含重复及非顺序映射；未使用的物理格锚点为 null。
- 零食、米饭、积木、梳头、擦脸、泡泡棒与敲槌使用画中道具，`bakedProps` 防止再叠加重复道具。

## 可重装入口

从 `D:/VibeCoding/Character` 执行，元数据重算不改原图像素：

```powershell
python artwork/sports-shorts/calibrate-realistic.py
python artwork/sports-shorts/install-realistic.py
python tools/install-sports-shorts.py --check
```

主安装脚本支持 `--id deepseek-adult` 或 `--kind bonk`。主协调安装器也可直接读取逐图 selected 记录。

`realistic-manual-measurements.json` 保存掌心、圆环与脚跟测量。`realistic-coverage.json` 为图集安装结果，`realistic-final-validation.json` 为本批验证概要。

## 本批验证证据

56 张保留选用图通过透明通道、独立人体数量、完整画布边界检查。清单检查结果：`16 sports appearances, 0 missing requirements`。保留的真人运动服动作均指向专用素材。

8 位真人各通过一次原生 Sports 单例检查，每例 544 项。新 bonk 接入后重跑以下三例并检查实际截图：

| 角色 | 最终原生证据目录 | 结论 |
| --- | --- | --- |
| DeepSeek | `.artifacts/sports-deepseek-realistic-native-final/` | 544 项通过；新 bonk、吃饭、接触与坐卧比例视检 |
| GPT | `.artifacts/sports-gpt-realistic-native-final/` | 544 项通过；新 bonk 腿长和头身恢复 |
| Claude | `.artifacts/sports-claude-realistic-native-final/` | 544 项通过；新 bonk、米饭及泡泡视检 |

其余五例在 `.artifacts/sports-{gemini,grok,qwen,zhipu,kimi}-realistic-native/`，各 544 项通过，并检查米饭与泡泡实际截图。它们在最终协调者全矩阵检查中再次覆盖新 bonk。


DeepSeek 与 GPT 各通过 17 项实时进食/球接触检查；勺子随人物送到嘴部，撞到人物后球反弹，远处漏接单独回应。证据分别为 `.artifacts/sports-deepseek-realistic-contact-final/` 与 `.artifacts/sports-gpt-realistic-contact/`。

本批源清单已停止写入，交由协调者完成旧三套服装 care 合并后的统一校准、完整矩阵验证、Demo 同步和发布。本批未发布或修改 Release。

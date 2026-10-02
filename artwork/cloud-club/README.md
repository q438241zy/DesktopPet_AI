# 云朵俱乐部原生扩展

2026-10-02 用户确认第二轮 HTML Demo，授权接入桌面并扩展。测试阶段全部开放，白金固定浅玉金属绿。

## 画稿

- 8 位角色 × Q版 / 3D真人 × 原装 / 泳装 / 婚纱，共 48 套。
- 每套一张 24 姿势透明 PNG：0–7 数星星、8–15 吹泡泡、16–23 伸懒腰。共 1152 个关键姿势。
- DeepSeek 使用已确认的六张 Demo 原图，其余角色使用各自服装参考，通过内置 `image_gen` 生成。完整提示词、参考与选择路径见 `generation-results.json`。
- `tools/install-cloud-club-art.py` 只读像素，生成裁切区域、连通部件归属、固定身体比例与双向位移场元数据，不改写原画像素。手脚相连的失败图不安装。
- 原生 `ClubPoseVisual` 以单张原图颜色和连续网格位移衔接姿势；泡泡棒、瓶子由画稿提供，程序只生成吹气阶段的泡泡。
- 梳头、擦脸沿用已确认的当前姿态与移动道具；捉蝴蝶沿用当前服装的行走和接物姿态。

## 运动衫

DeepSeek Q版与3D真人的奶白深蓝运动衫保留原设计，改为短袖。样稿在 `docs/demo/cloud-club/art/school-*.png`，编辑记录见 `short-sleeve-prompts.json`。尚未新增第四套服装的全动作图集。

## 验证与更新

1. `python tools/install-cloud-club-art.py` 必须完整安装 48 套；`--available` 仅供制作中诊断。
2. `python tools/calibrate-action-scale.py --install` 保留本批独立身体坐标。
3. `dotnet run --project tests/DesktopPet.Tests -c Release`。
4. 原生 `--verify-club` 遍历全部 48 套 / 六项互动，检查实际渲染变化、固定身体比例、当前外观、吹气时序、计数、戳泡泡及右键连续播放。所有 `--verify-*` 使用 D 盘独立 `--data-dir`。
5. 回归会员、照顾、尺寸、任务栏接触、界面、躲藏、行走、舞蹈与现有动作；使用 `--export-demo` 同步长期 Demo 和覆盖清单。
6. `tools/publish.ps1` 发布到 D 盘并安装 CloudClub；桌面仍只有原有 Demo 快捷方式。

注册仍是本机账号，无账号服务器或收费授权。测试开放不修改实际账号等级。

# 角色包格式 v1

一个角色包是包含 `pet.json` 与其引用图片的目录。导入时只复制清单和被引用的图片，不执行任何代码，也不会复制账号文件。

```json
{
  "version": 1,
  "id": "my-pet",
  "name": "我的伙伴",
  "accent": "#D59477",
  "category": "chibi",
  "description": "角色介绍",
  "demo": false,
  "atlas": { "file": "atlas.png", "columns": 3, "rows": 2 },
  "dizzy": { "file": "dizzy.png", "columns": 1, "rows": 1 },
  "motions": {
    "walk": { "file": "walk.webp", "columns": 3, "rows": 2, "facing": "right", "frameMs": [160,160,160,160,160,160] }
  },
  "outfits": {
    "wedding": {
      "name": "婚纱",
      "idle": { "file": "wedding.png", "columns": 1, "rows": 1 },
      "motions": {}
    }
  }
}
```

六姿势图集按行排列：悠闲、开心、摸摸、睡眠、晕倒、难过。标准为 1536×1024，每格 512×512，身体中心 x=256、落地脚底 y=448。各动作必须共用比例和基线。单张图应明确写 `columns: 1, rows: 1`。

`category` 为 `chibi`（Q版）、`3d`（3D版）、`adult`（成人版），省略时兼容旧包，归入 Q版。这是画风分类，不改变渲染方式；当前均使用二维透明图。`demo: true` 会标注为静态风格 Demo。

每个图集可指定原始朝向 `facing: "right"` 或 `"left"`，默认朝右。行走时按移动方向与原画朝向一起计算水平翻转；不要把左右朝向混在同一组帧内。程序按每帧可见像素的底边对齐桌面地面，避免额外浮动。行走必须提供至少两帧的 walk 动作；单张静态姿势不能触发桌面散步。

`motions`、`outfits` 和 `dizzy` 可省略。不要在清单引用尚未画好的图片。`frameMs` 若提供，长度必须等于格数，每项 40–5000 毫秒；省略时每格 240 毫秒。动画名称包括 eat、feed、chat、bonk、farewell、walk、angry、headpat、curl、ball-hit、ball-miss、pickup、think、jump、peek、shaken、shaken-strong，以及可逐步补齐的 meal、pounce、poke、tickle、kick。

动作选择顺序是当前服装同名动作、原装同名动作、语义回退动作、基本姿势。正餐回退吃饭、扑抱回退聊天、戳脸回退摸头、挠痒和踢积木回退跳跃。没有画稿的动作不会编造动画帧。

导入器拒绝重复角色 ID、绝对路径、上级目录引用、文件流、符号链接、目录联接和非图片文件。清单最多 256 KB，单图最多 24 MB、边长最多 6144 像素，全部图片最多 180 MB。图片必须可解码且尺寸能均分到格子。ID 为 1–48 位小写字母、数字和连字符。

原 Character_Generator 的成果目录可直接导入。导入器按 P01、P06、P10、P11、P17、P23、P24 与 E01/E02/E03 识别基本姿势；未识别的组合仍保留在原成果目录，不会自动塞进不对应的互动。它们是静态姿势，若希望真正逐帧动作，请使用上面的图集清单。

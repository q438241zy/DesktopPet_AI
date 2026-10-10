# v1.5 · 安装复核、纯聊天与 Claude 正面待机 Demo

2026-10-10。用户从桌面进入，要求确认是否真正安装、修正 Claude 画风，并让聊天保持简单。按既定「先 HTML Demo，拍板再拓展」规则，本轮交付 Demo，原生继续 v1.4。

## 日常程序复核

- 桌面「云朵伙伴」指向 `D:\VibeCoding\Character\Release\win-x64\DesktopPet.exe --settings`，运行进程路径一致，产品版本 1.4。
- SHA256 `853A9DDD4F7B33DEB363333618D2D9522F65505DE288E9A9AFBE27C76A2CFBC2` 与上一轮经过验证并安装的文件一致。
- 运行目录 `Assets/Characters` 的 **1366 个文件**逐项与源码对比，变动 0、缺失 0。未读取或发布个人聊天、密钥、桌面画面；未修改日常存档。
- 因此此前 v1.4 待机更新已经安装。Claude 侧身的原因是 Q运动服的站姿选择器用了行走第 7 帧，不是桌面入口指向旧版。
- 这是安装内容复核。本轮没有重跑原生分钟计时或物理鼠标矩阵；这些历史证据见 v1.4 记录。

## 本轮 Demo

入口仍为 `Release/win-x64/Demo/CompanionV01/index.html`；桌面原 Demo 快捷方式仍指向 CloudClub，可从顶部进入。

- 头顶聊天保留输入、发送、回复；移除日程入口与体验提示。八位角色问候如「我在。」「嗨！」「你好呀。」，保持简短。
- API 直接返回自然语言，不再附带行事历 JSON 合同。未接 API 的本机回复也不根据“提醒”等关键词劫持话题；均保留一秒思考与取消旧请求。
- 行事历功能与已有 Demo 日程继续保留。独立页面有专用输入、独立历史、可编辑确认卡；确认才保存，退出页面或切换角色取消迟到回复。日程回复不会写进普通聊天气泡。
- Claude Q运动服增加正面站立候选，短袖、短裤、圆眼镜、花饰和橙色长发；坐姿及其他 63 套外观素材不变。生成图片为透明 RGBA 原始文件，无局部拉伸。固定整图比例与站坐共同预留空间防止头发超出预览画布。
- `claude-review.html` 展示原装、旧侧身、候选和坐姿，并可叠图。候选比例与身份仍由用户目视决定，不把程序检查等同于美术批准。
- 画稿由内置 image_gen 生成，两轮提示词与参考来源在 `docs/demo/companion-v01/art/claude-sports-stand-v2.provenance.json`。第一稿保留在本地 `.artifacts/companion-v15-drafts`，不发布未选稿。

## 验证

隔离 Chrome file 页面，不访问个人浏览器配置；截图仅为生成的 Demo。所有 API 请求均截获模拟，真实付费调用 0。

- `tools/verify-simple-chat.cjs`：7 组，八位短问候、无日程聊天 UI、本机一秒思考、八家纯聊天、两套历史隔离、日程确认保存、取消迟到草稿、唯一素材覆盖、桌面/手机画面及原生文件不变。
- `tools/verify-companion-providers.cjs`：15 组，八家请求/鉴权/回复及工作提醒回归。
- `tools/verify-pet-agenda.cjs`：10 组，专用输入的八家 14 次模拟请求、确认/取消/修改/删除、周/月、补提醒、延后与重复。
- `tools/verify-idle-postures.cjs`：7 组，30 秒、60 秒边界、草稿/暂停/隐藏/减少动态保护、64 套姿态与手机显示。
- 证据目录：`.artifacts/companion-v15-review`、`companion-v15-providers`、`companion-v15-agenda`、`companion-v15-idle`。原图对照及桌面/手机 Demo 截图已目视检查。

## 范围与下一步

源码与 Demo 为 **v1.5**；桌面原生仍 **v1.4**。本轮新聊天和新站姿尚未安装到原生，不重打正式安装包，不上传 Gitee。原生 v1.4 的聊天仍有旧行事历入口，批准后再将该方案接入。源码按持续授权推送 GitHub，保留 v1.0 重要标签。

未来接入新站姿时须明确采用待审 `preview-art.js` 的候选元数据；当前原生导出对照仍按已批准的旧 `data.js` 读取，不得将它误用为本候选的批准证据。

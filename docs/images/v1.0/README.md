# v1.0 公开展示图

五张图片仅展示项目页面和合照，没有拍摄使用者桌面。

| 文件 | 内容 | 来源 |
| --- | --- | --- |
| `companions-q.png` | Q版伙伴、姿态与衣橱 | 独立 Demo 页面 |
| `companions-realistic.png` | 3D真人运动服伙伴 | 独立 Demo 页面 |
| `group-photo-q.png` | 八位 Q版原装合照 | Demo 合照画布导出 |
| `group-photo-realistic.png` | 八位3D真人运动服合照 | Demo 合照画布导出 |
| `calendar.png` | 月历打卡与收藏 | 独立 Demo 页面 |

使用 [capture-readme-showcase.cjs](../../../tools/capture-readme-showcase.cjs) 生成，依赖 Node.js、Playwright、Chrome 和已生成的 CompanionV01 Demo：

```powershell
./tools/install-companion-demo.ps1
node tools/capture-readme-showcase.cjs
```

每次运行新建无头浏览器上下文，不使用个人浏览器配置或原生存档。模型保持本机模式且密钥为空；阻断并检查外部 HTTP 请求；画面不包含地址栏、桌面、任务栏、其他窗口或账号资料。合照使用固定演示文字，日历只有本次隔离测试的打卡。截图前回到页面顶部并等待打卡提示结束。

图片经 PNG 元数据检查，不携带 EXIF、文本或压缩文本块。生成报告与页面文本只写入忽略目录 `.artifacts/v1.0-showcase`，不随公开图片提交。生成报告记录 SHA256、大小、页面错误与网络请求；本次五张图均通过检查并逐张查看。

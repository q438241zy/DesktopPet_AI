# 项目版本

用户于 2026-10-07 将项目版本重新定义为 **v0.1**。唯一版本来源是仓库根目录 `Version.props`。

- 对外名称格式：`云朵伙伴 / DesktopPet v<大版本>.<小版本>`。2026-10-09 用户明确将当时源码与 HTML Demo 定为 **v1.0 重要版本**。随后宠物行事历方案为 v1.1 待审 HTML Demo，已部署桌面程序仍为 v0.5；右上角不放版本标记，左下角显示各自实际构建版本。
- 大版本是 `DesktopPetMajor`，只有用户明确要求才能推进。
- 本次里程碑标签为 `v1.0`，不覆盖重置版本体系之前的历史 `v1.0.0` 标签。后续普通交付从 v1.1、v1.2 继续；下一次大版本仍由用户指定。
- 小版本是 `DesktopPetMinor`，每次交付的新一轮更新加一：v0.1 → v0.2 → v0.3，包括后续 Demo 修订。一次交付过程中修错、重跑验证不单独加号。
- .NET 包版本格式为 `<大版本>.<小版本>.0`，程序集/文件版本格式为 `<大版本>.<小版本>.0.0`，显示版本为 `<大版本>.<小版本>`；后两位是 Windows/.NET 格式所需，不另设互相冲突的 Preview 版本。
- 原生界面从程序集读取；HTML 数据由 `tools/build-companion-demo.py` 从同一文件导出；正式安装包由 exe 产品版本命名。不得手写多个当前版本。
- 2026-10-07 先完成 v0.1–v0.3 HTML 审阅；用户批准后，在 v0.4 同时交付月/周历 Demo 和已确认功能的原生接入。Demo、原生、协议与真实付费服务的验证结果分别记录，不能相互替代。
- 旧版本验证文档和用户数据备份保留其真实历史版本，旧正式安装包不改名冒充新版本。新的正式安装包仍须用户明确要求才制作，Gitee 继续暂停。

查询构建值：

```powershell
dotnet msbuild src/DesktopPet.App/DesktopPet.App.csproj -getProperty:Version,AssemblyVersion,FileVersion,InformationalVersion -nologo
```

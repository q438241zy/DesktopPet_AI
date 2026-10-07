# 项目版本

用户于 2026-10-07 将项目版本重新定义为 **v0.1**。唯一版本来源是仓库根目录 `Version.props`。

- 对外名称：`云朵伙伴 / DesktopPet v0.1`。
- 大版本是 `DesktopPetMajor`，只有用户明确要求才能推进。
- 小版本是 `DesktopPetMinor`，每次交付的新一轮更新加一：v0.1 → v0.2 → v0.3，包括后续 Demo 修订。一次交付过程中修错、重跑验证不单独加号。
- .NET 包版本为 0.1.0，程序集/文件版本为 0.1.0.0，显示版本为 0.1；后两位是 Windows/.NET 格式所需，不另设互相冲突的 Preview 版本。
- 原生界面从程序集读取；HTML 数据由 `tools/build-companion-demo.py` 从同一文件导出；正式安装包由 exe 产品版本命名。不得手写多个当前版本。
- 2026-10-07 这一轮先交付 v0.1 HTML Demo，日常运行的 Preview28 尚未换成新界面。用户批准设计后完成原生接入，按届时统一的小版本发布；不把 Demo 检查等同于原生发布。
- 旧版本验证文档和用户数据备份保留其真实历史版本，旧正式安装包不改名冒充新版本。新的正式安装包仍须用户明确要求才制作，Gitee 继续暂停。

查询构建值：

```powershell
dotnet msbuild src/DesktopPet.App/DesktopPet.App.csproj -getProperty:Version,AssemblyVersion,FileVersion,InformationalVersion -nologo
```

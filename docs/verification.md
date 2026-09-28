# Windows 驗證記錄

日期：2026-09-28。版本：1.2.0-preview.8。Windows x64、.NET SDK 10.0.302。

## 本輪修改

- 聊天輸入與回覆直接嵌在透明寵物視窗上方，不建立額外聊天視窗。Enter 發送、Shift+Enter 換行，可停止回覆。API 設定位於桌面偏好；未填 API 時使用本機預設多輪對話，金鑰只保留在本次進程。
- 本機與 API 共用同一個回覆門檻：請求與 1000 ms 思考計時同時開始；快速回覆等待滿一秒，慢速 API 不再額外等一秒。停止、換伙伴或關閉聊天會取消待回覆內容。
- 拖曳固定使用提起動作，移除拖曳途中的搖晃判定和鐘擺旋轉。Q 版原服的提起圖播放過渡後保持最後姿勢。
- 手動鬆手保持所選位置，距離底部 24 DIP 內吸附。散步／躲藏才主動自然落回地面，落地後才行走。重新抓住落下的角色會立即中止下落，不瞬移到底部。
- Q／3D／真人都依實際透明像素底界對齊腳底；Q 版睡姿也加入輕微呼吸。

## 本次通過的檢查

| 範圍 | 結果 |
|---|---:|
| 核心行為、本機及模擬 API 計時／取消 | 34 |
| 72 套外觀的 WPF 介面、衣服連貫性、步態與存檔 | 11830 |
| 72 套外觀拖曳放置、DeepSeek 九套頭頂聊天與自然下落 | 352 |
| DeepSeek 3D／真人六套姿勢與三輪聊天 | 156（每套 26） |

修正了舊測試在聊天 250 ms 換幀邊界前後固定假設第 0 幀的問題；現在核對當前幀仍來自正確服裝，並確認已離開行走狀態。這些測試透過 WPF 控件與程式互動入口執行，並非系統滑鼠注入。

日誌指標：`artifacts/current-placement-check.txt`、`artifacts/current-preview8-interface.txt`、`artifacts/current-preview8-poses.txt`。頭頂聊天截圖包含在 placement 目錄。

## DeepSeek 長期 HTML 與動作清單

`tools/demo.ps1` 使用當前桌面渲染器匯出 9 組 DeepSeek 外觀、210 段動作片段；Q／3D 不匯出舞蹈。固定 560×680 邏輯畫布、以 48 DPI 取樣，避免自動裁切把坐姿拉成站姿高度。片段每 100 ms 擷取一次並去除重複圖像，內嵌在可離線開啟的 HTML。

清單涵蓋 24 個角色 × 三套服裝 × 24 個行為，共 1728 筆。明確區分专用逐帧、专用姿势、程序动作、静态姿势、近似动作、缺少动作与不适用。Q 版確認仍有 272 個缺少動作組合與 32 個近似動作組合；本輪只稽核列明缺口，沒有新增或冒充這些美術。3D／真人的觸摸與舞蹈屬程式骨骼，不是新繪製逐幀圖。

HTML 提供三風格並排、換裝、時間軸、暫停、頭頂聊天展示與手動放置／自然落下對照，並可按八位伙伴切換完整清單和匯出 CSV。网页交互是獨立展示邏輯，不代替 Windows 滑鼠、跨螢幕或真實 API 驗收。

HTML 資料和 JavaScript 語法檢查通過，抽查了實際 WPF 匯出畫面。自動審核拒絕啟動本機預覽伺服器，回報 `blocked by policy`，所以尚未使用瀏覽器自動化驗收网页排版和拖曳。

## 重現

```powershell
./tools/check.ps1
./tools/demo.ps1
# 已建置的可執行檔也可單獨使用（必須用獨立資料目錄）：
DesktopPet.exe --verify-placement --data-dir <獨立目錄>
DesktopPet.exe --verify-interface --data-dir <獨立目錄>
DesktopPet.exe --verify-poses --appearance deepseek-adult-wedding --data-dir <獨立目錄>
DesktopPet.exe --export-demo --data-dir <獨立目錄>
```

前一版的全部新圖、進食及球類驗證保留於 [Preview 7 記錄](verification-preview7.md)。本輪未連接真實模型服務，也未使用使用者金鑰。

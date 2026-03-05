# NetFront — Claude 工作指引

## 專案概述
分散式訊息框架，提供用戶認證、Session 管理、Pub/Sub 通訊。
框架：**.NET 10**，無外部依賴（已移除 NetMQ）。

## 方案結構
```
NetFront.sln
├── NetFront\               # 核心庫
│   └── Transport\          # 自訂傳輸層（TCP + Inproc）
├── NetFront.Context\       # 服務端核心（BasicContext）
├── NetFront.SystemApi\     # System API 客戶端
└── NetFront.ClientApi\     # Client API 客戶端
```

## Transport 層（NetFront/Transport/）

### Wire Format
```
[uint16 frame_count][uint32 len1][data1][uint32 len2][data2]...
```

### 類別對應
| 類別 | 角色 |
|---|---|
| `TcpPubServer` | 服務端 Bind，主題路由，替換 XPublisherSocket |
| `TcpSubClient` | 客戶端 Connect，替換 XSubscriberSocket (TCP) |
| `InprocChannel` | 行程內通訊，替換 inproc:// |
| `FrameProtocol` | TCP 串流訊息框架編解碼 |
| `AddressParser` | 解析 tcp://host:port |

## 關鍵設計決策

- **TcpPubServer** 客戶端斷線時自動合成 FRONT_UNSUB frame：`[0x00, ...topic]`
- **InprocChannel** 使用靜態 `ConcurrentDictionary` 登錄表，Bind 建立，Connect 取得
- **BasicContext** 兩個 socket 各自獨立 msg 變數（sysMsg / clientMsg），避免競態
- **事件迴圈**：`Task.Delay(Timeout.Infinite, ct)` 替換 `NetMQPoller.Run()`
- **心跳計時器**：`PeriodicTimer` 替換 `NetMQTimer`
- **斷線**：`_runCts.Cancel()` 替換 `poller.Stop()`

## 注意事項

- `UserRoleEnum.CLIENT` 在原始碼中拼作 **CLINET**（錯字，維持原樣）
- `RspRoute()` 的 5-frame 訊息需用 `new byte[] { ... }` 而非 `[...]`（List 元素的 collection expression 歧義）
- `ProcesslChannel` 中的 l 是小寫 L（不是大寫 i）

## 建置與驗證
```bash
dotnet build NetFront.sln
# 期望：0 錯誤，0 警告
```

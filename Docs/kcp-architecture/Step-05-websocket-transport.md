# Step 05 — KCP over WebSocket 传输层现状

## 当前状态

`KcpWebSocketTransport` 已实现，并被 `#if ENABLE_GAME_FRAME_X_WEB_SOCKET` 包裹。该传输层依赖 `UnityWebSocket`，使用 WebSocket 消息边界承载 KCP 段，不额外添加 TCP 式长度前缀。

## 涉及文件

| 文件 | 当前状态 |
| --- | --- |
| `Runtime/Network/Network/Kcp/KcpWebSocketTransport.cs` | 已新增，条件编译 |

## 条件编译

整个文件被以下宏包裹：

```csharp
#if ENABLE_GAME_FRAME_X_WEB_SOCKET
...
#endif
```

`Runtime/GameFrameX.Network.Runtime.asmdef` 当前通过 `versionDefines` 为 UnityWebSocket 包定义 `ENABLE_GAME_FRAME_X_WEB_SOCKET`。

## 当前实现

### 类型

```csharp
[UnityEngine.Scripting.Preserve]
public class KcpWebSocketTransport : IKcpTransport
```

该类当前不是 `sealed`。

### URI 转换

当前转换逻辑：

- `kcp-ws` -> `ws`
- `kcp-wss` -> `wss`
- 其他 scheme 保持原值

正常调用路径中，`KcpNetworkChannel.InferServiceTypeFromScheme` 只会把 `kcp-ws` / `kcp-wss` 路由到 WebSocket 传输层。因此直接调用 `KcpWebSocketTransport.Connect()` 时，仍建议补充 unsupported scheme 校验。

### Connect

`Connect` 当前会：

1. 若已 Dispose，抛 `ObjectDisposedException`。
2. 若 `_webSocket` 已连接，直接返回。
3. 转换 URI scheme。
4. `new WebSocket(wsUrl)`。
5. 注册 `OnOpen`、`OnMessage`、`OnClose`、`OnError`。
6. 调用 `ConnectAsync()`。

连接成功由 `OnOpen` 触发 `_onConnected`。

### SendRaw

当前行为：

- 已 Dispose 时抛 `ObjectDisposedException`。
- WebSocket 为空或未连接时直接返回。
- `offset == 0 && count == data.Length` 时直接发送原数组。
- 否则拷贝 `offset/count` 片段后发送。

### 事件处理

| 事件 | 当前处理 |
| --- | --- |
| `OnOpen` | 触发 `_onConnected` |
| `OnMessage` | 仅当 `e.IsBinary` 时触发 `_onDataReceived(e.RawData, e.RawData.Length)` |
| `OnClose` | 触发 `_onClosed` |
| `OnError` | 打印错误日志，并触发 `_onClosed` |

### Close / Dispose

`Close()` 当前只在未 Dispose 时调用 `_webSocket.CloseAsync()`，不主动注销事件，也不清空回调。

`Dispose()` 会：

1. 标记 `_disposed = true`。
2. 注销 WebSocket 事件。
3. 调用 `CloseAsync()`。
4. 将 `_webSocket = null`。
5. 清空回调字段。

## 当前风险

- `Close()` 不注销事件，关闭异步完成后仍可能收到事件回调；最终是否重复触发取决于 UnityWebSocket 行为。
- `Connect()` 直接调用时没有校验 unsupported scheme。
- 尚无 WebSocket 相关单元测试或 Unity 集成测试。
- 宏关闭时，`ServiceType.KcpWebSocket` 创建 KCP 通道后连接阶段会因为无法创建传输而报错，该行为需要测试固定。

## 验收标准

- [x] 文件被 `#if ENABLE_GAME_FRAME_X_WEB_SOCKET` 包裹。
- [x] `kcp-ws` / `kcp-wss` 可转换为 `ws` / `wss`。
- [x] 实现 `IKcpTransport` 所有成员。
- [x] `OnOpen`、二进制 `OnMessage`、`OnClose`、`OnError` 已接入回调。
- [x] `SendRaw` 支持 `offset/count` 截取发送。
- [x] `OnMessage` 过滤文本消息。
- [x] `Dispose` 注销事件、关闭连接、清空回调。
- [x] 有 `[Preserve]` 标记。
- [ ] `Close` 是否也应注销事件，需要结合实际 UnityWebSocket 行为确认。
- [ ] 需要补充 WebSocket 宏开启/关闭两种编译和行为测试。

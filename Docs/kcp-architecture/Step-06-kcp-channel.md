# Step 06 — KCP 网络通道核心现状

## 当前状态

`KcpNetworkChannel` 已实现为 `NetworkManager` 的私有嵌套类：

```csharp
private sealed class KcpNetworkChannel : NetworkChannelBase
```

它继承现有 `NetworkChannelBase`，但重写了 `Connect`、`Close`、`Shutdown`、`Update` 和 `ProcessSend`，将 KCP 协议层与 `IKcpTransport` 组装起来。

## 涉及文件

| 文件 | 当前状态 |
| --- | --- |
| `Runtime/Network/Network/Kcp/NetworkManager.KcpNetworkChannel.cs` | 已新增 |

## 构造函数

当前有两个构造函数：

```csharp
public KcpNetworkChannel(
    string name,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout,
    ServiceType serviceType,
    KcpConfig kcpConfig)
```

用于显式指定 `ServiceType`。

```csharp
public KcpNetworkChannel(
    string name,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout,
    KcpConfig kcpConfig)
```

用于 URI scheme 自动推断。此时 `m_ServiceType = null`，在 `Connect` 时根据 `address.Scheme` 推断。

两个构造函数都会：

- 保存 `KcpConfig`，为空时创建默认配置。
- 初始化 `ConcurrentQueue<PooledBuffer>` 原始接收队列。
- 初始化 KCP 接收缓冲和发送 `MemoryStream`。

## Connect 流程

当前流程：

1. 如果 `PIsConnecting` 为 true，直接返回。
2. 如果已有 `PSocket`，先 `Close(NetworkCloseReason.ConnectClose, DisposeError)`。
3. `ValidateKcpConfig()`。
4. 设置 `PIsConnecting = true` 并保存 `userData`。
5. 重置 KCP 关闭标记、发送状态、接收状态。
6. 根据显式 `ServiceType` 或 URI scheme 得到有效服务类型。
7. `CreateTransport(effectiveServiceType)`。
8. 为 Transport 注册 `OnTransportConnected`、`OnTransportDataReceived`、`OnTransportClosed`。
9. 创建 `new Kcp(m_KcpConfig.ConversationId, OnKcpOutput)`。
10. 设置 KCP no-delay、MTU、窗口大小。
11. 调用 `PNetworkChannelHelper.PrepareForConnecting()`。
12. 设置 `PSocket = new KcpNetworkSocket(m_Transport)`。
13. 清理发送队列、心跳状态和统计计数。
14. 调用 `m_Transport.Connect(address)`。

连接状态由 Transport 回调决定。`OnTransportConnected()` 才会设置 `PActive = true`、`PIsConnecting = false` 并触发 `NetworkChannelConnected`。

## URI 推断

当前使用不区分大小写的 scheme：

| Scheme | 推断结果 |
| --- | --- |
| `kcp` | `ServiceType.KcpUdp` |
| `kcp-tcp` | `ServiceType.KcpTcp` |
| `kcp-ws` | `ServiceType.KcpWebSocket` |
| `kcp-wss` | `ServiceType.KcpWebSocket` |

其他 scheme 抛 `GameFrameworkException`。

## Transport 创建

当前映射：

| ServiceType | Transport |
| --- | --- |
| `Kcp` | `KcpUdpTransport((int)m_KcpConfig.Mtu)` |
| `KcpUdp` | `KcpUdpTransport((int)m_KcpConfig.Mtu)` |
| `KcpTcp` | `KcpTcpTransport(m_KcpConfig.MaxMessageSize)` |
| `KcpWebSocket` | `KcpWebSocketTransport()`，仅在 `ENABLE_GAME_FRAME_X_WEB_SOCKET` 下编译 |

宏关闭时，`KcpWebSocket` 会落入默认分支并返回 null。`Connect` 会触发 `NetworkChannelError` 或抛出异常。

## Update 顺序

当前 `Update` 仅在 `PActive` 时执行，顺序为：

1. `ProcessRawReceive()`：从 `ConcurrentQueue<PooledBuffer>` 取原始传输数据，调用 `m_Kcp.Input(...)`，随后归还池化缓冲。
2. `m_Kcp.Update(currentMs)`：驱动 ACK、重传等 KCP 定时逻辑。
3. `ProcessKcpReceive()`：使用 `PeekSize()` / `Receive()` 取完整 KCP 消息。
4. `ProcessSend()`：序列化待发送消息并调用 `m_Kcp.Send(...)`。
5. `ProcessHeartBeat(realElapseSeconds)`：复用基类心跳逻辑。
6. `ProcessReceivedMessage()`：复用基类消息分发。
7. `PRpcState.Update(elapseSeconds, realElapseSeconds)`：复用 RPC 超时更新。

## 接收处理

`OnTransportDataReceived` 会把 Transport 回调传入的数据拷贝到池化缓冲：

```csharp
m_RawReceiveQueue.Enqueue(PooledBuffer.CopyFrom(data, 0, length));
```

`ProcessRawReceive` 消费后会在 `finally` 中 `Dispose()` 该缓冲，把底层数组归还 `ArrayPool<byte>.Shared`。`Close` 清空原始接收队列时也会逐个归还池化缓冲。

`ProcessKcpReceive` 会：

1. 调用 `m_Kcp.PeekSize()`。
2. 若消息大小超过 `MaxMessageSize`，触发 `ReceiveError`。
3. 复用或扩容 `m_KcpReceiveBuffer`。
4. 调用 `m_Kcp.Receive(m_KcpReceiveBuffer, msgSize)`。
5. 重置心跳状态。
6. 增加接收统计。
7. 调用 `ProcessKcpMessage(...)`。

`ProcessKcpMessage` 当前复用现有包头/包体处理：

- `PNetworkChannelHelper.DeserializePacketHeader(data)`
- 根据 `PacketReceiveHeaderHandler.PacketLength` 计算 body 长度
- 如果 `ZipFlag != 0`，调用 `MessageDecompressHandler`
- `PNetworkChannelHelper.DeserializePacketBody(...)`
- 成功后设置 `UniqueId`、记录日志、入队 `m_ExecutionMessageQueue`

## 发送处理

`ProcessSend` 当前在 `lock (PSendPacketPool)` 中循环发送：

1. 记录发送日志。
2. 清空并复用 `m_SendMemoryStream`。
3. 调用 `SerializePacketHeader` 和 `SerializePacketBody`。
4. 释放 `MessageObject` 引用。
5. 检查 `m_Kcp` 是否可用。
6. `m_SendMemoryStream.ToArray()`。
7. 检查 `MaxMessageSize`。
8. 调用 `m_Kcp.Send(sendBuffer, 0, sendBuffer.Length)`。
9. 增加发送统计。
10. finally 中移除发送队列首节点。

`OnKcpOutput` 会拷贝 KCP 输出缓冲后调用 `m_Transport.SendRaw(...)`，避免持有 KCP 内部复用缓冲。

## Close / Shutdown

`Close(reason, code)` 当前使用 `lock (this)`，并通过 `m_KcpClosed` 防止重复关闭。

关闭时会：

- 设置 `PActive = false`、`PIsConnecting = false`。
- 清空 `m_Kcp`。
- 调用 `m_Transport.Close()`。
- 清空原始接收队列、发送队列、执行消息队列。
- 重置统计、心跳和 RPC 状态。
- 触发 `NetworkChannelClosed`。
- 设置 `PSocket = null`。

`Shutdown()` 会调用 `Close(NetworkCloseReason.Normal)`，随后 `Dispose` Transport、释放发送流，并关闭 helper。

## KcpNetworkSocket

`KcpNetworkSocket` 是 `INetworkSocket` 的轻量适配器：

- `IsConnected` 委托给 `IKcpTransport.IsConnected`。
- `IsClosed` 为 Transport 为空或未连接。
- `LocalEndPoint` / `RemoteEndPoint` 当前返回 null。
- `Shutdown()` / `Close()` 调用 `m_Transport.Close()`。

## 配置校验

当前 `ValidateKcpConfig()` 校验：

- `Mtu >= 50`
- `Mtu <= 65535`
- `SendWindowSize != 0`
- `ReceiveWindowSize != 0`
- `MaxMessageSize > 0`

## 当前风险和待完善项

- `ProcessSend` 在序列化后立即 `ReferencePool.Release(messageObject)`，如果后续 `m_Kcp.Send` 失败，消息不会保留重试。
- `KcpNetworkSocket` 不暴露本地/远端端点，依赖这些属性的诊断工具无法获取地址。
- `KcpNetworkChannel` 是私有嵌套类，无法在裁剪辅助类中直接引用类型。
- `Close` 中调用 `m_Transport.Close()` 会触发 Transport 关闭回调，但 `m_KcpClosed` 能阻止递归重复关闭；仍建议补充测试固定该行为。
- KCP 通道完整收发、心跳、RPC 仍缺少端到端测试。
- 当前只把跨线程原始接收队列接入 `PooledBuffer`，TCP/UDP Transport 内部接收缓冲、KCP 输出拷贝和 `MemoryStream.ToArray()` 仍有后续池化空间。

## 验收标准

- [x] 有显式 `ServiceType` 和 URI 自动推断两个构造路径。
- [x] `Connect` 支持 URI scheme 推断。
- [x] `Update` 顺序已按 RawReceive -> Kcp.Update -> KcpReceive -> Send -> HeartBeat -> MessageDispatch -> RpcUpdate 执行。
- [x] `OnTransportConnected` 设置通道激活并触发连接事件。
- [x] `ProcessSend` 和 `ProcessKcpReceive` 都检查 `MaxMessageSize`。
- [x] `OnKcpOutput` 拷贝数据后发送。
- [x] `ValidateKcpConfig` 覆盖 MTU、窗口和消息大小。
- [x] `Close` 使用 `lock (this)` 和 `m_KcpClosed` 防重复。
- [x] `KcpNetworkSocket` 实现 `INetworkSocket`。
- [x] `KcpNetworkChannel` 有 `[Preserve]` 标记。
- [x] 原始接收队列使用 `PooledBuffer` 管理跨线程数据生命周期。
- [ ] 需要补充 KCP 端到端消息、心跳、RPC、关闭幂等测试。

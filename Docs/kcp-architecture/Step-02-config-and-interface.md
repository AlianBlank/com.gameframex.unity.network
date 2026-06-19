# Step 02 — 配置类、传输层接口、ServiceType 现状

## 当前状态

`KcpConfig`、`IKcpTransport` 和 `ServiceType` 扩展已落地。当前实现保持简单的数据配置，不包含心跳、超时或自动会话分配逻辑。

## 涉及文件

| 文件 | 当前状态 |
| --- | --- |
| `Runtime/Network/Network/Kcp/KcpConfig.cs` | 已新增 |
| `Runtime/Network/Network/Kcp/IKcpTransport.cs` | 已新增 |
| `Runtime/Network/Base/ServiceType.cs` | 已扩展 |

## KcpConfig

当前定义：

```csharp
[Serializable]
[UnityEngine.Scripting.Preserve]
public sealed class KcpConfig
```

字段：

| 字段 | 类型 | 默认值 | 当前用途 |
| --- | --- | --- | --- |
| `NoDelay` | `bool` | `true` | 传入 `Kcp.SetNoDelay` |
| `Interval` | `uint` | `10` | KCP update 间隔，毫秒 |
| `Resend` | `int` | `2` | 快速重传阈值 |
| `NoCongestionControl` | `bool` | `true` | 是否关闭拥塞控制 |
| `Mtu` | `uint` | `1200` | 传入 `Kcp.SetMtu`，也用于构造 `KcpUdpTransport` 接收缓冲下限 |
| `SendWindowSize` | `uint` | `256` | 发送窗口 |
| `ReceiveWindowSize` | `uint` | `256` | 接收窗口 |
| `MaxMessageSize` | `int` | `256 * 1024` | KCP 单条消息上限，也用于 TCP 帧上限 |
| `ConversationId` | `uint` | `0` | 直接传入 `new Kcp(conversationId, output)` |

注意：

- `ConversationId = 0` 当前不是“自动分配”，只是默认传入 KCP 的会话 ID。多通道场景必须为每个通道显式配置不同值（且与服务端约定）。若后续需要自动分配，需要补充实现。
- `KcpConfig` **不包含**心跳相关字段（`HeartBeatInterval` / `MissHeartBeatCountByClose`）。KCP 通道的心跳完全复用 `NetworkChannelBase.ProcessHeartBeat`，间隔由 `IPacketHeartBeatHandler.HeartBeatInterval` 或基类默认值（30s）决定。

## IKcpTransport

当前接口：

```csharp
public interface IKcpTransport : IDisposable
{
    bool IsConnected { get; }
    void Connect(Uri address);
    void SendRaw(byte[] data, int offset, int count);
    void Close();
    void SetOnConnected(Action onConnected);
    void SetOnDataReceived(Action<byte[], int> onDataReceived);
    void SetOnClosed(Action onClosed);
}
```

回调线程由具体传输层决定。`KcpNetworkChannel` 会在 `OnTransportDataReceived` 中拷贝数据到 `PooledBuffer` 并放入 `ConcurrentQueue<PooledBuffer>`，确保 KCP 实例只在通道 `Update()` 中被访问。

## ServiceType

当前枚举：

| 枚举值 | 数值 | 当前路由 |
| --- | ---: | --- |
| `Tcp` | 0 | Legacy TCP |
| `TcpWithSyncReceive` | 1 | 当前创建工厂默认仍走 legacy 分支 |
| `Udp` | 2 | 当前创建工厂默认仍走 legacy 分支 |
| `Kcp` | 3 | `KcpNetworkChannel`，底层映射为 `KcpUdpTransport` |
| `WebSocket` | 4 | 宏条件下 legacy WebSocket，否则默认 TCP |
| `KcpUdp` | 5 | `KcpNetworkChannel` + `KcpUdpTransport` |
| `KcpTcp` | 6 | `KcpNetworkChannel` + `KcpTcpTransport` |
| `KcpWebSocket` | 7 | `KcpNetworkChannel` + `KcpWebSocketTransport`（需 WebSocket 宏） |

## 验收标准

- [x] `KcpConfig` 有 `[Serializable]` 和 `[Preserve]` 标记。
- [x] `KcpConfig` 包含 9 个字段且默认值符合测试。
- [x] `IKcpTransport` 继承 `IDisposable`。
- [x] `IKcpTransport` 包含 `Connect`、`SendRaw`、`Close`。
- [x] `IKcpTransport` 包含连接、接收、关闭三个回调设置方法。
- [x] `ServiceType` 新增 `KcpUdp=5`、`KcpTcp=6`、`KcpWebSocket=7`。
- [x] `ServiceType.Kcp=3` 保持不变，并兼容为 KCP over UDP。
- [x] 已有单元测试覆盖 `KcpConfig` 默认值和部分工厂路径。
- [ ] `KcpWebSocket` 宏关闭时的工厂行为需要补充测试。

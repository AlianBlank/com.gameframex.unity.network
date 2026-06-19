# KCP 网络架构方案（已对齐当前代码）

## 当前状态

本目录描述 GameFrameX Unity Network 包中 KCP 通道的现有实现和后续完善项。当前代码已经引入 kcp2k，并新增 `KcpNetworkChannel`、`KcpConfig`、`IKcpTransport` 以及 UDP/TCP/WebSocket 三种 KCP 传输层。

方案不再按“从零实施”理解，而应按“现状核对 + 剩余验证/修正清单”执行。

## 目标架构

上层仍复用现有 `MessageObject`、包头/包体序列化、心跳、RPC 和事件分发管线；新增的 KCP 通道只替换连接、收发和底层传输部分。

```
Application Layer
MessageObject / Packet Header / Serialization / HeartBeat / RPC
        |
KCP Protocol Layer
NetworkManager.KcpNetworkChannel (private nested class)
        |
IKcpTransport
        |
UDP / TCP / WebSocket
```

## URI Scheme 映射

| Scheme | ServiceType | Transport | 说明 |
| --- | --- | --- | --- |
| `kcp://` | `KcpUdp` | `KcpUdpTransport` | UDP 数据报承载 KCP 段 |
| `kcp-tcp://` | `KcpTcp` | `KcpTcpTransport` | 4 字节大端长度前缀承载 KCP 段 |
| `kcp-ws://` | `KcpWebSocket` | `KcpWebSocketTransport` | 转换为 `ws://`，仅在 `ENABLE_GAME_FRAME_X_WEB_SOCKET` 下编译 |
| `kcp-wss://` | `KcpWebSocket` | `KcpWebSocketTransport` | 转换为 `wss://`，仅在 `ENABLE_GAME_FRAME_X_WEB_SOCKET` 下编译 |
| 显式 `ServiceType.Kcp` | `Kcp` | `KcpUdpTransport` | 兼容旧枚举值，当前等价于 KCP over UDP |

## 当前 API

`INetworkManager` 层当前有 3 个创建入口，均带 `rpcTimeout`：

```csharp
INetworkChannel CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, int rpcTimeout);

INetworkChannel CreateNetworkChannel(
    string channelName,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout,
    ServiceType serviceType,
    KcpConfig kcpConfig);

INetworkChannel CreateNetworkChannel(
    string channelName,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout,
    KcpConfig kcpConfig);
```

`NetworkComponent` 层隐藏 `rpcTimeout`，并在创建后调用 `SetIgnoreLogNetworkIds`：

```csharp
CreateNetworkChannel(name, helper);
CreateNetworkChannel(name, helper, serviceType, kcpConfig);
CreateNetworkChannel(name, helper, kcpConfig);
```

## 数据流

- 发送：`MessageObject` -> `SerializePacketHeader/SerializePacketBody` -> `Kcp.Send()` -> `OnKcpOutput` -> `IKcpTransport.SendRaw()`
- 接收：Transport 回调 -> `PooledBuffer.CopyFrom(...)` -> `ConcurrentQueue<PooledBuffer>` -> 主线程 `Kcp.Input()` -> 归还池化缓冲 -> `Kcp.Receive()` -> `DeserializePacketHeader/DeserializePacketBody` -> `m_ExecutionMessageQueue`

KCP 实例只在 `KcpNetworkChannel.Update()` 主线程流程中执行 `Input/Update/Receive/Send`。Transport 接收回调只负责把数据拷贝到池化缓冲并入队，消费和关闭路径负责归还缓冲。

## 已实现文件

```
Runtime/Network/Network/Kcp/
├── ThirdParty/kcp2k/
│   ├── Kcp.cs
│   ├── Segment.cs
│   ├── Utils.cs
│   ├── Pool.cs
│   └── THIRD_PARTY_NOTICES.md
├── IKcpTransport.cs
├── KcpConfig.cs
├── KcpUdpTransport.cs
├── KcpTcpTransport.cs
├── KcpWebSocketTransport.cs
├── PooledBuffer.cs
└── NetworkManager.KcpNetworkChannel.cs
```

## 已修改文件

| 文件 | 当前变更 |
| --- | --- |
| `Runtime/Network/Base/ServiceType.cs` | 新增 `KcpUdp=5`、`KcpTcp=6`、`KcpWebSocket=7`，保留 `Kcp=3` |
| `Runtime/Network/Interface/INetworkManager.cs` | 新增显式 KCP ServiceType 和 URI 自动推断重载 |
| `Runtime/Network/Network/NetworkManager.cs` | 将 `Kcp/KcpUdp/KcpTcp/KcpWebSocket` 路由到 `KcpNetworkChannel` |
| `Runtime/Network/NetworkComponent.cs` | 新增 KCP 创建重载并透传日志过滤配置 |
| `Runtime/GameFrameXNetworkCroppingHelper.cs` | 引用 `IKcpTransport`、`KcpConfig`、`KcpTcpTransport`、`KcpUdpTransport`、条件引用 `KcpWebSocketTransport` |
| `Tests/UnitTests.cs` | 新增部分 KCP 工厂、配置、UDP 连接回调、TCP 帧协议测试 |

## 当前已知限制

- `KcpConfig.ConversationId = 0` 默认会话 ID，无自动分配；多通道场景需为每个通道显式配置不同值（与服务端约定）。
- `KcpNetworkChannel` 是 `NetworkManager` 的私有嵌套类，类本身已标 `[Preserve]`；`CroppingHelper` 通过引用 `NetworkManager` 和公开 KCP 类型（`IKcpTransport` / `KcpConfig` 等）保证不被裁剪。
- `NetworkChannelBase` 没有 `NetworkChannelSent` 事件；当前 KCP 架构不依赖该事件，如需发送成功通知可作为未来增强。
- TCP 接收使用固定长度 `BeginReceive`，一次只读取当前状态所需字节；多帧连续到达时由下一次 `BeginReceive` 处理。
- KCP 心跳复用 `NetworkChannelBase` / `IPacketHeartBeatHandler`，不由 `KcpConfig` 配置；间隔由 helper 的 `HeartBeatInterval` 决定。

## 文档索引

| 序号 | 文档 | 内容 |
| --- | --- | --- |
| 01 | [Step-01-kcp2k-integration.md](Step-01-kcp2k-integration.md) | kcp2k 源码现状 |
| 02 | [Step-02-config-and-interface.md](Step-02-config-and-interface.md) | `KcpConfig`、`IKcpTransport`、`ServiceType` |
| 03 | [Step-03-udp-transport.md](Step-03-udp-transport.md) | UDP 传输层 |
| 04 | [Step-04-tcp-transport.md](Step-04-tcp-transport.md) | TCP 帧协议传输层 |
| 05 | [Step-05-websocket-transport.md](Step-05-websocket-transport.md) | WebSocket 传输层 |
| 06 | [Step-06-kcp-channel.md](Step-06-kcp-channel.md) | KCP 通道核心 |
| 07 | [Step-07-api-integration.md](Step-07-api-integration.md) | API 集成 |
| 08 | [Step-08-verify.md](Step-08-verify.md) | 已有测试和待验证项 |
| 报告 | [Test-Report.md](Test-Report.md) | Unity 2022.3.62f2 测试验收结果 |

# Step 07 — API 层集成现状

## 当前状态

`INetworkManager`、`NetworkManager` 和 `NetworkComponent` 已暴露 KCP 通道创建能力。现有无参 ServiceType 的 legacy 创建入口仍保留。

## 涉及文件

| 文件 | 当前状态 |
| --- | --- |
| `Runtime/Network/Interface/INetworkManager.cs` | 已新增 KCP 重载 |
| `Runtime/Network/Network/NetworkManager.cs` | 已实现工厂路由 |
| `Runtime/Network/NetworkComponent.cs` | 已透传组件层 API |
| `Runtime/GameFrameXNetworkCroppingHelper.cs` | 已补充 KCP 类型引用 |

## INetworkManager API

当前接口包含：

```csharp
INetworkChannel CreateNetworkChannel(
    string channelName,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout);
```

Legacy 入口，当前实现转发到：

```csharp
CreateNetworkChannel(channelName, networkChannelHelper, rpcTimeout, ServiceType.Tcp, null);
```

显式服务类型入口：

```csharp
INetworkChannel CreateNetworkChannel(
    string channelName,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout,
    ServiceType serviceType,
    KcpConfig kcpConfig);
```

URI 自动推断入口：

```csharp
INetworkChannel CreateNetworkChannel(
    string channelName,
    INetworkChannelHelper networkChannelHelper,
    int rpcTimeout,
    KcpConfig kcpConfig);
```

## NetworkManager 工厂路由

当前显式 `ServiceType` 分支：

| ServiceType | 创建结果 |
| --- | --- |
| `Kcp` | `KcpNetworkChannel` |
| `KcpUdp` | `KcpNetworkChannel` |
| `KcpTcp` | `KcpNetworkChannel` |
| `KcpWebSocket` | `KcpNetworkChannel` |
| 其他，WebSocket/WebGL 宏成立 | `WebSocketNetworkChannel` |
| 其他 | `SystemTcpNetworkChannel` |

URI 自动推断重载直接创建：

```csharp
new KcpNetworkChannel(channelName, networkChannelHelper, rpcTimeout, kcpConfig);
```

此时底层 Transport 延迟到 `Connect(address)` 时按 URI scheme 推断。

## 事件注册

事件订阅/退订已抽取到 `RegisterChannelEvents` / `UnregisterChannelEvents` 两个私有方法，被以下四处调用：

- `CreateNetworkChannel(name, helper, rpcTimeout, ServiceType, KcpConfig)` — 创建后订阅
- `CreateNetworkChannel(name, helper, rpcTimeout, KcpConfig)` — 创建后订阅
- `DestroyNetworkChannel(name)` — 销毁前退订
- `Shutdown()` — 管理器关闭时退订所有

注册的事件：

- `NetworkChannelConnected`
- `NetworkChannelClosed`
- `NetworkChannelMissHeartBeat`
- `NetworkChannelError`

`NetworkChannelBase` 当前没有 `NetworkChannelSent` 事件（发送成功通知）。如需该事件作为未来增强，需要在基类新增字段并由各通道在发送完成时触发；当前 KCP 架构不依赖该事件。

## NetworkComponent API

组件层当前暴露：

```csharp
CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper)
CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, ServiceType serviceType, KcpConfig kcpConfig = null)
CreateNetworkChannel(string channelName, INetworkChannelHelper networkChannelHelper, KcpConfig kcpConfig)
```

每个方法都会：

1. 校验 `channelName`。
2. 调用 `m_NetworkManager.CreateNetworkChannel(...)`。
3. 调用 `networkChannel.SetIgnoreLogNetworkIds(...)`。
4. 返回创建的 channel。

## CroppingHelper

当前已引用：

- `IKcpTransport`
- `KcpConfig`
- `KcpTcpTransport`
- `KcpUdpTransport`
- `KcpWebSocketTransport`（仅在 `ENABLE_GAME_FRAME_X_WEB_SOCKET` 下）

没有引用 `KcpNetworkChannel`，因为该类是 `NetworkManager` 私有嵌套类，外部不能直接 `typeof`。

## 当前测试覆盖

- legacy 创建和销毁 channel。
- 重复 channelName 抛异常。
- `ServiceType.Kcp` 创建 `KcpNetworkChannel`。
- `CreateNetworkChannel(..., KcpConfig)` 创建 `KcpNetworkChannel`。
- KCP 非法配置在 Connect 前阻止打开传输。

## 待完善项

- 补充 `ServiceType.KcpUdp`、`KcpTcp`、`KcpWebSocket` 的工厂测试。
- 补充 URI 自动推断失败测试，例如 `http://` 应抛 `GameFrameworkException`。
- 补充 WebSocket 宏关闭时 `KcpWebSocket` 的错误路径测试。
- 如果需要公共诊断 KCP 通道类型，需考虑将 `KcpNetworkChannel` 从私有嵌套类调整为可见类型，或提供公开属性。

## 验收标准

- [x] `INetworkManager` 有 legacy、显式 ServiceType、URI 自动推断 3 个创建入口。
- [x] `NetworkManager` 实现 3 个创建入口。
- [x] `NetworkComponent` 有 3 个对应入口，并设置忽略日志 ID。
- [x] legacy 调用仍创建原有 TCP/WebSocket 分支，不强制进入 KCP。
- [x] 显式 KCP ServiceType 调用创建 `KcpNetworkChannel`。
- [x] URI 自动推断重载创建 `KcpNetworkChannel`。
- [x] `CroppingHelper` 包含外部可引用的 KCP 类型。
- [x] `KcpWebSocketTransport` 在 `#if ENABLE_GAME_FRAME_X_WEB_SOCKET` 中引用。
- [ ] Unity 编译通过仍需在 Unity 环境中验证。

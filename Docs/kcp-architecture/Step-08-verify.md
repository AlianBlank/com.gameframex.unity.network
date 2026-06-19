# Step 08 — 验证现状与待补齐清单

## 当前状态

当前仓库已有 EditMode 单元测试覆盖 KCP 的部分基础路径，但尚未覆盖完整端到端收发、WebSocket、心跳和 RPC。该步骤用于区分“已覆盖”和“仍需验证”。

## 已有测试文件

| 文件 | 说明 |
| --- | --- |
| `Tests/UnitTests.cs` | 包含 NetworkManager 基础测试和部分 KCP 测试 |
| `Tests/Editor/NetworkFunctionalTestRunner.cs` | Unity Editor TestRunner 执行入口，输出 XML 和 summary |
| `Tests/Editor/NetworkBuildValidationRunner.cs` | StandaloneOSX Player Build 验证入口，使用 `.app` 输出路径 |
| `Tests/GameFrameX.Network.Tests.asmdef` | Editor 测试程序集 |

## 已覆盖场景

### NetworkManager 基础行为

- [x] 创建和销毁 channel。
- [x] 查询 channel 数量和列表。
- [x] 重复 channelName 抛 `GameFrameworkException`。

### KCP 工厂和配置

- [x] `ServiceType.Kcp` 创建 `KcpNetworkChannel`。
- [x] `ServiceType.KcpUdp`、`KcpTcp`、`KcpWebSocket` 创建 `KcpNetworkChannel`。
- [x] URI 自动推断入口创建 `KcpNetworkChannel`。
- [x] 非法 `KcpConfig.Mtu` 在打开传输前抛异常。
- [x] 非法发送窗口、接收窗口和消息大小在打开传输前抛异常。
- [x] `KcpConfig` 默认值稳定。
- [x] `PooledBuffer` 拷贝数据后不受源数组修改影响。
- [x] `PooledBuffer.Dispose()` 可重复调用，释放后访问会抛 `ObjectDisposedException`。

### UDP 基础行为

- [x] `KcpUdpTransport.Connect(kcp://127.0.0.1:9)` 触发连接回调。
- [x] `KcpUdp` channel connect 后触发 `NetworkConnected`。
- [x] `KcpUdpTransport.Close()` 重复调用只触发一次关闭回调。

### TCP 帧协议基础行为

- [x] `KcpTcpTransport` 拒绝非法 `maxFrameSize`。
- [x] 可接收 4 字节大端长度前缀帧。
- [x] `SendRaw` 会发送 4 字节大端长度前缀。
- [x] 可接收拆分的长度头和 body。
- [x] 可连续接收多个长度前缀帧。
- [x] 超过 `maxFrameSize` 的帧会关闭连接。

### kcp2k 基础行为

- [x] 当前测试覆盖 KCP 在小 MTU 下发送过大消息时抛异常的路径。

## 未覆盖但必须补齐的验证

### 编译验证

- [x] Unity 2022.3.62f2 下 `dotnet build /Users/mac/Documents/GithubWorks/GameFrameX.Unity/GameFrameX.Network.Tests.csproj --no-restore` 通过，0 错误。
- [x] Unity 2022.3.62f2 StandaloneOSX 目标 EditMode 编译和测试通过，28/0。
- [x] Unity 2022.3.62f2 WebGL 目标 EditMode 编译和测试通过，28/0。
- [x] WebGL 条件分支下 legacy WebSocket 行为不受影响。
- [x] Unity 2022.3.62f2 StandaloneOSX IL2CPP Player Build 在临时关闭 `HybridCLRSettings.enable` 后通过。
- [ ] Unity 2019.4+ 下无编译错误。
- [ ] `ENABLE_GAME_FRAME_X_WEB_SOCKET` 关闭配置可编译。
- [ ] Mono 后端构建。
- [ ] 默认 HybridCLR 启用配置下的完整 Player Build：当前 StandaloneOSX 被项目级 HybridCLR 未初始化阻塞，非 KCP 编译错误。

### 向后兼容

- [x] `CreateNetworkChannel(name, helper)` 仍保持原有 TCP/WebSocket 分支行为。
- [ ] 原有 TCP 通道 Connect / Send / Receive / Close 行为不变。
- [ ] 原有 TCP 心跳和 RPC 行为不变。
- [ ] 原有 WebSocket 通道在宏开启时行为不变。

### KCP over UDP

- [ ] 建立真实 UDP echo 服务端，验证 `kcp://host:port` 完整连接、发送、接收、关闭。
- [ ] 验证 UDP 收包后 KCP `Input/Receive` 能重组出完整 GameFrameX 消息。
- [ ] 验证关闭后 Socket 资源释放。
- [x] 验证 UDP Transport 重复关闭不会重复触发关闭回调。
- [ ] 验证 Channel 关闭事件不会重复造成状态异常。

### KCP over TCP

- [x] 验证 `SendRaw` 发送 4 字节大端长度前缀。
- [x] 验证长度头拆包。
- [x] 验证 body 拆包。
- [x] 验证连续多帧到达时逐帧回调。
- [ ] 验证连接失败、发送失败、对端主动关闭。
- [ ] 验证 `kcp-tcp://host:port` 完整 KCP 消息收发。

### KCP over WebSocket

- [ ] 宏开启时验证 `kcp-ws://` -> `ws://`。
- [ ] 宏开启时验证 `kcp-wss://` -> `wss://`。
- [ ] 验证二进制消息触发 KCP 输入，文本消息被忽略。
- [ ] 验证 `Close()` / `Dispose()` 后不会重复回调或泄漏事件。
- [ ] 宏关闭时验证 `ServiceType.KcpWebSocket` 和 `kcp-ws://` 错误路径明确。

### 错误处理

- [x] URI 自动推断入口连接 `http://` 等不支持 scheme 时抛 `GameFrameworkException`。
- [ ] `MaxMessageSize` 超限发送触发 `NetworkChannelError`。
- [ ] `MaxMessageSize` 超限接收触发 `NetworkChannelError`。
- [ ] 连接断开后发送不导致未处理异常。
- [ ] 重复 `Connect` 防重入行为稳定。
- [x] 非法窗口大小和非法消息大小抛异常。

### 心跳

KCP 心跳复用 `NetworkChannelBase` 和 `IPacketHeartBeatHandler`，不是 `KcpConfig` 字段。

- [x] 心跳间隔到达后调用 `PNetworkChannelHelper.SendHeartBeat()`。
- [ ] KCP 收到数据后按 `ResetHeartBeatElapseSecondsWhenReceivePacket` 重置心跳状态。
- [ ] 心跳丢失超过 `MissHeartBeatCountByClose` 后关闭连接。
- [ ] `NetworkMissHeartBeat` 事件正常抛出。

### RPC

- [ ] `Call<T>` 请求和响应能通过 KCP 通道完整往返。
- [ ] RPC 超时触发预期错误路径。
- [ ] 并发 RPC 响应可正确匹配请求序列。
- [ ] 通道关闭后 RPC 状态被重置，不泄漏等待项。

## 建议优先级

1. 先补 Unity 编译验证，分别覆盖 WebSocket 宏开启/关闭。
2. 补 TCP 帧协议单测，因为它不依赖真实 KCP 服务端，成本低。
3. 补 URI 错误路径和工厂路由测试。
4. 搭建 UDP/TCP KCP echo 集成测试。
5. 最后补 WebSocket、心跳和 RPC 端到端测试。

## 本次文档核对结论

当前 KCP 架构代码已经具备主体能力，并已在 Unity 2022.3.62f2 的 StandaloneOSX 与 WebGL 目标下通过 EditMode 编译/测试。StandaloneOSX IL2CPP Player Build 在临时关闭 `HybridCLRSettings.enable` 后通过，说明 KCP 代码可进入真实 Player 构建链路；默认 HybridCLR 启用配置仍阻塞在项目级 HybridCLR 初始化。方案后续应先处理 HybridCLR 初始化或建立专用无热更验证配置，再继续 Mono/WebGL Player 验收；功能层面继续补 UDP/TCP/WebSocket 端到端、RPC 和心跳缺失场景。

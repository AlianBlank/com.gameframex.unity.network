# Step 03 — KCP over UDP 传输层现状

## 当前状态

`KcpUdpTransport` 已实现 `IKcpTransport`。它使用 UDP 数据报承载 KCP 输出段，不额外添加帧协议。

## 涉及文件

| 文件 | 当前状态 |
| --- | --- |
| `Runtime/Network/Network/Kcp/KcpUdpTransport.cs` | 已新增 |

## 当前实现

### Socket 创建

`Connect` 根据 URI host 做 DNS 解析，使用解析结果的地址族创建 Socket：

```csharp
new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
```

默认接收缓冲下限是 1500 字节。构造参数会取 `Math.Max(1500, receiveBufferSize)`，因此 `KcpNetworkChannel` 传入默认 `Mtu=1200` 时实际仍使用 1500。

### Connect 流程

1. 校验 `address != null` 和未连接状态。
2. 解析 `address.Host` 和 `address.Port`。
3. `Dns.GetHostAddresses(host)`。
4. 创建 `IPEndPoint` 和 UDP Socket。
5. 调用 `Socket.Connect(remoteEp)`。
6. 设置 `_isConnected = true`。
7. 调用 `BeginReceive()` 启动 APM 接收循环。
8. 触发 `_onConnected` 回调。

UDP 的 `Connect` 不建立真正连接，只设置默认远端，方便后续 `Socket.Send`。

### 接收流程

每次 `BeginReceiveFrom` 都创建一个新的接收缓冲和 `ReceiveState`：

```csharp
socket.BeginReceiveFrom(
    state.Buffer,
    0,
    state.Buffer.Length,
    SocketFlags.None,
    ref state.RemoteEndPoint,
    OnReceiveCallback,
    state);
```

`OnReceiveCallback` 调用 `EndReceiveFrom` 后，如果 `bytesRead > 0`，直接触发 `_onDataReceived(state.Buffer, bytesRead)`，再进入下一次 `BeginReceive()`。

### SendRaw

当前行为：

- 未连接或 Socket 为空时直接返回。
- 已连接时调用 `Socket.Send(data, offset, count, SocketFlags.None)`。
- 捕获 `SocketException` 后调用 `Close()`。

### Close / Dispose

`Close()` 会：

1. 将 `_isConnected` 设为 `false`。
2. 将 `_socket` 置空。
3. 尝试 `Shutdown` 和 `Close` Socket。
4. 清空 `_remoteEndPoint`。
5. 触发 `_onClosed`。

`Dispose()` 当前直接调用 `Close()`。

## 与方案相关的注意点

- UDP 接收回调传出的缓冲区会在 `KcpNetworkChannel.OnTransportDataReceived` 中再次拷贝，因此传输层不需要保存缓冲。
- UDP `Socket.Shutdown` 可能抛异常，当前已忽略关闭异常。
- 该传输层没有连接超时语义，因为 UDP connect 是本地操作。

## 已有测试

- `KcpUdpTransportConnectInvokesConnectedCallback`
- `KcpUdpChannelFiresConnectedEventAfterTransportConnects`

这些测试只验证本地 UDP connect 和回调，不验证实际 UDP 收发。

## 验收标准

- [x] 实现 `IKcpTransport` 所有成员。
- [x] `Connect` 后触发连接回调。
- [x] 接收 UDP 数据报时触发数据回调。
- [x] `SendRaw` 使用 `Socket.Send` 同步发送。
- [x] 构造函数可传入自定义接收缓冲区大小，且下限为 1500。
- [x] 有 `[Preserve]` 标记。
- [x] 无帧协议逻辑。
- [ ] 需要补充真实 UDP echo 收发测试。
- [ ] 需要补充关闭回调只触发一次的回归测试。

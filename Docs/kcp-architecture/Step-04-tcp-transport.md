# Step 04 — KCP over TCP 传输层现状

## 当前状态

`KcpTcpTransport` 已实现 `IKcpTransport`。它使用 TCP stream 承载 KCP 段，并在每个 KCP 段前添加 4 字节大端长度前缀。

## 涉及文件

| 文件 | 当前状态 |
| --- | --- |
| `Runtime/Network/Network/Kcp/KcpTcpTransport.cs` | 已新增 |

## 当前实现

### Socket 创建

`Connect` 解析 host 后优先选择 IPv4 地址，若没有 IPv4 则使用第一个解析结果：

```csharp
new Socket(targetAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
```

随后设置：

```csharp
_socket.NoDelay = true;
```

### Connect 流程

1. 校验 `address != null` 和未连接状态。
2. DNS 解析 host。
3. 优先选择 IPv4 地址。
4. 创建 TCP Socket 并设置 `NoDelay = true`。
5. `BeginConnect` 后同步 `WaitOne()`，再 `EndConnect()`。
6. 设置 `_isConnected = true`。
7. 重置接收状态。
8. 启动 `BeginReceive()`。
9. 触发 `_onConnected`。

注意：当前连接是 APM 发起，但会同步等待完成。方案中如需真正异步连接，需要另行修改代码。

### 帧协议

发送格式：

```
[4 字节长度，大端][载荷数据]
```

接收状态：

```csharp
private enum ReceiveState
{
    ReadLength = 0,
    ReadBody = 1,
}
```

当前 `BeginReceive` 每次只读取当前状态剩余所需字节：

- `ReadLength` 只读满 4 字节长度头。
- `ReadBody` 只读满 `_expectedLength` 字节消息体。

因此代码天然处理拆包；对于“多个帧连续到达”，额外字节会留在 Socket 缓冲区，下一次 `BeginReceive` 再读取，不是单次回调中拆多个帧。

### SendRaw

当前行为：

- 未连接时抛 `InvalidOperationException`。
- `data == null` 时抛 `ArgumentNullException`。
- `count <= 0` 时直接返回。
- 构造长度前缀 + payload 的新数组。
- 同步调用 `_socket.Send(...)`。

### 安全防护

构造函数参数 `maxFrameSize` 必须大于 0，默认值为 `256 * 1024`。

接收时如果帧长度 `<= 0` 或 `> _maxFrameSize`，调用 `HandleConnectionLost()`，最终关闭连接并触发关闭回调。

### Close / Dispose

`Close()` 会：

1. 将 `_isConnected` 设为 `false`。
2. 将 `_socket` 置空。
3. 忽略关闭期间的 `SocketException` / `ObjectDisposedException`。
4. 触发 `_onClosed`。

`Dispose()` 当前直接调用 `Close()`。

## 已有测试

- `KcpTcpTransportRejectsInvalidMaxFrameSize`
- `KcpTcpTransportReceivesLengthPrefixedFrames`
- `KcpTcpTransportClosesOnOversizedFrame`

## 仍需补充

- `SendRaw` 长度前缀发送验证。
- TCP 拆包场景：长度头分多次读取、body 分多次读取。
- 连续多帧到达时是否能逐帧回调。
- 连接失败、发送失败和 Close 幂等行为。

## 验收标准

- [x] 实现 `IKcpTransport` 所有成员。
- [x] 发送数据时添加 4 字节大端长度前缀。
- [x] 接收状态机按长度头和 body 两阶段读取。
- [x] `_maxFrameSize` 超限时关闭连接并触发 `onClosed`。
- [x] 连接成功后触发 `onConnected`。
- [x] `Socket.NoDelay = true`。
- [x] 有 `[Preserve]` 标记。
- [x] 构造函数可传入自定义 `_maxFrameSize`。
- [ ] 需要补充完整拆包/连续多帧测试。

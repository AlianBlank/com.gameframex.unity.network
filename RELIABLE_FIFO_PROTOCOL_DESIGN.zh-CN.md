# 强制 FIFO 可靠协议设计稿

## 一、背景与决策

当前网络包默认消息头为 14 字节，无法表达会话恢复、可靠序号、ACK 和重复消息处理。为了支持游戏内可靠 KCP 以及 TCP、UDP、WebSocket 下的一致可靠投递能力，本设计决定进行一次破坏性协议升级。

本设计不兼容旧 14 字节协议。客户端 Unity 包与服务端 C# 网络栈必须同时升级。

核心决策：

- 基础消息头从 14 字节升级为 16 字节。
- 所有业务消息强制进入 FIFO 可靠队列。
- 可靠消息携带 24 字节可靠扩展头，完整头长度为 40 字节。
- 服务端生成 `SessionId`。
- 服务端维护会话、ACK、去重和重复登录响应缓存。
- KCP、TCP、UDP、WebSocket 统一复用同一套应用层可靠协议。
- 静默重连窗口推荐 10 秒。
- 服务端会话保留 TTL 推荐 30 秒。

## 二、目标

1. 在同一个连接内，继续由 KCP 或底层传输处理传输可靠性。
2. 在连接断开、静默重连、服务端短暂闪断后，由应用层可靠协议恢复 pending 消息。
3. 所有业务消息按 FIFO 顺序处理，服务端不越序执行业务。
4. 重复消息不重复执行业务，并返回原 ACK 或原响应。
5. 重复登录返回原登录响应，避免客户端因重试导致状态分叉。
6. 普通传输通道与 KCP 通道共享协议层能力，避免每种传输各自实现可靠逻辑。

## 三、非目标

1. 不兼容旧 14 字节消息头。
2. 不支持旧客户端与新服务端混连。
3. 不通过压缩解决包头开销。
4. 不把 KCP 的连接内可靠性等同于业务可靠性。
5. 不允许服务端在 FIFO 缺口存在时越序执行业务消息。

## 四、协议头

### 基础头 16 字节

```text
PacketLength      uint32   4 bytes   整包长度，包含头部和包体
OperationType     uint8    1 byte    心跳、业务、RPC、控制等操作类型
ZipFlag           uint8    1 byte    消息体压缩标记
HeaderFlags       uint16   2 bytes   协议版本和扩展标记
UniqueId          int32    4 bytes   RPC 请求/响应匹配 ID
MessageId         int32    4 bytes   协议消息号
----------------------------------------------
Total                     16 bytes
```

### 可靠扩展头 24 字节

所有业务消息强制可靠，因此业务消息默认携带可靠扩展头。

```text
SessionId         uint64   8 bytes   服务端生成的会话 ID
ReliableSequence  uint64   8 bytes   当前可靠消息序号
AckSequence       uint64   8 bytes   已确认对端可靠消息序号
----------------------------------------------
Total                     24 bytes
```

### 完整可靠头 40 字节

```text
16 bytes 基础头 + 24 bytes 可靠扩展头 = 40 bytes
```

## 五、HeaderFlags

`HeaderFlags` 使用 16 位，高 4 位保留协议版本，低位表示扩展语义。

```text
bit 15-12  ProtocolVersion   当前为 1
bit 11-8   Reserved          预留给加密、分片、优先级等能力
bit 7      Reliable          携带可靠扩展头
bit 6      Ack               携带有效 AckSequence
bit 5      Control           控制消息
bit 4      Resume            会话恢复消息
bit 3      LatestOnly        只保留同类最新消息，默认不启用
bit 2      NoRetry           不重试，默认不启用
bit 1      Duplicate         重复消息响应
bit 0      Reserved
```

虽然本次不兼容旧协议，仍建议保留 `ProtocolVersion`，用于未来协议演进和抓包诊断。

## 六、消息分类

| 类型 | 是否可靠 | 是否进入业务 FIFO | 说明 |
| --- | --- | --- | --- |
| 心跳 | 否 | 否 | 只用于连接活性检测 |
| 业务 Send | 是 | 是 | 强制 FIFO |
| 业务 Call/RPC | 是 | 是 | 强制 FIFO，响应通过 `UniqueId` 匹配 |
| ACK 控制包 | 是 | 否 | 更新确认序号，不作为业务消息排序 |
| Resume 控制包 | 是 | 否 | 恢复会话，不作为业务消息排序 |
| 登录请求 | 是 | 是 | 成功后由服务端分配或返回 `SessionId` |
| 重复登录 | 是 | 是 | 返回原登录响应 |

控制包不进入业务 FIFO，否则 ACK 和 Resume 可能被业务缺口阻塞。控制包仍然携带 `SessionId` 和 `AckSequence`，用于对齐可靠状态。

## 七、可靠序号规则

### 客户端上行序号

- 客户端为每条业务消息分配单调递增的 `ReliableSequence`。
- 初始值建议从 1 开始。
- 同一个 `SessionId` 内不得重复分配。
- 重连补发时必须使用原始 `ReliableSequence`，不能重新分配。

### 服务端下行序号

- 如果服务端也需要可靠下行，服务端为下行业务消息分配独立的 `ReliableSequence`。
- 客户端同样按 FIFO 规则确认服务端下行消息。
- 上行序号和下行序号彼此独立。

### ACK 规则

采用累计 ACK：

```text
AckSequence = N
```

表示对端已经按顺序处理所有 `ReliableSequence <= N` 的业务消息。

## 八、服务端 FIFO 处理规则

服务端按 `SessionId` 维护会话状态：

```text
SessionId
LastProcessedClientSequence
LastAckedServerSequence
PendingResponseCache
LoginResponseCache
ExpireAt
```

收到客户端业务消息后：

1. 如果 `ReliableSequence == LastProcessedClientSequence + 1`：
   - 正常执行业务。
   - 执行成功后推进 `LastProcessedClientSequence`。
   - 返回业务响应或 ACK。
   - 缓存必要响应。
2. 如果 `ReliableSequence <= LastProcessedClientSequence`：
   - 判定为重复消息。
   - 不重复执行业务。
   - 返回缓存响应或 ACK。
   - 响应包可设置 `Duplicate` 标记。
3. 如果 `ReliableSequence > LastProcessedClientSequence + 1`：
   - 判定为 FIFO 缺口。
   - 不执行业务。
   - 返回当前 `AckSequence = LastProcessedClientSequence`。
   - 等待客户端补发缺失消息。

服务端不得越序处理未来序号消息。

## 九、重复登录处理

登录成功后，服务端需要缓存登录响应：

```text
LoginKey
SessionId
LoginResponseBytes
ExpireAt
```

`LoginKey` 可由账号 ID、区服 ID、设备 ID、登录 token 或业务定义的登录唯一键组成。

重复登录命中缓存时：

- 不重复创建业务会话。
- 返回原登录响应。
- 旧连接可按业务策略踢下线、迁移到新连接，或标记为被替换。
- 返回响应时保持相同 `SessionId`。

登录响应缓存 TTL 建议与会话保留 TTL 一致，默认 30 秒。

## 十、重连类型

所有传输类型统一使用同一套上层连接活性判断模型。TCP、WebSocket、UDP、KCP 的差异只体现在底层事件来源不同，不影响业务层状态机。

统一输入事件：

```text
TransportConnected        传输层连接建立
TransportDisconnected     传输层断开
TransportError            传输层错误
ClosedByUser              业务主动关闭
PacketReceived            收到任意有效入包
HeartbeatAckReceived      收到心跳响应
HeartbeatTimeout          心跳超时
NoPacketTimeout           长时间无任何有效入包
ResumeAccepted            会话恢复成功
ResumeRejected            会话恢复失败
```

统一判定原则：

1. `ClosedByUser` 优先级最高，直接进入关闭流程。
2. `TransportDisconnected` 和 `TransportError` 不直接通知业务失败，先进入静默重连。
3. `HeartbeatTimeout` 和 `NoPacketTimeout` 先进入疑似断线，再尝试静默恢复。
4. 任意有效入包都刷新连接活性时间，不仅限于心跳包。
5. 是否继续 pending 队列只由 `ResumeAccepted` 决定。
6. `ResumeRejected`、超过静默窗口或会话过期后，统一转业务层重连。

传输差异：

| 传输 | 传输事件 | 心跳活性 | 统一处理 |
| --- | --- | --- | --- |
| TCP | 有断开/错误事件 | 必须保留 | 事件优先，心跳兜底半开连接 |
| WebSocket | 有关闭/错误事件 | 必须保留 | 事件优先，心跳兜底代理/NAT 静默失效 |
| UDP | 无可靠断开事件 | 唯一核心依据 | 通过心跳和入包时间判断逻辑连接 |
| KCP | 依赖底层传输事件 | 必须保留 | KCP 可靠不等于业务会话可靠 |

### 静默重连

静默重连用于短暂网络抖动、切网、传输层闪断。

推荐条件：

- 断开后 10 秒内重新连接。
- 客户端仍持有 `SessionId`。
- 断开原因不是业务层主动重登、心跳确认失败后的业务重连、账号切换或手动关闭。

行为：

- 客户端保留 pending FIFO 队列。
- 连接恢复后先发送 Resume 控制包。
- 服务端验证 `SessionId` 未过期。
- 服务端返回当前已处理的客户端 `AckSequence`。
- 客户端清理已 ACK 消息。
- 客户端按 FIFO 顺序补发剩余 pending 消息。

### 业务层重连

业务层重连用于心跳断开后确认连接不可恢复、超过静默窗口、服务端会话过期、账号状态变化等场景。

行为：

- 客户端清空 pending FIFO 队列。
- 未完成 RPC 失败化或由业务层重新发起。
- 客户端重新登录并同步状态。
- 服务端生成新的 `SessionId`。

### 主动关闭

主动关闭不触发自动重连，不保留 pending 队列。

## 十一、推荐配置

```text
SilentResumeWindowSeconds = 10
ServerSessionTtlSeconds = 30
LoginResponseCacheTtlSeconds = 30
ReliableMessageTimeoutSeconds = 15
ReliableRetryLimit = 5
PendingQueueMaxCount = 按项目压测确定
PendingQueueMaxBytes = 按项目压测确定
```

说明：

- 10 秒静默窗口适合覆盖常见移动网络抖动、切网、前后台短暂停顿。
- 30 秒服务端会话 TTL 给客户端恢复、ACK 对齐和补发留出余量。
- 超过 30 秒后继续补发旧业务消息风险较高，建议转业务重连。

## 十二、客户端状态机

```text
Disconnected
    -> Connecting
Connecting
    -> Connected
Connected
    -> SuspectDisconnected
    -> SilentReconnecting
    -> BusinessReconnecting
    -> Closed
SuspectDisconnected
    -> Connected
    -> SilentReconnecting
    -> BusinessReconnecting
SilentReconnecting
    -> Resuming
    -> BusinessReconnecting
Resuming
    -> Connected
    -> BusinessReconnecting
BusinessReconnecting
    -> Connecting
    -> Closed
Closed
```

状态说明：

- `SuspectDisconnected`：心跳或入包超时后的疑似断线状态，不通知业务失败，不清理 pending。
- `SilentReconnecting`：保留 pending 队列，尝试恢复原会话。
- `Resuming`：连接已建立，但业务消息尚不能补发，必须先完成 Resume。
- `BusinessReconnecting`：丢弃旧 pending，请业务重新登录和同步状态。

推荐活性阈值：

```text
HeartbeatWarnTimeoutSeconds = 5
SilentResumeWindowSeconds = 10
BusinessDisconnectTimeoutSeconds = 30
```

含义：

- 5 秒无有效入包或心跳响应：进入疑似断线。
- 10 秒内恢复入包或 Resume 成功：静默恢复，业务层无感。
- 超过 10 秒仍无法恢复：转业务层重连。
- 30 秒对应服务端会话保留上限，超过后服务端拒绝 Resume。

## 十三、业务层统一事件

业务层只感知统一后的网络事件，不直接感知 TCP、UDP、WebSocket、KCP 的底层差异。底层事件只能作为诊断原因进入事件参数，不能要求业务层按传输类型写分支逻辑。

事件按用途分为两类：

```text
UI/提示类事件：
NetworkSuspected
NetworkSilentResuming
NetworkResumed

业务决策类事件：
NetworkBusinessReconnectRequired
NetworkClosed
NetworkError
```

推荐事件：

```text
NetworkConnected
NetworkSuspected
NetworkSilentResuming
NetworkResumed
NetworkBusinessReconnectRequired
NetworkClosed
NetworkError
```

事件语义：

| 事件 | 业务层是否必须处理 | 说明 |
| --- | --- | --- |
| `NetworkConnected` | 可选 | 首次连接成功，或业务层重连后连接成功 |
| `NetworkSuspected` | 可选 | 疑似断线，网络层尚未判定失败，业务不清状态 |
| `NetworkSilentResuming` | 可选 | 正在静默恢复，pending 队列仍保留 |
| `NetworkResumed` | 可选 | 静默恢复成功，pending 队列已继续处理 |
| `NetworkBusinessReconnectRequired` | 必须 | 静默恢复失败、会话过期或超过窗口，需要业务重新登录和同步状态 |
| `NetworkClosed` | 可选 | 主动关闭或最终关闭 |
| `NetworkError` | 可选 | 协议错误、反序列化错误、不可恢复错误等 |

事件流建议：

```text
短闪断恢复成功：
NetworkSuspected
-> NetworkSilentResuming
-> NetworkResumed

短闪断恢复失败：
NetworkSuspected
-> NetworkSilentResuming
-> NetworkBusinessReconnectRequired

服务器踢人：
NetworkBusinessReconnectRequired(reason = ServerKick / DuplicateLogin / AccountBanned)

主动关闭：
NetworkClosed(reason = Normal / Dispose / UserClose)
```

说明：

- `NetworkSilentResuming` 等价于 UI 语义上的“正在重连”，但它强调当前仍处于可静默恢复窗口。
- `NetworkResumed` 等价于 UI 语义上的“重连成功”，但它要求连接恢复且 Resume 成功，pending 队列可以继续处理。
- `NetworkBusinessReconnectRequired` 等价于业务语义上的“重连失败或不可静默恢复”，业务层必须重新登录或同步状态。

推荐事件参数：

```text
ChannelName
TransportType
Reason
SessionId
LastAckSequence
PendingCount
ElapsedSeconds
Recoverable
ErrorCode
Message
```

其中：

- `TransportType` 仅用于日志和诊断，不建议业务按传输类型分支处理。
- `Reason` 表示触发原因，例如 `TransportDisconnected`、`HeartbeatTimeout`、`ResumeRejected`、`SessionExpired`。
- `Recoverable` 表示网络层是否仍可自行恢复。
- `PendingCount` 用于 UI 或日志观察当前仍未 ACK 的可靠消息数量。

事件触发建议：

1. 首次连接成功后抛出 `NetworkConnected`。
2. 心跳或入包超时进入 `SuspectDisconnected` 时抛出 `NetworkSuspected`。
3. 开始静默重连时抛出 `NetworkSilentResuming`。
4. `ResumeAccepted` 后抛出 `NetworkResumed`。
5. `ResumeRejected`、超过静默窗口、会话过期或达到重试上限后抛出 `NetworkBusinessReconnectRequired`。
6. 主动关闭时抛出 `NetworkClosed`，不抛出重连相关事件。
7. 协议解析失败、非法包头、反序列化失败等异常抛出 `NetworkError`。

短暂网络闪断且静默恢复成功时，业务层不需要清理状态，也不需要重新登录。业务层只有在收到 `NetworkBusinessReconnectRequired` 后才必须介入。

核心原则：

```text
业务层只处理“业务该做什么”，不处理“底层为什么断”。
```

## 十四、发送流程

业务消息发送：

```text
业务调用 Send/Call
-> 分配 ReliableSequence
-> 序列化消息体
-> 根据阈值压缩消息体
-> 写入 40 字节可靠头
-> 放入 pending FIFO 队列
-> 如果连接可发送，则按队列顺序发送
-> 等待 ACK 清理
```

重连补发：

```text
Silent reconnect connected
-> 发送 Resume 控制包
-> 收到 ResumeResult(AckSequence)
-> 清理 ReliableSequence <= AckSequence 的 pending 消息
-> 从队首开始补发剩余消息
```

## 十五、接收流程

收到包后：

```text
读取 16 字节基础头
-> 校验 PacketLength
-> 读取 HeaderFlags 和 ProtocolVersion
-> 如果 Reliable = 1，读取 24 字节可靠扩展
-> 如果是控制包，进入控制处理
-> 如果是业务包，进入 FIFO 校验
-> FIFO 校验通过后反序列化消息体
-> 执行业务
-> 推进 AckSequence
-> 返回 ACK 或业务响应
```

包体解压必须发生在包头解析之后、业务反序列化之前。

## 十六、压缩策略

继续保留现有压缩原则：

- 只压缩消息体。
- 包头不压缩。
- 小包不压缩。
- 大消息体按阈值压缩。
- 心跳、ACK、Resume 等控制包通常不压缩。

当前 512 字节阈值可以继续作为默认值，后续根据项目压测调整。

## 十七、传输层关系

本协议是应用层可靠协议，位于 TCP、UDP、WebSocket、KCP 之上。

要求：

- 包头编解码逻辑统一。
- FIFO reliable queue 统一。
- ACK 和 Resume 逻辑统一。
- KCP 通道不单独实现一套可靠队列。
- TCP、UDP、WebSocket、KCP 只负责字节传输和连接状态上报。

KCP 的 ARQ 只保证连接期间传输可靠，不能替代 `SessionId + ReliableSequence + AckSequence`。

## 十八、服务端实现要求

服务端 C# 网络栈至少需要提供：

1. 新 16 字节基础头解析和编码。
2. 24 字节可靠扩展头解析和编码。
3. `SessionId` 生成器。
4. 会话状态表。
5. FIFO 序号校验。
6. ACK 生成和处理。
7. Resume 控制消息处理。
8. 重复消息响应缓存。
9. 重复登录响应缓存。
10. 会话 TTL 清理。
11. 连接替换或踢下线策略。

## 十九、客户端实现要求

Unity 客户端至少需要提供：

1. 新 16 字节基础头编码和解析。
2. 24 字节可靠扩展头编码和解析。
3. `ReliableSequence` 生成器。
4. pending FIFO 可靠队列。
5. ACK 清理逻辑。
6. 静默重连状态机。
7. Resume 控制消息。
8. 业务重连时 pending 清理和 RPC 失败化。
9. 重复响应处理。
10. TCP、UDP、WebSocket、KCP 通道统一接入。

## 二十、失败处理

| 场景 | 处理 |
| --- | --- |
| Resume 成功 | 清理已 ACK，继续补发 pending |
| Resume 失败 | 转业务重连，清空 pending |
| 会话过期 | 转业务重连 |
| FIFO 缺口 | 返回当前 ACK，等待补发 |
| 重复消息 | 不执行业务，返回缓存响应或 ACK |
| pending 超时 | 静默阶段继续等待，业务重连阶段失败化 |
| 达到重试上限 | 转业务重连 |
| 主动关闭 | 清空 pending，不重连 |

## 二十一、测试清单

### 单元测试

1. 16 字节基础头长度和字段偏移。
2. 40 字节可靠头长度和字段偏移。
3. `HeaderFlags` 版本位和标记位编码。
4. `ReliableSequence` 单调递增。
5. ACK 清理 pending 队列。
6. FIFO 缺口不执行业务。
7. 重复序号返回缓存响应。
8. 登录响应缓存命中。

### 集成测试

1. TCP 通道可靠发送、ACK、补发。
2. UDP 通道可靠发送、ACK、补发。
3. WebSocket 通道可靠发送、ACK、补发。
4. KCP 通道可靠发送、ACK、补发。
5. 静默重连 10 秒内恢复 pending。
6. 超过 10 秒转业务重连。
7. 服务端会话 30 秒过期后拒绝 Resume。
8. 重复登录返回原响应。
9. 服务端收到重复可靠消息不重复执行业务。
10. 服务端收到未来序号不越序执行业务。

## 二十二、风险与约束

1. 强制 FIFO 会让前序消息阻塞后续消息，必须控制单条业务处理耗时。
2. 高频同步类消息如果全部强制 FIFO，可能增加延迟和队列积压，需要业务确认可接受。
3. 服务端必须缓存必要响应，否则重复消息只能返回 ACK，RPC 调用方可能拿不到原结果。
4. 会话状态和响应缓存会增加服务端内存占用，需要 TTL 和容量上限。
5. 双端必须同步发布，否则协议无法互通。

## 二十三、最终建议

本设计建议直接采用：

```text
16B 基础头 + 24B 可靠扩展头
所有业务消息强制 FIFO reliable
10 秒静默重连窗口
30 秒服务端会话保留 TTL
服务端生成 SessionId
重复登录返回原响应
TCP/UDP/WebSocket/KCP 统一接入应用层可靠协议
```

这套方案实现成本高于可选可靠模式，但协议语义清晰，适合双端同时破坏性升级，并能为后续服务端幂等、断线恢复、KCP 弱网体验优化提供统一基础。

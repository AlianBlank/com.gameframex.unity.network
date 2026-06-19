# 可靠重连与待发送消息保留设计方案

## 背景

游戏长连接在运行过程中可能因为服务端闪断、服务端滚动重启、客户端网络抖动、移动网络切换等原因短暂断开。

当前网络包已经具备以下基础能力：

- `MessageId`：协议消息号，用于根据消息类型识别请求或响应。
- `UniqueId`：消息实例编号，当前主要用于 RPC 请求和响应匹配。
- `ZipFlag`：消息体压缩标记。
- 默认发送头长度为 14 字节：`PacketLength + OperationType + ZipFlag + UniqueId + MessageId`。
- 默认消息体压缩阈值为 512 字节，只有注册压缩器且消息体长度大于 512 字节时才压缩。
- 已有心跳、心跳丢失、连接关闭、RPC 超时等基础机制。

现有机制可以识别消息类型、匹配 RPC 响应、检测断线，但还不能完整表达“这条消息是否需要断线保留、是否已被服务端确认、重连后是否应继续发送”。

因此需要在现有协议上增加可靠投递语义，使客户端在短时间闪断后可以自动重连，并继续处理未完成的可靠业务消息。

## 设计目标

1. 服务端短暂闪断或重启期间，客户端可以保留可靠消息队列。
2. 心跳导致的连接断开后，客户端可以在可配置时间窗口内自动重连。
3. 重连成功并恢复会话后，客户端可以继续发送尚未确认的可靠消息。
4. 服务端可以识别重复消息，避免重复执行业务逻辑。
5. 高频实时消息不被旧消息重发污染，例如移动、位置、输入、临时状态。
6. 兼容当前 `MessageId` 和 `UniqueId` 语义，避免破坏已有 RPC 匹配逻辑。
7. 普通消息尽量保持低开销，可靠扩展只在需要时启用。

## 非目标

以下能力不建议第一阶段实现：

- 客户端进程重启后继续恢复待发送队列。
- 将所有消息无差别持久化到磁盘。
- 让底层传输层自动判断业务消息是否可重发。
- 用压缩解决所有流量问题。

第一阶段建议先实现“内存级可靠发送队列 + 自动重连 + ACK 驱动补发”。

## 设计原因

### MessageId 不能作为单条消息唯一标识

`MessageId` 是协议号，表示消息类型。例如“领取奖励请求”“聊天请求”“移动同步请求”各自有不同的 `MessageId`。

同一种消息可以发送很多次，所以 `MessageId` 不能用于判断某一次具体发送是否已经被服务端处理。

### UniqueId 可以辅助去重，但不建议承担全部可靠投递职责

`UniqueId` 当前已经用于 RPC 请求和响应匹配。它的主要语义是“请求和响应配对”。

可靠投递还需要表达：

- 消息在可靠队列中的发送顺序。
- 哪些消息已经被对端确认。
- 哪些消息需要重连后继续发送。
- 哪些消息已经过期或重试次数过多。

这些语义和 RPC 匹配相关，但不是同一个概念。因此建议保留 `UniqueId` 的 RPC 职责，额外增加可靠投递序号。

### 服务端必须参与 ACK 和幂等处理

客户端保留队列只能保证“客户端还记得要发什么”。如果服务端没有 ACK 和幂等判断，重连后补发消息可能导致业务重复执行。

例如：

- 领取奖励重复执行。
- 购买请求重复扣费。
- 背包操作重复提交。
- 聊天消息重复发送。

因此可靠重发必须配套服务端 ACK 与重复消息识别。

## 消息分类

不建议所有消息都进入可靠队列。建议增加发送语义：

| 模式 | 含义 | 适用场景 |
| --- | --- | --- |
| `None` | 不缓存，断线后丢弃 | 心跳、弱通知、埋点 |
| `ReliableQueued` | 进入可靠队列，等待 ACK，重连后继续发送 | 领取奖励、背包操作、聊天、关键 RPC |
| `LatestOnly` | 只保留同类最后一条 | 位置、朝向、临时状态 |
| `Control` | 控制消息 | ACK、重连恢复、会话确认 |

推荐默认策略：

- `Call()` 默认可进入可靠队列，但应允许业务禁用。
- `Send()` 默认不进入可靠队列，除非显式指定。
- 心跳永远不进入可靠队列。
- 高频实时同步不进入普通可靠队列，必要时使用 `LatestOnly`。

## 协议头设计

### 当前默认消息头

当前默认头部为 14 字节：

```text
PacketLength       4 bytes
OperationType      1 byte
ZipFlag            1 byte
UniqueId           4 bytes
MessageId          4 bytes
--------------------------------
Total             14 bytes
```

### 完整可靠协议头

如果采用固定完整头，40 字节可以覆盖可靠重连所需信息：

```text
PacketLength        4 bytes   // 整包长度或包体总长度
OperationType       1 byte    // 心跳、业务、RPC、ACK、控制包
ZipFlag             1 byte    // 消息体是否压缩
HeaderFlags         2 bytes   // 可靠、ACK、重连、LatestOnly 等标记
UniqueId            4 bytes   // RPC 匹配
MessageId           4 bytes   // 协议消息号
SessionId           8 bytes   // 会话或重连标识
ReliableSequence    8 bytes   // 当前可靠消息序号
AckSequence         8 bytes   // 已确认对端可靠序号
--------------------------------
Total              40 bytes
```

40 字节固定头的优点：

- 协议实现简单。
- 解析路径固定。
- 所有消息都有一致结构。
- 调试和抓包更直观。

40 字节固定头的缺点：

- 心跳、小包、高频消息也承担完整头部开销。
- 压缩只作用于消息体，不能抵消头部开销。
- 高频实时消息流量会变大。

### 推荐：基础头 + 可选扩展头

为了兼顾性能和扩展性，推荐使用基础头加扩展头：

```text
基础头：
PacketLength       4 bytes
OperationType      1 byte
ZipFlag            1 byte
HeaderFlags        2 bytes
UniqueId           4 bytes
MessageId          4 bytes
--------------------------------
Total             16 bytes
```

可靠扩展：

```text
SessionId           8 bytes
ReliableSequence    8 bytes
AckSequence         8 bytes
--------------------------------
Total              24 bytes
```

最终开销：

| 消息类型 | 头部大小 |
| --- | ---: |
| 普通消息 | 16 bytes |
| 可靠消息 | 40 bytes |
| 控制消息 | 40 bytes |
| 心跳消息 | 16 bytes，或保留旧 14 bytes |

如果更重视实现简单，可以直接采用固定 40 字节头。如果更重视高频消息流量，建议采用扩展头方案。

## HeaderFlags 建议

`HeaderFlags` 使用 2 字节，最多可表达 16 个布尔标记。

建议定义：

```text
0x0001 Reliable        // 消息需要可靠投递
0x0002 Ack             // 包含 AckSequence
0x0004 Control         // 控制消息
0x0008 Resume          // 会话恢复消息
0x0010 LatestOnly      // 只保留最新消息
0x0020 NoRetry         // 不重试
0x0040 Duplicate       // 服务端识别到重复消息时可用于响应
0x0080 Reserved
```

后续可以继续扩展，例如加密、分片、优先级等。

## ACK 设计

### 累计 ACK

推荐优先采用累计 ACK：

```text
AckSequence = 1024
```

表示对端已经确认所有 `ReliableSequence <= 1024` 的可靠消息。

优点：

- ACK 数据少。
- 客户端清理队列简单。
- 可以附加在普通业务包中，减少单独 ACK 包数量。

### 单独 ACK 包

如果当前没有业务包返回，也可以发控制 ACK 包：

```text
OperationType = Control
HeaderFlags = Ack
AckSequence = latestReceivedReliableSequence
```

### 业务响应是否等同 ACK

如果某个 RPC 请求收到对应响应，理论上可以认为该请求已被服务端处理。

但仍建议保留协议级 ACK，因为：

- 有些可靠消息不是 RPC。
- RPC 响应可能丢失，服务端实际上已经处理完成。
- ACK 可以和业务响应解耦。
- 重连恢复时可以统一对齐队列状态。

## 会话恢复设计

客户端需要在重连时告诉服务器：

```text
SessionId
LastClientReliableSequence
LastServerAckSequence
LastReceivedServerSequence
```

建议流程：

```text
连接断开
-> 客户端进入 Reconnecting
-> 保留 pending reliable 队列
-> 重新建立 TCP / WebSocket / KCP 连接
-> 发送 ResumeSession
-> 服务端验证 SessionId / Token
-> 服务端返回 ResumeResult
-> 客户端根据结果继续发送 pending 队列或清空重同步
```

服务端返回结果建议包含：

```text
CanResume              // 是否允许恢复
AckSequence            // 服务端已确认的客户端可靠序号
ServerSequence         // 服务端当前下行序号
Reason                 // 不可恢复原因
```

如果 `CanResume = false`，客户端应该：

- 清空或失败化 pending reliable 队列。
- 通知业务层重新同步状态。
- 必要时重新登录或重新进入场景。

## 客户端发送队列

可靠队列中的每条消息建议包含：

```csharp
class PendingReliableMessage
{
    long ReliableSequence;
    int UniqueId;
    int MessageId;
    MessageObject MessageObject;
    byte[] SerializedPacket;
    SendMode SendMode;
    double CreatedTime;
    double LastSendTime;
    int RetryCount;
    double ExpireSeconds;
}
```

说明：

- `ReliableSequence` 用于 ACK 和顺序清理。
- `UniqueId` 继续用于 RPC 匹配。
- `SerializedPacket` 可以避免重连后重复序列化导致内容变化。
- `MessageObject` 可以保留给业务回调、日志、失败通知。
- `ExpireSeconds` 防止长时间离线后继续补发旧业务。

是否保留 `SerializedPacket` 需要权衡：

- 保留序列化包：重发严格一致，性能更好，但占内存。
- 只保留消息对象：内存较小，但重新序列化可能受到对象状态变化影响。

推荐第一阶段保留序列化后的包。

## 自动重连状态机

建议通道增加状态：

```text
Disconnected
Connecting
Connected
Disconnecting
Reconnecting
Resuming
Closed
```

心跳超时或 socket 异常时：

```text
Connected
-> Reconnecting
-> Connecting
-> Resuming
-> Connected
```

主动关闭时：

```text
Connected
-> Disconnecting
-> Closed
```

主动关闭不应触发自动重连，也不应继续补发队列。

## 发送流程

可靠消息：

```text
Send(message, ReliableQueued)
-> 分配 ReliableSequence
-> 分配或保留 UniqueId
-> 序列化并写入可靠扩展头
-> 加入 pending 队列
-> 如果 Connected，立即发送
-> 等待 ACK
-> 收到 ACK 后从 pending 队列移除
```

非可靠消息：

```text
Send(message, None)
-> 如果 Connected，直接发送
-> 如果未连接，丢弃或返回失败
```

LatestOnly 消息：

```text
Send(message, LatestOnly)
-> 按 MessageId 或业务 Key 替换旧消息
-> Connected 时发送最新值
-> Reconnect 后只发送最新值
```

## 重连后补发流程

```text
ResumeSession 成功
-> 服务端返回 AckSequence
-> 客户端移除 <= AckSequence 的 pending 消息
-> 按 ReliableSequence 顺序发送剩余 pending 消息
-> 恢复新消息发送
```

注意：补发前必须先完成会话恢复。不要在 TCP/WebSocket/KCP 连接成功后立即发送业务队列。

## 配置建议

建议增加通道级配置：

```csharp
public sealed class ReliableReconnectConfig
{
    public bool EnableAutoReconnect = true;
    public bool EnableReliableQueue = true;
    public int MaxReconnectAttempts = 5;
    public float ReconnectIntervalSeconds = 1.0f;
    public float MaxReconnectDurationSeconds = 30.0f;
    public int MaxPendingReliableMessages = 256;
    public int MaxPendingReliableBytes = 1024 * 1024;
    public float ReliableMessageExpireSeconds = 30.0f;
    public int MaxReliableRetryCount = 3;
}
```

默认值建议保守：

- 最大重连时间 30 秒。
- 最大 pending 消息 256 条。
- 最大 pending 字节 1 MB。
- 超出后失败化旧消息，而不是无限增长。

## API 建议

新增发送选项：

```csharp
public enum SendMode
{
    None = 0,
    ReliableQueued = 1,
    LatestOnly = 2,
    Control = 3,
}
```

扩展发送接口：

```csharp
void Send<T>(T messageObject, SendMode mode) where T : MessageObject;
```

RPC 接口可以增加参数：

```csharp
Task<TResult> Call<TResult>(
    MessageObject messageObject,
    bool isIgnoreErrorCode = false,
    SendMode mode = SendMode.ReliableQueued)
    where TResult : MessageObject, IResponseMessage;
```

为了兼容旧代码：

- 原有 `Send<T>(T messageObject)` 行为不变。
- 原有 `Call<TResult>()` 可以默认进入可靠队列，也可以通过配置控制。

## 服务端要求

服务端需要支持：

1. 识别 `SessionId`。
2. 维护每个会话的最近已处理 `ReliableSequence`。
3. 对重复 `ReliableSequence` 做幂等处理。
4. 返回 `AckSequence`。
5. 支持 `ResumeSession`。
6. 服务重启后明确返回是否可恢复。

如果服务端重启后没有持久化会话状态，则应返回 `CanResume = false`，客户端进入重新同步流程。

## 压缩策略

当前发送头处理器已经包含 512 字节压缩阈值：

```text
messageBodyBuffer.Length > 512
```

并且只有注册 `IMessageCompressHandler` 后才压缩。

这个设计应保留。可靠重连方案不改变压缩原则：

- 包头不参与压缩。
- 小包不压缩。
- 大消息体继续按阈值压缩。
- ACK、心跳、控制包通常不压缩。

压缩可以降低大包流量，但不能抵消固定包头对小包和高频消息的影响。

## 收益

### 提升弱网体验

短时间服务闪断或网络抖动时，玩家不一定立即掉线回登录页。客户端可以自动重连并继续处理关键请求。

### 降低业务失败率

关键 RPC 或可靠业务消息不会因为心跳断开窗口直接丢失，可以在恢复后继续发送。

### 避免重复执行业务

通过 `ReliableSequence + AckSequence + 服务端幂等`，可以避免补发导致重复领取、重复扣费、重复操作。

### 更清晰的协议语义

`MessageId` 继续表示消息类型，`UniqueId` 继续匹配 RPC，`ReliableSequence` 专门负责可靠投递。职责拆分后更容易维护。

### 可渐进落地

可以先对少量关键 RPC 启用可靠队列，验证稳定后再扩大范围。

## 风险与限制

### 服务端不支持幂等时不能安全重发

如果服务端无法识别重复消息，可靠重发可能引起严重业务问题。服务端 ACK 和幂等是必要条件。

### 队列过大可能占用内存

必须限制 pending 条数、字节数、过期时间和重试次数。

### 会话不可恢复时需要业务补偿

服务端重启后如果丢失会话，客户端不能盲目补发旧消息，需要重新同步状态。

### 高频消息不适合可靠队列

移动、位置、朝向、帧同步输入等旧消息可能已经过期，重发会污染状态。

### 固定 40 字节头会增加小包成本

如果所有消息都使用完整头，小包和高频消息流量会增加。建议优先考虑扩展头方案。

## 实现步骤建议

### 第一阶段：协议与队列基础

1. 增加 `HeaderFlags`。
2. 增加可靠扩展头解析和写入。
3. 增加 `ReliableSequence` 分配器。
4. 增加 pending reliable 队列。
5. 增加 `SendMode`。
6. 保持旧 `Send()` 接口兼容。

### 第二阶段：ACK

1. 增加 ACK 控制包。
2. 收到 ACK 后清理 pending 队列。
3. 支持业务包 piggyback `AckSequence`。
4. 增加 ACK 相关日志。

### 第三阶段：自动重连

1. 增加连接状态机。
2. 心跳超时后进入自动重连。
3. 支持最大重连次数和最大重连时长。
4. 主动关闭不触发重连。

### 第四阶段：会话恢复

1. 增加 `ResumeSession` 控制消息。
2. 服务端返回 `ResumeResult`。
3. 恢复成功后补发 pending。
4. 恢复失败后清空队列并通知业务同步状态。

### 第五阶段：测试与灰度

1. 单元测试包头兼容。
2. 单元测试可靠队列入队、ACK 清理、过期清理。
3. 功能测试服务端断开后自动重连。
4. 功能测试重连后补发未 ACK 消息。
5. 功能测试重复消息不会重复执行业务。
6. 灰度只对关键 RPC 开启。

## 当前代码改造映射

本节按当前包内代码结构拆分实现落点，便于后续编码时逐步推进。

### 协议头接口

当前发送头接口：

```text
Runtime/Network/Interface/IPacketSendHeaderHandler.cs
```

当前接收头接口：

```text
Runtime/Network/Interface/IPacketReceiveHeaderHandler.cs
```

当前默认实现：

```text
Runtime/Network/Helper/DefaultPacketSendHeaderHandler.cs
Runtime/Network/Helper/DefaultPacketReceiveHeaderHandler.cs
```

建议新增属性：

```csharp
ushort HeaderFlags { get; }
ulong SessionId { get; }
long ReliableSequence { get; }
long AckSequence { get; }
bool HasReliableHeader { get; }
```

注意事项：

- 不建议直接删除旧字段。
- `UniqueId` 和 `MessageId` 继续保留原语义。
- 如果采用可选扩展头，`PacketHeaderLength` 需要能表达当前包实际头长。
- 接收侧读取包体前必须先知道本包是否包含可靠扩展头。

兼容方案建议：

1. 保留当前 14 字节默认头实现。
2. 新增可靠头实现，例如 `ReliablePacketSendHeaderHandler` 和 `ReliablePacketReceiveHeaderHandler`。
3. 通过通道配置或注册不同 handler 来启用可靠协议。
4. 不让旧服务端误读新 40 字节头。

### 包头布局常量

建议新增集中定义，避免散落 magic number：

```csharp
public static class PacketHeaderLayout
{
    public const ushort LegacyHeaderLength = 14;
    public const ushort BaseHeaderLength = 16;
    public const ushort ReliableExtensionLength = 24;
    public const ushort ReliableHeaderLength = 40;
}
```

如果继续使用旧 14 字节头，则 `HeaderFlags` 不存在。

如果升级为 16 字节基础头，则基础头相比旧协议增加 2 字节 `HeaderFlags`。这属于协议不兼容变更，必须客户端和服务端同时升级。

### 发送入口

当前发送入口：

```text
Runtime/Network/Interface/INetworkChannel.cs
Runtime/Network/Network/NetworkManager.NetworkChannelBase.cs
```

当前 `Send<T>(T messageObject)` 会校验连接状态，然后把消息放入 `PSendPacketPool`。

建议保留旧接口：

```csharp
void Send<T>(T messageObject) where T : MessageObject;
```

新增接口：

```csharp
void Send<T>(T messageObject, SendMode mode) where T : MessageObject;
```

旧接口默认行为建议保持不可靠：

```csharp
Send(messageObject, SendMode.None);
```

原因：

- 避免旧业务突然进入可靠队列。
- 避免高频消息被错误重发。
- 避免旧调用在断线后表现变化。

### RPC 入口

当前 RPC 入口：

```text
NetworkManager.NetworkChannelBase.Call<TResult>()
NetworkManager.RpcState.cs
```

当前流程是：

```text
Call()
-> Send(messageObject)
-> PRpcState.Call(messageObject)
```

建议改造为：

```text
Call()
-> PRpcState.Call(messageObject)
-> Send(messageObject, ReliableQueued)
```

原因：

- 先进入 RPC 等待表，再发送，避免极端情况下响应很快回来但等待表还未登记。
- RPC 默认更适合可靠队列，但需要配置允许禁用。

建议新增重载：

```csharp
Task<TResult> Call<TResult>(
    MessageObject messageObject,
    bool isIgnoreErrorCode,
    SendMode mode)
    where TResult : MessageObject, IResponseMessage;
```

### 发送池与可靠队列

当前发送池：

```text
PSendPacketPool : GameFrameworkLinkedList<MessageObject>
```

它适合当前“连接正常时排队发送”的模型，但不适合作为可靠队列，因为：

- `Close()` 当前会清空 `PSendPacketPool`。
- `ProcessSendMessage()` 发送序列化后会 `ReferencePool.Release(messageObject)`。
- 它没有 ACK 状态、过期时间、重试次数和序列化包缓存。

建议新增：

```csharp
ReliableSendQueue
PendingReliableMessage
ReliableSequenceGenerator
```

职责拆分：

| 结构 | 职责 |
| --- | --- |
| `PSendPacketPool` | 当前连接内的即时发送队列 |
| `ReliableSendQueue` | 跨重连保留的可靠待确认队列 |
| `PendingReliableMessage` | 单条可靠消息状态 |

可靠消息发送时：

```text
ReliableQueued
-> 生成 ReliableSequence
-> 序列化成 byte[]
-> 写入 ReliableSendQueue
-> 当前连接可用时投递到传输层
```

非可靠消息发送时继续走旧 `PSendPacketPool`。

### Close 行为

当前 `NetworkChannelBase.Close()` 会：

- 关闭 socket。
- 触发 `NetworkChannelClosed`。
- 清空 `PSendPacketPool`。
- 重置心跳。
- `PRpcState.Reset()`。
- 清空执行消息队列。

可靠重连需要区分关闭原因：

| 场景 | 是否保留可靠队列 | 是否重置 RPC |
| --- | --- | --- |
| 主动关闭 | 否 | 是 |
| Dispose | 否 | 是 |
| 心跳超时进入重连 | 是 | 否，或暂停超时 |
| Socket 异常进入重连 | 是 | 否，或暂停超时 |
| 会话恢复失败 | 否 | 是，并通知失败 |

因此建议增加关闭类型：

```csharp
CloseMode.Normal
CloseMode.Dispose
CloseMode.Reconnectable
CloseMode.ResumeFailed
```

不要只依赖字符串 reason 判断业务行为。

### RPC 超时处理

当前 `RpcState.Update()` 会按真实流逝时间累计超时。

重连期间需要明确策略：

1. 继续计时：实现简单，但网络闪断时 RPC 容易超时。
2. 暂停计时：更符合自动重连预期，但需要新增状态。
3. 延长计时：重连期间以较慢倍率计时。

推荐第一阶段采用暂停计时：

```text
Connected: RPC 正常计时
Reconnecting/Resuming: RPC 暂停计时
ResumeFailed: RPC 全部失败
```

这样短时间服务闪断不会导致正在等待的 RPC 立即失败。

### 接收处理

当前接收后会：

```text
DeserializePacketHeader
-> 根据 ZipFlag 解压
-> DeserializePacketBody
-> messageObject.SetUpdateUniqueId(PacketReceiveHeaderHandler.UniqueId)
-> PRpcState.TryReply
-> 分发普通消息
```

可靠协议需要在 `DeserializePacketHeader` 后增加：

1. 读取 `HeaderFlags`。
2. 如果有 `AckSequence`，清理本地 pending。
3. 如果是可靠下行消息，更新本地 `LastReceivedServerSequence`。
4. 必要时回 ACK 给服务端。
5. 再进入原来的 RPC 或普通消息分发。

建议 ACK 清理发生在业务分发前，避免业务处理异常导致 ACK 状态滞后。

### 自动重连落点

当前 `NetworkManager` 和 `NetworkChannelBase` 已有：

- `NetworkConnected`
- `NetworkClosed`
- `NetworkMissHeartBeat`
- `Connect(Uri, object userData)`
- `Close(string reason, ushort code)`

建议重连逻辑不要放在传输层 socket 内部，而应放在 `NetworkChannelBase` 或其组合状态对象中。

原因：

- TCP、WebSocket、KCP 都需要同样的重连策略。
- 传输层不知道业务队列和会话恢复。
- 重连成功后必须先恢复会话再补发可靠队列。

建议新增：

```csharp
ReconnectState
ReliableReconnectConfig
SessionResumeState
```

### KCP / TCP / WebSocket 兼容

可靠重连应尽量工作在 `NetworkChannelBase` 层，而不是每个传输协议各自实现。

各通道只需要保证：

- 连接成功事件准确。
- 连接关闭事件准确。
- 发送失败能上报错误。
- 重连时可以重新调用 `Connect()`。

KCP 当前有独立发送路径，后续实现时需要确认：

- `KcpNetworkChannel.ProcessSendMessage()` 是否和基础通道复用同一可靠队列。
- KCP 内部可靠性不等同于业务可靠重发。KCP 保证连接期间传输可靠，不保证服务重启后的业务恢复。

### 事件建议

建议增加新事件，方便业务层感知恢复过程：

```csharp
NetworkReconnecting
NetworkReconnectFailed
NetworkResumeSucceeded
NetworkResumeFailed
ReliableMessageExpired
ReliableMessageDropped
ReliableMessageAcked
```

第一阶段可以只做内部日志，后续再公开事件。

### 日志建议

建议关键路径输出 Debug 日志：

```text
Reliable enqueue: messageId, uniqueId, sequence
Reliable send: sequence, retryCount
Reliable ack: ackSequence, removedCount
Reconnect start: reason, attempt
Reconnect connected
Resume request: sessionId, lastSequence
Resume result: canResume, ackSequence
Reliable expired: sequence, messageId, uniqueId
```

这些日志对弱网和服务闪断问题定位非常关键。

## 推荐落地顺序

如果后续正式实现，推荐按下面顺序拆 PR 或提交：

1. 只增加枚举、配置和可靠头结构，不改变默认行为。
2. 增加可靠头编码/解码单元测试。
3. 增加 `SendMode` 和新发送重载，旧 `Send()` 行为不变。
4. 增加 `ReliableSendQueue`，但先不启用自动重连。
5. 增加 ACK 清理逻辑和测试。
6. 增加自动重连状态机。
7. 增加会话恢复控制消息。
8. 将 RPC 默认接入 `ReliableQueued`，或通过配置开启。
9. 做断线、重连、补发、重复 ACK 的功能测试。
10. 灰度开启关键业务消息。

这样可以降低一次性改动范围，也方便每个阶段独立验证。

## 测试建议

### 客户端单元测试

- 普通消息头长度正确。
- 可靠消息头长度正确。
- `HeaderFlags` 正确编码和解码。
- `ReliableSequence` 单调递增。
- ACK 可以清理 pending 队列。
- 超时消息会失败化。
- `LatestOnly` 会替换旧消息。
- 心跳不会进入 pending 队列。

### 集成测试

- 连接正常时可靠消息发送并收到 ACK。
- 发送后断线，未 ACK 消息保留。
- 重连恢复成功后继续发送 pending。
- 服务端返回不可恢复时 pending 被清理。
- 服务端收到重复 `ReliableSequence` 不重复执行业务。
- 服务端闪断 5 秒内客户端无感恢复。
- 超过最大重连时间后通知业务失败。

### 压测与弱网测试

- 高频小包使用短头或非可靠模式。
- 大包压缩阈值仍然生效。
- pending 队列达到上限时行为符合预期。
- 自动重连不会造成连接风暴。

## 推荐结论

推荐采用：

```text
基础头 16 bytes + 可靠扩展 24 bytes = 完整可靠头 40 bytes
```

并且只对需要可靠投递的消息启用 40 字节完整头。

核心原则：

1. `MessageId` 负责消息类型。
2. `UniqueId` 负责 RPC 匹配。
3. `ReliableSequence` 负责可靠投递。
4. `AckSequence` 负责确认清理。
5. `SessionId` 负责重连恢复。
6. 业务层决定消息是否可重发。
7. 服务端必须支持 ACK 和幂等。

该方案可以在不破坏现有 RPC 语义的前提下，为服务闪断、心跳断开、短时间网络抖动提供可控的可靠恢复能力。

# 消息头升级评估方案

> 本文档是对 [`RELIABLE_RECONNECT_DESIGN.zh-CN.md`](./RELIABLE_RECONNECT_DESIGN.zh-CN.md) 中「协议头设计」章节的架构性评估，回答两个核心问题：
>
> 1. 将当前 14 字节消息头扩展为更大长度，是否属于破坏性变更？
> 2. 如果要"一步到位"，应该选择哪种方案，为什么？

## 一、概念校正：KCP 保证传输可靠 ≠ 业务可靠

在讨论是否需要修改消息头之前，必须先理清一个常见概念混淆。

| 可靠性类型 | 责任方 | 是否需要修改应用层消息头 |
| --- | --- | --- |
| 传输层可靠（连接不丢包、不乱序） | KCP（ARQ 重传 + 序列号） | 否 |
| 会话层可靠（重连后业务恢复） | 应用层（业务代码 + 协议头） | **是** |

KCP 在**同一个连接内**保证字节流可靠有序送达，这是 KCP 自身的能力，与上层应用协议无关。

但以下场景 KCP 无法覆盖：

- 服务端滚动重启 / 闪断。
- 客户端网络抖动后断线重连。
- 服务端进程被 kill 后冷启动。

这些场景下，KCP 的 conversation 会被重建，所有 inflight 数据丢失。客户端不知道哪些请求已被服务端处理，服务端也不知道客户端还想发哪些消息。这正是 `RELIABLE_RECONNECT_DESIGN.zh-CN.md` 要解决的问题。

**结论**：修改应用层消息头是为了实现"会话级可靠"，与 KCP 的"传输级可靠"是两个正交的维度。架构是 KCP+TCP/UDP/WS **不能成为不修改消息头的理由**，也不能成为修改后不算破坏性变更的理由。

## 二、当前消息头结构对账

当前应用层消息头长度为 **14 字节**，在三处代码中硬编码一致。

### 发送端

文件：`Runtime/Network/Helper/DefaultPacketSendHeaderHandler.cs`

```csharp
private const int NetPacketLength = sizeof(uint);       // 4
private const int NetCmdIdLength = sizeof(int);         // 4
private const int NetOperationTypeLength = sizeof(byte); // 1
private const int NetZipFlagLength = sizeof(byte);      // 1
private const int NetUniqueIdLength = sizeof(int);      // 4

PacketHeaderLength = NetPacketLength + NetOperationTypeLength + NetZipFlagLength
                   + NetUniqueIdLength + NetCmdIdLength;
// = 4 + 1 + 1 + 4 + 4 = 14
```

### 接收端

文件：`Runtime/Network/Helper/DefaultPacketReceiveHeaderHandler.cs`

```csharp
public ushort PacketHeaderLength { get; } = NetPacketLength + OperationTypeLength
                                          + NetZipFlagLength + NetUniqueIdLength
                                          + NetCmdIdLength;
// = 4 + 1 + 1 + 4 + 4 = 14
```

> 注意：该文件第 91 行注释 `包头长度 2 + 1 + 1 + 4 + 4` 是错的，应为 `4 + 1 + 1 + 4 + 4`，建议顺手修正。

### 接收状态机

文件：`Runtime/Network/Network/NetworkManager.ReceiveState.cs`

```csharp
public const int PacketHeaderLength = 14;
```

### 字段拆解

| 字段 | 类型 | 长度 | 说明 |
| --- | --- | ---: | --- |
| `PacketLength` | `uint` | 4 | 整包长度（含头） |
| `OperationType` | `byte` | 1 | 心跳 / 业务 / RPC 等 |
| `ZipFlag` | `byte` | 1 | 消息体是否压缩 |
| `UniqueId` | `int` | 4 | RPC 请求/响应匹配 |
| `MessageId` | `int` | 4 | 协议消息号 |
| **合计** | | **14** | |

## 三、是否破坏性变更的判断

**结论：是破坏性变更。**

理由如下：

### 1. 协议线上格式不兼容

消息头长度是流式解析的硬约束。一旦改变头长度：

- 旧端发送的包，新端按新偏移读 → 字段错位。
- 新端发送的包，旧端按旧偏移读 → 字段错位。
- 错位后 `PacketLength` 字段就读错，整条流废掉。

### 2. 客户端 / 服务端必须同步升级

没有"灰度兼容"窗口。两端必须强一致地切换到新协议，否则任何一端都无法解析对方的消息。

### 3. 没有协议版本号

当前消息头里没有 `ProtocolVersion` 字段，无法做协商式平滑过渡。要做兼容升级，必须先加版本号 + 旧格式回落逻辑。

### 4. 波及所有下游项目

所有使用 `com.gameframex.unity.network` 的项目都得跟着升 major。

### 5. KCP 不解决兼容性

KCP 把应用层消息当作不可解析的 blob 透传。它只保证可靠、保序、分包，对内容格式没有任何理解，更不会做协议协商。

参考代码 `Runtime/Network/Network/Kcp/NetworkManager.KcpNetworkChannel.cs`：

```csharp
// 发送路径：14B 头 + 消息体 整段丢给 KCP
SerializePacketHeader(messageObject, m_SendMemoryStream, ...);
SerializePacketBody(messageBodyBuffer, m_SendMemoryStream);
byte[] sendBuffer = m_SendMemoryStream.ToArray();
m_Kcp.Send(sendBuffer, 0, sendBuffer.Length);

// 接收路径：从 KCP 给的 byte[] 按 14 字节偏移取头
int offset = 0;
var readPacketLength = reader.ReadUInt(ref offset); // 4
OperationType = reader.ReadByte(ref offset);        // 1
ZipFlag = reader.ReadByte(ref offset);              // 1
UniqueId = reader.ReadInt(ref offset);              // 4
Id = reader.ReadInt(ref offset);                    // 4
```

应用层消息格式是**两端应用层的共同约定**，与下面用什么传输层完全无关。

## 四、SemVer 建议

修改消息头长度 = **MAJOR 升级**。

例如当前 `2.x.x` → `3.0.0`。

`CHANGELOG.md` 必须在 BREAKING CHANGES 区段标注，例如：

```text
## [3.0.0] - YYYY-MM-DD

### BREAKING CHANGES

- **协议头**：基础头由 14 字节扩展为 16 字节（新增 `HeaderFlags`）。
- 客户端与服务端必须同步升级，否则无法互通。
- 旧版 14 字节头不再支持。
```

## 五、"一步到位"的真正含义

容易有两种误读：

- **误读 A**："一步到位" = 直接采用固定 40 字节头。
- **误读 B**："一步到位" = 一次性把所有字段都加进去。

**真正的"一步到位"应该是：协议骨架一步到位，而不是字段长度一步到位。**

具体含义：

- ✅ **协议头骨架一步到位**：定义清楚基础头 + 扩展段的位置和触发机制，未来不再有破坏性变更。
- ✅ **字段常量一步到位**：把 `PacketHeaderLayout` 常量类准备好，所有长度集中管理。
- ❌ **不要直接切固定 40B**：心跳、小包、高频消息每条多花 26 字节，长期看是流量浪费。
- ❌ **不要在没有版本位的情况下硬切**：以后想加新字段还得再破坏一次。

## 六、推荐方案：16B 基础头 + 可选 24B 可靠扩展

### 协议布局

```text
基础头 16 字节（所有包都携带）：
┌──────────────────────────────────────────────┐
│ PacketLength     4 bytes   整包长度（含头）    │
│ OperationType    1 byte    心跳/业务/RPC/控制  │
│ ZipFlag          1 byte    消息体是否压缩       │
│ HeaderFlags      2 bytes   标记位（含版本）     │
│ UniqueId         4 bytes   RPC 匹配            │
│ MessageId        4 bytes   协议消息号           │
└──────────────────────────────────────────────┘

可靠扩展 24 字节（仅当 HeaderFlags.HasReliable = 1 时存在）：
┌──────────────────────────────────────────────┐
│ SessionId        8 bytes   会话或重连标识       │
│ ReliableSequence 8 bytes   当前可靠消息序号     │
│ AckSequence      8 bytes   已确认的对端可靠序号  │
└──────────────────────────────────────────────┘
```

### HeaderFlags 位分配建议

```text
bit 15-12  ProtocolVersion   (4 位，留 16 个版本空间)
bit 11-8   Reserved          (留给未来加密/分片/优先级)
bit 7      Reliable          (本包携带 24B 可靠扩展)
bit 6      Ack               (本包含 AckSequence)
bit 5      Control           (控制包)
bit 4      Resume            (会话恢复包)
bit 3      LatestOnly        (只保留最新)
bit 2      NoRetry           (不重试)
bit 1      Duplicate         (服务端识别到重复时的响应)
bit 0      Reserved
```

`ProtocolVersion` 塞进 HeaderFlags 高 4 位，**不占用额外字节**。当前 `ProtocolVersion = 1`，老协议视为 `ProtocolVersion = 0`（且没有 HeaderFlags 字段）。

### 各类消息的实际开销

| 消息类型 | 头部大小 |
| --- | ---: |
| 普通消息 | 16 字节 |
| 可靠消息 | 40 字节 |
| 控制消息（ACK/Resume） | 40 字节 |
| 心跳 | 16 字节 |

### 为什么这是真正的"一步到位"

1. **基础头永远 16 字节**：未来加加密、分片、压缩级别——都是新增 flag + 可选扩展段，**永远不再需要破坏性变更**。
2. **小包不膨胀**：心跳、移动同步、临时状态走 16 字节短头。
3. **可靠包才付 40 字节成本**：领奖、扣费、聊天等关键消息才进入可靠路径。
4. **服务端可以读版本号做协商**：未来协议若要重构，用 `ProtocolVersion = 2` 走新路径，老版本回落。

### 与设计文档原方案的对比

`RELIABLE_RECONNECT_DESIGN.zh-CN.md` 的方案 B 也是「基础 16B + 可靠扩展 24B」，本文档在此基础上补充：

| 维度 | 设计文档原方案 | 本评估建议 |
| --- | --- | --- |
| 基础头长度 | 16B | 16B（一致） |
| 可靠扩展长度 | 24B | 24B（一致） |
| HeaderFlags 位分配 | 8 个布尔位（0x0001~0x0080） | 高 4 位留给 ProtocolVersion，低 8 位用于业务标记 |
| 版本协商 | 未提及 | 通过 HeaderFlags 高 4 位实现 |
| 长期可扩展性 | 加新字段需破坏性升级 | 加新字段走 flag + 扩展段，**永不破坏** |

核心差异：**本文档把"版本位"纳入了 HeaderFlags**，避免未来任何协议变更都需要 MAJOR 升级。

## 七、落地顺序

只有 Phase 1 是破坏性变更（v3.0.0），Phase 2/3 都是非破坏性的。这才是真正的"一步到位"——以后再也不用碰消息头长度。

### Phase 1：协议骨架（破坏性 v3.0.0）

- 14B → 16B 基础头。
- 新增 `PacketHeaderLayout` 常量类，集中管理所有长度。
- 接收端按 `HeaderFlags.HasReliable` 判断是否有 24B 扩展段。
- 默认行为：所有消息走 16B 短头，不启用可靠扩展。
- **客户端 + 服务端必须同步上线**。

### Phase 2：可靠扩展实现（非破坏性 v3.x.x）

- 24B 可靠扩展段。
- `SendMode` API（`None` / `ReliableQueued` / `LatestOnly` / `Control`）。
- `ReliableSendQueue` + `PendingReliableMessage` + `ReliableSequenceGenerator`。
- 只对显式 `ReliableQueued` 消息启用扩展段。
- 旧 `Send()` 接口保持 `SendMode.None` 行为。

### Phase 3：ACK + 自动重连 + 会话恢复（非破坏性 v3.x.x）

- 连接状态机（`Disconnected` / `Connecting` / `Connected` / `Reconnecting` / `Resuming` / `Closed`）。
- ACK 控制包 + 业务包 piggyback AckSequence。
- `ResumeSession` 控制消息 + `ResumeResult` 响应。
- 重连成功后先恢复会话，再补发 pending 队列。

## 八、关键工程提醒

### 1. 服务端必须同步改

`RELIABLE_RECONNECT_DESIGN.zh-CN.md` 侧重客户端，但**服务端 C# 端也得做对应改造**。这是 v3.0.0 升级最大的工作量，不是客户端。

服务端至少要做：

- 识别 `ProtocolVersion`。
- 按新 16B 格式解析基础头。
- 根据 `HeaderFlags.HasReliable` 决定是否读 24B 扩展。
- 维护每个会话的 `LastProcessedReliableSequence`。
- 对重复 `ReliableSequence` 做幂等处理。
- 支持 `ResumeSession` 控制包。

### 2. 灰度策略

Phase 1 上线时，可以加一个 channel 配置项，允许临时回落到 14B 老协议，做双版本灰度：

```csharp
public sealed class ProtocolVersionConfig
{
    public const ushort Legacy = 0;  // 14B 老协议
    public const ushort Current = 1; // 16B+ 新协议
}
```

灰度完成后再移除 Legacy 支持。

### 3. 抓包工具同步更新

Wireshark / 自定义协议解析工具都得改，否则调试时看不见新字段。

### 4. 单元测试覆盖

至少覆盖：

- 基础头 16B 长度正确。
- 可靠扩展 24B 长度正确。
- `HeaderFlags` 编解码正确（含 `ProtocolVersion` 高位）。
- `ReliableSequence` 单调递增。
- ACK 可清理 pending 队列。
- 旧 14B 包不能被新接收端误解析（早期阶段的双版本灰度）。

### 5. KCP 通道的额外注意

参考 `NetworkManager.KcpNetworkChannel.cs:417` 的 `ProcessSend()`，KCP 通道有独立的发送路径。Phase 2 实现时需要确认：

- KCP 通道和基础通道是否复用同一可靠队列。
- KCP 通道的 `MaxMessageSize` 配置是否能容纳 40B 头 + 大消息体。
- KCP 自身的可靠性（连接内 ARQ）不能等同于业务可靠重发，二者职责不同。

## 九、风险清单

| 风险 | 缓解措施 |
| --- | --- |
| 服务端不支持幂等时不能安全重发 | Phase 2 上线前必须先完成服务端幂等改造 |
| 队列过大占用内存 | `ReliableReconnectConfig` 限制 pending 条数、字节数、过期时间、重试次数 |
| 会话不可恢复时盲目补发 | `ResumeResult.CanResume = false` 时清空队列并通知业务同步状态 |
| 高频消息进可靠队列造成污染 | `SendMode.LatestOnly` 专门处理移动、位置、临时状态 |
| 双版本灰度期间协议混乱 | 客户端发送前协商版本，服务端拒绝不支持的版本 |

## 十、最终结论

### 推荐方案

```text
基础头 16 字节 + 可靠扩展 24 字节 = 完整可靠头 40 字节
```

`ProtocolVersion` 占用 `HeaderFlags` 高 4 位，避免未来再发生破坏性变更。

### 升级路径

```text
2.x.x (14B)
    ↓ Phase 1（破坏性 v3.0.0）
3.0.0 (16B 基础头，无可靠扩展)
    ↓ Phase 2（非破坏性）
3.1.0 (+ 可靠扩展 24B，SendMode API)
    ↓ Phase 3（非破坏性）
3.2.0 (+ ACK + 自动重连 + 会话恢复)
```

### 核心原则

1. `ProtocolVersion` 负责版本协商（未来扩展的唯一兜底）。
2. `HeaderFlags` 负责"本包携带哪些扩展段"。
3. `MessageId` 负责消息类型。
4. `UniqueId` 负责 RPC 匹配。
5. `ReliableSequence` 负责可靠投递序号。
6. `AckSequence` 负责确认清理。
7. `SessionId` 负责重连恢复。
8. 业务层决定消息是否可重发。
9. 服务端必须支持 ACK 和幂等。

### 长期收益

完成本次"一步到位"的协议骨架升级后：

- 任何未来的协议变更（加密、分片、新业务字段）都通过 `HeaderFlags` 新增 flag + 可选扩展段实现。
- 不再发生 MAJOR 升级。
- 客户端与服务端可以异步演进（在 `ProtocolVersion` 协商允许的范围内）。

这是真正意义上的"一步到位"。

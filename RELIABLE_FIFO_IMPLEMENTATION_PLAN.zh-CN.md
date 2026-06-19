# 强制 FIFO 可靠协议渐进式执行方案

## 一、输入来源

本执行方案整合以下内容：

1. 当前设计稿：`RELIABLE_FIFO_PROTOCOL_DESIGN.zh-CN.md`。
2. 当前评估稿：`PACKET_HEADER_UPGRADE_ASSESSMENT.zh-CN.md`。
3. 历史分支：`feature/network-reconnect-and-server-kick`。
4. 服务端源码：`/Users/mac/Documents/GithubWorks/GameFrameX.Server.Source`。

本方案面向客户端 Unity 网络包和服务端 C# 网络栈同步破坏性升级，不兼容旧 14 字节协议。

## 二、历史分支评估

`feature/network-reconnect-and-server-kick` 已实现或设计了以下可吸收能力：

- `NetworkCloseReason.ServerKick` 和 `NetworkErrorCode.ServerKickError`。
- 连接地址与 `userData` 保存，用于重连复用。
- `ReconnectState`，支持指数退避。
- 自动重连 API：启用、最大重试次数、手动重连、取消重连。
- 重连生命周期事件：重连中、重连成功、重连失败。
- `NetworkComponent` 事件转发。
- 服务器踢人不触发自动重连的语义。

但该分支不能直接合并，原因：

- 事件语义停留在连接级 `Reconnecting/Reconnected/Failed`，尚未区分 `SuspectDisconnected`、`SilentResuming`、`BusinessReconnectRequired`。
- 没有 `SessionId`、`ReliableSequence`、`AckSequence`、Resume 和 pending FIFO 队列。
- 没有服务端幂等、重复响应缓存、重复登录返回原响应。
- 部分改动与当前主线优化方向冲突，例如把线程安全队列退回普通集合、恢复空包体拒绝、回退 package 元数据。
- 部分 EventArgs 生命周期处理有风险，不能直接复用同一个实例跨管理器事件和全局事件系统。

结论：吸收其“关闭原因、重连状态、连接信息保存、服务器踢人语义、指数退避经验”，不直接 cherry-pick 全部分支。

## 三、总体目标

1. 客户端和服务端统一升级到 16B 基础头 + 24B 可靠扩展头。
2. 所有业务消息默认强制 FIFO reliable。
3. TCP、UDP、WebSocket、KCP 统一走同一套应用层可靠状态机。
4. 业务层只感知统一事件，不感知底层传输差异。
5. 静默重连成功时业务层无感。
6. 静默重连失败、会话过期、服务器踢人时业务层明确收到事件并介入。
7. 服务端支持 FIFO 去重、ACK、Resume、重复登录返回原响应。

## 四、执行原则

- 双端同步发布，作为 major 版本升级。
- 协议头、Flags、序号和状态枚举必须先定常量，再写逻辑。
- 可靠协议放在应用层网络管线，不放到具体 TCP、UDP、WebSocket、KCP 实现里。
- 控制包不进入业务 FIFO，否则 ACK/Resume 会被业务消息阻塞。
- 所有重连是否继续 pending 队列，只以 Resume 结果为准。
- 历史分支里的连接级重连事件要升级为统一业务事件。
- 服务端幂等先于客户端补发上线，否则不能安全启用强制 FIFO。

## 五、阶段计划

### Phase 0：冻结协议与任务边界

目标：确保双端按同一份协议实现。

客户端任务：

1. 确认 `PacketHeaderLayout` 常量：
   - `BaseHeaderLength = 16`
   - `ReliableExtensionLength = 24`
   - `ReliableHeaderLength = 40`
2. 确认 `HeaderFlags` 位分配。
3. 确认 `ProtocolVersion = 1`。
4. 确认默认配置：
   - `HeartbeatWarnTimeoutSeconds = 5`
   - `SilentResumeWindowSeconds = 10`
   - `ServerSessionTtlSeconds = 30`
   - `ReliableMessageTimeoutSeconds = 15`
   - `ReliableRetryLimit = 5`

服务端任务：

1. 在 `GameFrameX.NetWork.Abstractions` 或合适的共享项目中定义相同常量。
2. 明确新协议只支持 16B/40B，不解析旧 14B。
3. 确认服务端登录成功后生成 `SessionId`。

验收：

- 双端代码中协议常量完全一致。
- 设计稿和执行方案中没有旧协议兼容路径。

### Phase 1：协议头破坏性升级

目标：先让双端能收发新头，但可靠队列暂不启用业务补发。

客户端任务：

1. 新增 `PacketHeaderLayout`。
2. 修改 `DefaultPacketSendHeaderHandler`：
   - 写入 16B 基础头。
   - 写入 `HeaderFlags`。
   - 默认业务包设置 `Reliable` 标记后预留可靠扩展写入路径。
3. 修改 `DefaultPacketReceiveHeaderHandler`：
   - 读取 16B 基础头。
   - 解析 `ProtocolVersion` 和 Flags。
   - `Reliable = 1` 时读取 24B 扩展。
4. 修改接收状态机：
   - 不再硬编码 14。
   - 以当前包实际头长计算 body offset。
5. TCP、WebSocket、KCP 接收路径统一使用实际头长。

服务端任务：

1. 修改消息解码器和管道过滤器：
   - `IMessageEncoderHandler.PackageHeaderLength` 从旧长度升级。
   - TCP/WebSocket/KCP 的包头解析同步更新。
2. `NetworkMessagePackage.Header` 增加：
   - `HeaderFlags`
   - `SessionId`
   - `ReliableSequence`
   - `AckSequence`
3. 心跳和控制包允许短头或明确的控制头策略，业务包统一 40B。

验收：

- 双端普通业务消息可用新头收发。
- 包头字段偏移单元测试通过。
- 旧 14B 包被明确拒绝，不误解析。

### Phase 2：统一活性状态机和业务事件

目标：把历史分支的自动重连升级为统一状态模型。

吸收历史分支：

- 连接地址和 `userData` 保存。
- `ServerKick` 关闭原因。
- 指数退避状态对象。
- 手动重连、取消重连 API。

重构方向：

1. 不直接暴露旧的 `NetworkReconnecting`、`NetworkReconnected`、`NetworkReconnectFailed` 作为最终业务语义。
2. 统一事件按用途分两类：
   - UI/提示类：
     - `NetworkSuspected`
     - `NetworkSilentResuming`
     - `NetworkResumed`
   - 业务决策类：
     - `NetworkBusinessReconnectRequired`
     - `NetworkClosed`
     - `NetworkError`
3. 统一业务层事件：
   - `NetworkConnected`
   - `NetworkSuspected`
   - `NetworkSilentResuming`
   - `NetworkResumed`
   - `NetworkBusinessReconnectRequired`
   - `NetworkClosed`
   - `NetworkError`
4. 事件参数统一包含：
   - `ChannelName`
   - `TransportType`
   - `Reason`
   - `SessionId`
   - `LastAckSequence`
   - `PendingCount`
   - `ElapsedSeconds`
   - `Recoverable`
   - `ErrorCode`
   - `Message`

客户端任务：

1. 新增 `NetworkLivenessState`：
   - `Connected`
   - `SuspectDisconnected`
   - `SilentReconnecting`
   - `Resuming`
   - `BusinessReconnecting`
   - `Closed`
2. 新增统一输入事件：
   - `TransportDisconnected`
   - `TransportError`
   - `HeartbeatTimeout`
   - `NoPacketTimeout`
   - `ResumeAccepted`
   - `ResumeRejected`
3. 任意有效入包刷新活性时间。
4. TCP/WebSocket 断开事件进入静默重连，不直接失败业务。
5. UDP 主要依赖心跳和入包时间。
6. KCP 复用同一上层状态机。
7. `ServerKick` 直接进入业务重连或关闭，不触发静默恢复。

服务端任务：

1. 统一心跳响应携带当前 ACK 信息。
2. 为 TCP/WebSocket/KCP/UDP 连接上报统一断开原因。
3. 支持服务器主动踢人控制消息或关闭原因。

验收：

- TCP/WebSocket 断开事件和心跳超时都进入同一状态机。
- UDP 无入包超时能触发 `NetworkSuspected`。
- 服务器踢人不触发静默重连。
- 短闪断恢复成功时不要求业务层重新登录。

### Phase 3：客户端 pending FIFO 可靠队列

目标：客户端具备强制 FIFO 入队、ACK 清理和补发能力。

客户端任务：

1. 新增 `ReliableSequenceGenerator`。
2. 新增 `PendingReliableMessage`：
   - `ReliableSequence`
   - `UniqueId`
   - `MessageId`
   - `OperationType`
   - `SerializedPacket`
   - `RetryCount`
   - `CreatedTime`
   - `LastSendTime`
3. 新增 `ReliableSendQueue`：
   - 业务消息强制入队。
   - 按 FIFO 顺序发送。
   - ACK 后从队首清理。
   - 重连补发使用原始 `SerializedPacket` 和原始 `ReliableSequence`。
4. `Send()` 和 `Call()` 统一进入 reliable FIFO。
5. 心跳、ACK、Resume、Close 等控制包不进入业务 FIFO。
6. RPC 超时在静默恢复期间暂停计时，业务重连时失败化。

服务端任务：

1. 暂时可只回 ACK，不启用复杂响应缓存。
2. 确保 ACK 能让客户端清理 pending。

验收：

- 客户端发送 1、2、3，收到 ACK 2 后只保留 3。
- 断线后 pending 不丢。
- 业务重连时 pending 清空，RPC 失败化。

### Phase 4：服务端 FIFO 会话与幂等

目标：服务端能安全处理补发和重复消息。

服务端任务：

1. 新增可靠会话状态：
   - `SessionId`
   - `LastProcessedClientSequence`
   - `LastAckedServerSequence`
   - `PendingResponseCache`
   - `LoginResponseCache`
   - `ExpireAt`
2. 登录成功生成 `SessionId` 并返回客户端。
3. 收到业务消息：
   - `seq == last + 1`：执行业务，推进 ACK。
   - `seq <= last`：不重复执行业务，返回缓存响应或 ACK。
   - `seq > last + 1`：不执行业务，返回当前 ACK。
4. 对 RPC 响应缓存最近 N 条或 TTL 内响应。
5. 会话状态 TTL 默认 30 秒。
6. 会话清理后台任务。

客户端任务：

1. 消费服务端 ACK。
2. 处理 Duplicate 标记。
3. 对重复响应按 `UniqueId` 正常完成 RPC。

验收：

- 重复发送同一 `ReliableSequence` 不重复执行业务。
- 未来序号不会越序执行业务。
- Resume 后服务端返回准确 ACK。

### Phase 5：Resume 和静默恢复闭环

目标：短闪断业务层无感，pending 继续处理。

客户端任务：

1. 连接恢复后进入 `Resuming`。
2. 发送 Resume 控制包：
   - `SessionId`
   - `LastClientReliableSequence`
   - `LastReceivedServerSequence`
   - `AckSequence`
3. Resume 成功：
   - 清理已 ACK pending。
   - 按 FIFO 补发剩余 pending。
   - 抛 `NetworkResumed`。
4. Resume 失败：
   - 清空 pending。
   - RPC 失败化。
   - 抛 `NetworkBusinessReconnectRequired`。

服务端任务：

1. 验证 `SessionId` 是否存在且未过期。
2. 返回 `ResumeResult`：
   - `CanResume`
   - `AckSequence`
   - `Reason`
3. 恢复连接到会话映射。

验收：

- 10 秒内断线恢复，业务层不重新登录。
- 超过 10 秒或 Resume 失败，业务层收到重连要求。
- 服务端会话超过 30 秒后拒绝 Resume。

### Phase 6：重复登录与服务器踢人

目标：把历史分支“服务器踢人”能力纳入可靠协议。

客户端任务：

1. 支持服务器踢人控制消息。
2. 收到踢人：
   - 清空 pending。
   - RPC 失败化。
   - 抛 `NetworkBusinessReconnectRequired`，由 Reason 区分具体踢人原因。
3. 不触发静默重连。

服务端任务：

1. 登录成功缓存原响应：
   - `LoginKey`
   - `SessionId`
   - `LoginResponseBytes`
   - `ExpireAt`
2. 重复登录命中缓存：
   - 返回原登录响应。
   - 保持相同 `SessionId`。
   - 旧连接按策略踢下线或替换。
3. 踢人消息设置明确原因：
   - 重复登录
   - 管理后台踢人
   - 账号封禁
   - 会话被替换

验收：

- 重复登录返回原响应。
- 服务器主动踢人不触发自动重连。
- 业务层能根据 Reason 做 UI 或重新登录。

### Phase 7：跨传输集成

目标：TCP、UDP、WebSocket、KCP 行为一致。

客户端任务：

1. TCP 使用传输断开事件 + 心跳兜底。
2. WebSocket 使用关闭/错误事件 + 心跳兜底。
3. UDP 使用心跳和入包时间作为核心判断。
4. KCP 使用底层事件 + 心跳兜底，不单独维护业务可靠队列。

服务端任务：

1. TCP/WebSocket/KCP/UDP 的消息包都进入同一可靠会话处理器。
2. KCP Session 与业务 `SessionId` 解耦。
3. UDP Endpoint 变化时以 `SessionId` 恢复业务会话。

验收：

- 四种传输都通过同一套 ACK/Resume/FIFO 测试。
- UDP 切换网络后能通过 `SessionId` 静默恢复。
- KCP 重建 conversation 后仍可通过 Resume 恢复业务 pending。

### Phase 8：测试、压测与发布

测试清单：

1. 16B 基础头和 40B 可靠头字段偏移。
2. HeaderFlags 版本位和标记位。
3. ACK 清理 pending。
4. FIFO 缺口不执行业务。
5. 重复消息返回缓存响应。
6. 重复登录返回原响应。
7. 服务器踢人不重连。
8. TCP/WebSocket 断开后静默恢复。
9. UDP 无入包后疑似断线和恢复。
10. KCP 连接重建后 Resume。
11. 10 秒内恢复成功。
12. 超过 10 秒转业务重连。
13. 30 秒后服务端拒绝 Resume。
14. pending 队列容量上限和内存压力。
15. 大包压缩和最大包长限制。

发布要求：

- 客户端 Unity 包 major 版本升级。
- 服务端同步发布。
- CHANGELOG 标记 BREAKING CHANGES。
- README 和服务端文档同步更新新协议。

## 六、建议

1. 不要直接合并 `feature/network-reconnect-and-server-kick`，而是按本方案挑选能力重做。
2. 优先实现服务端幂等和 ACK，再启用客户端补发。
3. `NetworkBusinessReconnectRequired` 应该是业务层唯一必须处理的重连事件。
4. `NetworkSuspected`、`NetworkSilentResuming`、`NetworkResumed` 主要用于 UI 弱提示、日志和监控。
5. `SessionId` 必须由服务端生成，并且与 TCP/WebSocket/KCP/UDP 的底层连接 ID 解耦。
6. 重复响应缓存至少覆盖登录响应和可靠 RPC 响应，否则重复包只能 ACK，RPC 调用方可能拿不到结果。
7. 历史分支中与性能优化相反的改动不要吸收，包括普通集合替代并发集合、`ToList()` 复制、package 元数据回退。
8. EventArgs 必须保持现有 ReferencePool 生命周期规范，NetworkManager 和 EventComponent 转发时不要共享会被释放的实例。

## 七、待确认问题

以下为建议决策，除非后续业务有明确反例，否则按这些默认值实施。

### 1. 业务层事件兼容策略

建议：不保留旧 `NetworkReconnecting/Reconnected/ReconnectFailed` 作为业务层主事件，只保留为内部过渡或废弃别名。正式对业务层暴露统一事件：

```text
NetworkSuspected
NetworkSilentResuming
NetworkResumed
NetworkBusinessReconnectRequired
NetworkClosed
NetworkError
```

原因：

- 旧事件只表达“正在重连/重连成功/重连失败”，无法区分静默恢复和业务重连。
- 当前方案的关键分界不是“是否重连成功”，而是“是否 Resume 成功、pending 是否还能继续”。
- 保留两套正式事件会让业务层出现重复处理和状态分叉。

目的：

- 让业务层只处理稳定语义。
- 降低 TCP、UDP、WebSocket、KCP 差异外泄到业务层的概率。
- 为后续统一监控、日志和 UI 提示提供一致事件源。

### 2. 服务器踢人事件策略

建议：服务器踢人统一抛 `NetworkBusinessReconnectRequired`，并通过 `Reason` 区分具体原因。主动关闭或完全关闭再抛 `NetworkClosed`。

推荐 Reason：

```text
ServerKick
DuplicateLogin
SessionReplaced
AccountBanned
AdminKick
SessionExpired
```

原因：

- 服务器踢人通常需要业务层介入，例如返回登录页、弹窗、重新鉴权或展示封禁提示。
- 如果只抛 `NetworkClosed`，业务层还需要再判断是否要重新登录，语义不够直接。
- `NetworkBusinessReconnectRequired` 已经是“旧 pending 不可信，业务必须介入”的统一事件。

目的：

- 让业务层在一个事件入口处理所有不可静默恢复场景。
- 避免服务器踢人误触发静默重连。
- 保留 `Reason` 供 UI、日志和账号策略区分。

### 3. 登录请求 FIFO 策略

决策：严格要求 FIFO。登录请求也进入可靠语义，但登录是建立 `SessionId` 的特殊首包。

建议：

- 未登录前 `SessionId = 0`。
- 登录请求携带 `ReliableSequence = 1`。
- 登录成功后服务端返回正式 `SessionId`。
- 登录响应需要进入登录响应缓存，重复登录返回原响应。
- 登录成功后的业务消息从同一 FIFO 序号继续，或由服务端返回新的起始序号，推荐继续使用同一序号空间。

原因：

- 你要求严格 FIFO，登录是后续会话和业务状态的根。
- 如果登录不可靠，后续业务可靠没有完整上下文。
- 重复登录返回原响应依赖登录请求具备可识别、可缓存、可去重的可靠语义。

目的：

- 保证会话建立过程也具备幂等能力。
- 避免登录重试导致重复创建角色会话或重复初始化业务状态。
- 让客户端从连接建立到业务请求都在同一套可靠模型内。

### 4. 服务端 RPC 响应缓存策略

建议：每个 `SessionId` 缓存最近 256 条可靠响应，TTL 30 秒，二者任一条件满足即淘汰。

```text
ReliableResponseCacheMaxCount = 256
ReliableResponseCacheTtlSeconds = 30
LoginResponseCacheTtlSeconds = 30
```

原因：

- 30 秒与服务端会话 TTL 对齐，避免会话还在但响应已经不可恢复。
- 256 条对多数游戏短闪断足够覆盖高峰请求，同时内存可控。
- 只按 TTL 可能遇到短时间请求暴增造成内存压力；只按条数可能在低频业务下过早丢失响应。

目的：

- 重复 RPC 能返回原响应，而不是只返回 ACK。
- 避免客户端 RPC 调用方因重复包拿不到结果。
- 控制服务端内存上限。

### 5. UDP Endpoint 迁移策略

建议：允许同一 `SessionId` 在静默窗口内迁移到新 UDP Endpoint，但必须通过 Resume 验证。

规则：

- 客户端更换网络后 Endpoint 变化，不直接判定为新会话。
- 服务端收到带有效 `SessionId` 的 Resume 后，可以把会话绑定迁移到新 Endpoint。
- 迁移必须校验登录 token、会话 token 或 Resume token。
- 同一 `SessionId` 同时出现多个 Endpoint 时，以最后一次 Resume 成功的 Endpoint 为准，旧 Endpoint 作废。

原因：

- 移动网络切 Wi-Fi/蜂窝时 UDP Endpoint 变化很常见。
- 如果不允许迁移，UDP 很难做到静默恢复。
- 只凭 `SessionId` 迁移有劫持风险，所以必须配合 token 验证。

目的：

- 提升 UDP/KCP 弱网和切网体验。
- 保持业务层对短闪断无感。
- 在允许恢复的同时控制会话劫持风险。

### 6. 静默重连期间 RPC 超时策略

建议：静默重连期间暂停 RPC 超时计时；进入业务重连后立即失败化 pending RPC。

规则：

- `Connected` 状态正常计时。
- `SuspectDisconnected`、`SilentReconnecting`、`Resuming` 暂停计时。
- `NetworkResumed` 后恢复计时。
- `NetworkBusinessReconnectRequired` 后失败化所有未完成 RPC。

原因：

- 静默恢复的目标是业务层无感，如果 RPC 在恢复窗口内超时，会破坏无感体验。
- 10 秒静默窗口本身已经是恢复上限，不需要 RPC 超时重复施压。
- 业务重连后旧 pending 不可信，继续等待没有意义。

目的：

- 避免短闪断导致大量误超时。
- 保持 RPC 生命周期和可靠恢复状态一致。
- 让业务层只在真正不可恢复时收到失败。

### 7. 下行业务消息 FIFO 策略

决策：服务端是单线程，一次只处理一个任务。建议下行业务消息也纳入强制 FIFO reliable，但可以分阶段开启。

建议：

- 协议层从一开始支持双向 `ReliableSequence`。
- Phase 1-5 优先完成客户端上行可靠和服务端 ACK。
- Phase 6 后开启服务端下行 FIFO reliable。
- 服务端下行序号和客户端上行序号独立维护。

原因：

- 服务端单线程降低了下行排序复杂度，天然更适合维护 FIFO。
- 如果只做上行可靠，下行通知仍可能在断线恢复后丢失或乱序。
- 分阶段开启可以降低首轮实现风险。

目的：

- 最终实现双向业务可靠。
- 保证服务端主动通知、踢人、状态变更、RPC 响应在恢复后也有一致语义。
- 不阻塞前期上行可靠闭环落地。

### 8. 可靠队列容量默认值

建议默认值：

```text
PendingQueueMaxCount = 1024
PendingQueueMaxBytes = 4 MB
PendingMessageMaxBytes = 256 KB
ReliableRetryLimit = 5
ReliableMessageTimeoutSeconds = 15
```

原因：

- 1024 条足够覆盖短时间断网期间的大多数业务请求。
- 4 MB 对移动端内存压力可控，也能容纳一定数量的中型请求。
- 单条 256 KB 可避免大包长期阻塞 FIFO 队列。
- FIFO 强制顺序下，大包和慢包会阻塞后续消息，必须设置单条上限。

目的：

- 防止断网期间 pending 无限增长。
- 防止异常大包拖垮 FIFO。
- 为业务层提供明确失败边界：超过容量后转业务重连或拒绝新请求。

超限策略：

- 超过 `PendingQueueMaxCount` 或 `PendingQueueMaxBytes`：触发 `NetworkBusinessReconnectRequired`，清理 pending。
- 单条超过 `PendingMessageMaxBytes`：发送前拒绝，并抛 `NetworkError`。
- 业务层可根据项目压测调整默认值。

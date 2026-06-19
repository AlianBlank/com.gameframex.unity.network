# Step 01 — kcp2k 源码集成现状

## 当前状态

kcp2k 源码已集成在 `Runtime/Network/Network/Kcp/ThirdParty/kcp2k/`，命名空间保持为 `kcp2k`。当前代码直接引用 `kcp2k.Kcp`，没有对第三方源码做命名空间包装。

## 文件清单

| 文件 | 当前行数 | 说明 |
| --- | ---: | --- |
| `Kcp.cs` | 1124 | KCP 协议核心实现 |
| `Segment.cs` | 78 | KCP 数据段 |
| `Utils.cs` | 76 | 编解码工具 |
| `Pool.cs` | 46 | 通用对象池 |
| `THIRD_PARTY_NOTICES.md` | 已存在 | 第三方声明 |

## 已核对实现

- `AckItem` 是 `Kcp.cs` 顶部的 `internal struct`，不是独立文件。
- `Kcp` 暴露当前通道所需 API：`Send`、`Receive`、`Input`、`Update`、`PeekSize`、`SetNoDelay`、`SetMtu`、`SetWindowSize`。
- `Segment` 内部使用 `MemoryStream` 存储段数据。
- 当前源码没有使用 `Span<T>`。
- 所有第三方源码文件均位于 `kcp2k` 命名空间。

## 依赖关系

`NetworkManager.KcpNetworkChannel.cs` 通过 `using kcp2k;` 直接创建：

```csharp
m_Kcp = new Kcp(m_KcpConfig.ConversationId, OnKcpOutput);
```

随后按 `KcpConfig` 设置 no-delay、MTU 和窗口大小。

## 验收标准

- [x] `Kcp.cs`、`Segment.cs`、`Utils.cs`、`Pool.cs` 均存在且非空。
- [x] 命名空间保持 `kcp2k`。
- [x] 无独立 `AckItem.cs`。
- [x] `THIRD_PARTY_NOTICES.md` 已存在。
- [x] 每个文件均有对应 `.meta` 文件。
- [ ] Unity 编译通过仍需在 Unity 环境中验证。

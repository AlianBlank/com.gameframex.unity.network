## [2.6.10](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.9...2.6.10) (2026-06-19)


### Bug Fixes

* **network:** 将 JSON 库从 Newtonsoft 切换为 LitJSON ([b1b0ce2](https://github.com/gameframex/com.gameframex.unity.network/commit/b1b0ce2f62d9b57b9f279967921e3e15001dee20))

## [2.6.9](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.8...2.6.9) (2026-06-15)


### Bug Fixes

* **websocket:** 简化 WebSocket 连接逻辑 ([9fda842](https://github.com/gameframex/com.gameframex.unity.network/commit/9fda842e3c62c2ebdbf2bb437b8765de5b8b7a3a)), closes [#163](https://github.com/gameframex/com.gameframex.unity.network/issues/163)

## [2.6.8](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.7...2.6.8) (2026-06-10)


### Bug Fixes

* **network:** 移除消息体长度为零的检查 ([0b3bfac](https://github.com/gameframex/com.gameframex.unity.network/commit/0b3bfacde4916443e057f765ecb53c713eb55309))

## [2.6.7](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.6...2.6.7) (2026-06-07)


### Bug Fixes

* 补全包规范文件（LICENSE/CHANGELOG/URL 字段/unity 字段） ([02a2833](https://github.com/gameframex/com.gameframex.unity.network/commit/02a2833333f7520f263d5a91ce993f8014d17eb3))

## [2.6.6](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.5...2.6.6) (2026-06-05)


### Bug Fixes

* **network:** 网络事件重新创建 EventArgs 实例触发 ([2e7f8a9](https://github.com/gameframex/com.gameframex.unity.network/commit/2e7f8a9d54f480fec89cf72214a8f88a3c0b38db))

## [2.6.5](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.4...2.6.5) (2026-06-02)


### Bug Fixes

* **network:** 恢复网络事件 EventArgs 的引用池回收 ([f9f109e](https://github.com/gameframex/com.gameframex.unity.network/commit/f9f109e89b4954f41f4fe3b478d963ee5b5460c1))

## [2.6.4](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.3...2.6.4) (2026-06-02)


### Bug Fixes

* **network:** 使用 TryDequeue 循环替代 ConcurrentQueue.Clear ([8d3a1fe](https://github.com/gameframex/com.gameframex.unity.network/commit/8d3a1fe5fc093fe65a0dfa8a38d12b6c0c3335eb))

## [2.6.3](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.2...2.6.3) (2026-06-02)


### Bug Fixes

* **network:** 集合预分配初始容量避免首次扩容 ([8fab110](https://github.com/gameframex/com.gameframex.unity.network/commit/8fab110fed45c5351a85c74e139587bed8048405))

## [2.6.2](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.1...2.6.2) (2026-06-01)


### Bug Fixes

* **network:** m_PActive 和 PIsConnecting 标记 volatile 保证跨线程可见性 ([857e22a](https://github.com/gameframex/com.gameframex.unity.network/commit/857e22ad0b0856fd21c8914f09bfc359180124b0))
* **network:** RpcState 超时收集改为预分配实例字段消除每帧 GC ([986acf6](https://github.com/gameframex/com.gameframex.unity.network/commit/986acf68ba0adaef3a22675b1fc066d96b416edf))
* **network:** 收发计数器改用 Interlocked 操作修复多线程可见性 ([ea45494](https://github.com/gameframex/com.gameframex.unity.network/commit/ea45494ec41d63d7e77b37e919012132f42ca73a))
* **network:** 消息接收队列改为 ConcurrentQueue 修复线程安全问题 ([cc363a7](https://github.com/gameframex/com.gameframex.unity.network/commit/cc363a7434c73f643b889fcf526002c3366a8972))


### Performance Improvements

* **network:** 集合预分配初始容量避免首次扩容 ([9b1b590](https://github.com/gameframex/com.gameframex.unity.network/commit/9b1b5904f125e667bda73bbb10684993e50d0e2d))

## [2.6.1](https://github.com/gameframex/com.gameframex.unity.network/compare/2.6.0...2.6.1) (2026-06-01)


### Bug Fixes

* **network:** Call 添加类型不匹配异常；移除 Send 重复 null 检查；HeartBeatInterval 添加负值校验 ([375372b](https://github.com/gameframex/com.gameframex.unity.network/commit/375372b7ef6ec5fe9c0241ff94c71db8452e97b6))
* **network:** Close 使用 RpcState.Reset 替代 Dispose，允许重连后 RPC 正常工作 ([2d0e7e2](https://github.com/gameframex/com.gameframex.unity.network/commit/2d0e7e29fd2fe52d39f0d03d39156badb3c8be1d))
* **network:** DefaultMessageSerializer 私有构造函数、IPacketHandler 文档、ProtoMessageHandler Obsolete 标记为错误、ProtoMessageIdHandler 清理 HeartBeatList、RpcMessageData 时间单位注释 ([29a2883](https://github.com/gameframex/com.gameframex.unity.network/commit/29a2883e4b91214e8f59a2611977052b9f97f32b))
* **network:** DefaultNetworkChannelHelper _event 改为实例字段；添加 null 检查 ([052f27d](https://github.com/gameframex/com.gameframex.unity.network/commit/052f27df5e0709481866731f9024d394403d3238))
* **network:** DefaultPacketReceiveBodyHandler 检查 GetRespTypeById 返回 null ([8bd3272](https://github.com/gameframex/com.gameframex.unity.network/commit/8bd3272483cb1c7153dc4a0b5a125661afd59b07))
* **network:** DefaultPacketReceiveHeaderHandler 添加入参长度校验；重命名局部变量避免与属性混淆 ([dd1577f](https://github.com/gameframex/com.gameframex.unity.network/commit/dd1577f482e55f6cb4526cd22b8b71c5a6850910))
* **network:** DefaultPacketSendBodyHandler/CompressHandler/DecompressHandler 添加参数 null 检查 ([f599f95](https://github.com/gameframex/com.gameframex.unity.network/commit/f599f95ed427e0e2cabc890414e4b4b87a34052a))
* **network:** DefaultPacketSendHeaderHandler 添加序列化结果 null 检查；m_Offset 改为局部变量防止重入 ([ad5e664](https://github.com/gameframex/com.gameframex.unity.network/commit/ad5e6642fa5925792602837271eb99f12580f435))
* **network:** MessageHandlerAttribute 移除 ?. 静默吞异常；改用 Type 直接比较替代 FullName；修正 typo ([42f5c22](https://github.com/gameframex/com.gameframex.unity.network/commit/42f5c22d332c0120e6eba61660ebf242743968d4))
* **network:** MessageSerializerRegistry._global 添加 volatile 保证多线程可见性 ([eebe735](https://github.com/gameframex/com.gameframex.unity.network/commit/eebe735754976990f4a5d3520411c934ffee4479))
* **network:** NetworkClosedEventArgs.Clear 补充重置 Reason 和 ErrorCode，修复引用池脏数据 ([958ca04](https://github.com/gameframex/com.gameframex.unity.network/commit/958ca04d238fd7a3ac023a18dc6ceb286b7ff14b))
* **network:** NetworkComponent 事件处理器添加 m_EventComponent null 检查，防止 Awake/Start 间触发 NRE ([4d839e4](https://github.com/gameframex/com.gameframex.unity.network/commit/4d839e4ee67633ca6878003877423a5ea6a5251e))
* **network:** NetworkComponentInspector 访问 Socket 端点时添加 null 保护，防止 Inspector 崩溃 ([8bb5f4d](https://github.com/gameframex/com.gameframex.unity.network/commit/8bb5f4d6ee70eab0e50287f9c8604292c57fb735))
* **network:** ReceiveState/SendState.Reset 添加 disposed 检查，防止 Dispose 后 NRE ([45b8f38](https://github.com/gameframex/com.gameframex.unity.network/commit/45b8f38e8c7e34c3b7b0a642a7dd7d4b111f23c0))
* **network:** RpcMessageData.Dispose 取消等待中 TaskCompletionSource，防止 await 永久挂起 ([489edec](https://github.com/gameframex/com.gameframex.unity.network/commit/489edec33ec4107ed3bce2c945d814ed93261b04))
* **network:** RpcState Dispose 时取消等待中 TCS；新增 Reset 方法支持重连场景 ([b69157c](https://github.com/gameframex/com.gameframex.unity.network/commit/b69157cf00a431de5dba40243d4a74603205b074))
* **network:** RpcState.Call 处理 TryAdd 返回 false 的情况，修复 TOCTOU 问题 ([c8cefc2](https://github.com/gameframex/com.gameframex.unity.network/commit/c8cefc2060a1a4c09721bd0d7bbf3078b39f9407))
* **network:** SystemTcpNetworkChannel 修复接收失败处理、反序列化入队、连接事件顺序、bodyLength 校验 ([6b743db](https://github.com/gameframex/com.gameframex.unity.network/commit/6b743db57857f6d69004472861ed3fec720aa6ef))
* **network:** Update 使用快照遍历防止回调中修改集合；移除 DestroyNetworkChannel 冗余 null 检查 ([9c7c6d6](https://github.com/gameframex/com.gameframex.unity.network/commit/9c7c6d68203f6be552cd57e8b36951f8a331a3d2))
* **network:** WebSocket 连接超时保护、CTS 释放、async void 异常保护、连接事件顺序、重复计数修复 ([e6dc9c4](https://github.com/gameframex/com.gameframex.unity.network/commit/e6dc9c4e8bfcfae85786c761592ba8bd09046130))
* **network:** 使用专用锁对象替代 EventHandler 委托实例作为 lock 目标，修复多线程竞态 ([b50b51f](https://github.com/gameframex/com.gameframex.unity.network/commit/b50b51f4b53a438a5b1ac8506a7af7c31669d06b))
* **network:** 将 ProcessHeartBeat 中 Close 调用移到锁外，避免嵌套锁死锁风险 ([3594aea](https://github.com/gameframex/com.gameframex.unity.network/commit/3594aeac7cf592900c11b785e61fc7f1bb454a03))


### Performance Improvements

* **network:** MessageHandlerAttribute.Invoke 预分配反射参数数组 ([54de39a](https://github.com/gameframex/com.gameframex.unity.network/commit/54de39af1c2fbe86da03ce976ee8f8a9d54169e8))
* **network:** ProtoMessageHandler.GetHandlers 去掉 ToList 复制 ([34c445d](https://github.com/gameframex/com.gameframex.unity.network/commit/34c445d6ab845b480d9765cfc1334ce0a3e2d5a3))
* **network:** Update 使用预分配列表快照遍历，消除每帧 GC 分配 ([dae5463](https://github.com/gameframex/com.gameframex.unity.network/commit/dae54639aaee149dd62a7969e897ed16df30ea12))

# [2.6.0](https://github.com/gameframex/com.gameframex.unity.network/compare/2.5.1...2.6.0) (2026-05-29)


### Features

* **serializer:** 新增 IMessageSerializer 可插拔序列化接口 ([8307320](https://github.com/gameframex/com.gameframex.unity.network/commit/830732097338b28f485f0d0236472b0edce024ec))

## [2.5.1](https://github.com/gameframex/com.gameframex.unity.network/compare/2.5.0...2.5.1) (2026-05-27)


### Bug Fixes

* **network:** 添加缺失的GameFrameX.Runtime命名空间引用 ([8b136bd](https://github.com/gameframex/com.gameframex.unity.network/commit/8b136bd19cd01721b92df83c61972f59c42112fa))

# [2.5.0](https://github.com/gameframex/com.gameframex.unity.network/compare/2.4.0...2.5.0) (2025-12-23)


### Bug Fixes

* **Network:** 优化RPC超时错误信息，包含消息类型全名 ([ce4fb0d](https://github.com/gameframex/com.gameframex.unity.network/commit/ce4fb0d704f04b9536ca0ba776a1cc4addae2322))
* **Network:** 修复RPC超时处理逻辑错误并优化性能 ([213b99d](https://github.com/gameframex/com.gameframex.unity.network/commit/213b99dac04baba62fd5225abfc86d33a9e54e31))
* **Network:** 修复RPC错误码处理条件判断错误 ([37c2cb1](https://github.com/gameframex/com.gameframex.unity.network/commit/37c2cb10484381a57de27c79a33d8c34f6fa503a))
* **Network:** 修正应用获得焦点时心跳包的默认行为 ([b5840f6](https://github.com/gameframex/com.gameframex.unity.network/commit/b5840f62295458cf37a87db9dafbe6b4dfafdf20))
* **Network:** 修正接收消息日志中使用错误的MessageId获取方法 ([09c5853](https://github.com/gameframex/com.gameframex.unity.network/commit/09c58538831507e8d1e4f69cb552c7605cf3bc75))
* **Network:** 修正网络日志中消息ID的获取方式 ([d60741a](https://github.com/gameframex/com.gameframex.unity.network/commit/d60741af11e2d9594b9d3f1ecea703ff5f18a2b7))
* **Network:** 移除未实现的Close方法并修正SystemTcpNetworkChannel中的Close调用 ([c07dee1](https://github.com/gameframex/com.gameframex.unity.network/commit/c07dee1191f8f5f42982de7c94b2e0dbd77384c9))
* **Network:** 调整消息发送和日志记录顺序以避免潜在问题 ([38a77c7](https://github.com/gameframex/com.gameframex.unity.network/commit/38a77c7598ee0f503c0ff1cf0b986aef8d478d63))
* 在NetworkChannelBase中释放messageObject引用 ([d8634ec](https://github.com/gameframex/com.gameframex.unity.network/commit/d8634ecd9cf8d32851d83f30d53e650b9a00bdcb))


### Features

* **network:** 为网络关闭添加原因和错误码参数 ([688de90](https://github.com/gameframex/com.gameframex.unity.network/commit/688de90357b6b053ae4d382d147f97397319bbb7))
* **网络:** 为RPC调用添加忽略错误码选项 ([f50f845](https://github.com/gameframex/com.gameframex.unity.network/commit/f50f84505f8cf20d78cdac9e31e61344795b7a8b))
* **网络:** 添加失去焦点时发送心跳包的功能 ([1927396](https://github.com/gameframex/com.gameframex.unity.network/commit/1927396e6eadd9180636f252ba2ab867c01bea25))
* **网络:** 添加应用获得焦点时发送心跳包的功能 ([ccc8504](https://github.com/gameframex/com.gameframex.unity.network/commit/ccc8504dffd6ac81023244c1d5d217650891afc9))

# Changelog

## [2.4.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.4.0) (2025-10-15)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.3.4...2.4.0)

## [2.3.4](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.3.4) (2025-05-31)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.3.3...2.3.4)

## [2.3.3](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.3.3) (2025-05-19)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.3.2...2.3.3)

## [2.3.2](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.3.2) (2025-05-13)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.3.1...2.3.2)

## [2.3.1](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.3.1) (2025-05-10)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.3.0...2.3.1)

## [2.3.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.3.0) (2025-02-20)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.2.2...2.3.0)

## [2.2.2](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.2.2) (2025-02-18)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.2.1...2.2.2)

## [2.2.1](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.2.1) (2025-02-08)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.2.0...2.2.1)

## [2.2.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.2.0) (2025-02-07)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.1.0...2.2.0)

## [2.1.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.1.0) (2025-02-05)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.0.1...2.1.0)

## [2.0.1](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.0.1) (2025-01-02)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2.0.0...2.0.1)

## [2.0.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/2.0.0) (2024-11-29)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.1.0...2.0.0)

## [1.1.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.1.0) (2024-10-11)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.19...1.1.0)

## [1.0.19](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.19) (2024-09-23)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.18...1.0.19)

## [1.0.18](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.18) (2024-09-09)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.17...1.0.18)

## [1.0.17](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.17) (2024-09-09)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.16...1.0.17)

## [1.0.16](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.16) (2024-09-09)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.15...1.0.16)

## [1.0.15](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.15) (2024-08-15)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.14...1.0.15)

## [1.0.14](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.14) (2024-08-13)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.13...1.0.14)

## [1.0.13](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.13) (2024-08-13)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.12...1.0.13)

**Merged pull requests:**

- \[修改\]1. 修改消息线程问题 [\#1](https://github.com/GameFrameX/com.gameframex.unity.network/pull/1) ([StarryGaming](https://github.com/StarryGaming))

## [1.0.12](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.12) (2024-08-05)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.11...1.0.12)

## [1.0.11](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.11) (2024-08-05)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.10...1.0.11)

## [1.0.10](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.10) (2024-08-05)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.9...1.0.10)

## [1.0.9](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.9) (2024-07-06)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.8...1.0.9)

## [1.0.8](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.8) (2024-07-02)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.7...1.0.8)

## [1.0.7](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.7) (2024-06-28)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.6...1.0.7)

## [1.0.6](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.6) (2024-06-28)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.5...1.0.6)

## [1.0.5](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.5) (2024-06-21)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.4...1.0.5)

## [1.0.4](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.4) (2024-06-19)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.3...1.0.4)

## [1.0.3](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.3) (2024-05-26)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.2...1.0.3)

## [1.0.2](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.2) (2024-05-25)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.1...1.0.2)

## [1.0.1](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.1) (2024-05-20)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/1.0.0...1.0.1)

## [1.0.0](https://github.com/GameFrameX/com.gameframex.unity.network/tree/1.0.0) (2024-04-10)

[Full Changelog](https://github.com/GameFrameX/com.gameframex.unity.network/compare/2c9e2d4d42112ca6848ad5cd9dfa5093d1cd93e0...1.0.0)



\* *This Changelog was automatically generated by [github_changelog_generator](https://github.com/github-changelog-generator/github-changelog-generator)*

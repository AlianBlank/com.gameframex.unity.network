# KCP 架构测试验收报告

## 测试范围

本次根据 `Docs/kcp-architecture/Step-08-verify.md` 补充并执行可自动化的 EditMode 单元测试。测试范围集中在：

- KCP 工厂路由和向后兼容创建入口。
- KCP 配置非法参数校验。
- URI scheme 错误路径。
- UDP Transport 基础连接与关闭幂等。
- TCP Transport 长度前缀发送、拆包、连续帧、超大帧关闭。
- KCP 通道复用 `NetworkChannelBase` 心跳逻辑。
- `PooledBuffer` 生命周期。

## 新增/补强测试

新增或补强的测试用例包括：

- `KcpServiceTypesCreateKcpChannel`
- `LegacyFactoryCreatesSystemTcpChannel`
- `KcpUriChannelRejectsUnsupportedScheme`
- `KcpChannelRejectsInvalidWindowAndMessageConfigBeforeOpeningTransport`
- `KcpUdpTransportCloseInvokesClosedCallbackOnce`
- `KcpChannelHeartbeatUsesNetworkChannelBaseHeartbeat`
- `KcpTcpTransportSendRawWritesLengthPrefixedFrame`
- `KcpTcpTransportReceivesSplitLengthAndBody`
- `KcpTcpTransportReceivesConsecutiveFrames`

## 执行命令

快速编译验证：

```bash
dotnet build /Users/mac/Documents/GithubWorks/GameFrameX.Unity/GameFrameX.Network.Tests.csproj --no-restore
```

Unity EditMode 测试（StandaloneOSX）：

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -quit \
  -projectPath "/Users/mac/Documents/GithubWorks/GameFrameX.Unity" \
  -buildTarget StandaloneOSX \
  -executeMethod GameFrameX.Network.Tests.Editor.NetworkFunctionalTestRunner.RunEditModeTests \
  -testResults "Packages/com.gameframex.unity.network/TestResults.compile-osx.xml" \
  -logFile "/tmp/GFXNetworkBuild/StandaloneOSX/unity-compile-test-fixed.log"
```

Unity EditMode 测试（WebGL）：

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -quit \
  -projectPath "/Users/mac/Documents/GithubWorks/GameFrameX.Unity" \
  -buildTarget WebGL \
  -executeMethod GameFrameX.Network.Tests.Editor.NetworkFunctionalTestRunner.RunEditModeTests \
  -testResults "Packages/com.gameframex.unity.network/TestResults.compile-webgl.xml" \
  -logFile "/tmp/GFXNetworkBuild/WebGL/unity-compile-test-fixed.log"
```

StandaloneOSX Player Build 验证：

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -quit \
  -projectPath "/Users/mac/Documents/GithubWorks/GameFrameX.Unity" \
  -buildTarget StandaloneOSX \
  -executeMethod GameFrameX.Network.Tests.Editor.NetworkBuildValidationRunner.BuildStandaloneOSX \
  -buildOutput "/tmp/GFXNetworkBuild/StandaloneOSX/GameFrameXNetworkValidation.app" \
  -buildSummary "/tmp/GFXNetworkBuild/StandaloneOSX/build-summary-fixed.txt" \
  -logFile "/tmp/GFXNetworkBuild/StandaloneOSX/unity-build-fixed.log"
```

StandaloneOSX Player Build 验证（临时关闭 HybridCLR 设置）：

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -quit \
  -projectPath "/Users/mac/Documents/GithubWorks/GameFrameX.Unity" \
  -buildTarget StandaloneOSX \
  -executeMethod GameFrameX.Network.Tests.Editor.NetworkBuildValidationRunner.BuildStandaloneOSX \
  -buildOutput "/tmp/GFXNetworkBuild/StandaloneOSX/GameFrameXNetworkValidation-nohybridclr.app" \
  -buildSummary "/tmp/GFXNetworkBuild/StandaloneOSX/build-summary-nohybridclr.txt" \
  -logFile "/tmp/GFXNetworkBuild/StandaloneOSX/unity-build-nohybridclr.log"
```

## 执行结果

`dotnet build /Users/mac/Documents/GithubWorks/GameFrameX.Unity/GameFrameX.Network.Tests.csproj --no-restore`：

- 结果：通过
- 错误：0
- 警告：4 个，均为项目既有程序集版本冲突和不可达代码警告，非本次测试代码引入

Unity 2022.3.62f2 EditMode（StandaloneOSX）：

- Passed: 28
- Failed: 0
- Skipped: 0
- Inconclusive: 0
- Duration: 0.1391715 秒

Unity 2022.3.62f2 EditMode（WebGL）：

- Passed: 28
- Failed: 0
- Skipped: 0
- Inconclusive: 0
- Duration: 0.1429047 秒

StandaloneOSX Player Build：

- Result: Failed
- TotalErrors: 2
- TotalWarnings: 1
- Output: `/tmp/GFXNetworkBuild/StandaloneOSX/GameFrameXNetworkValidation.app`
- 阻塞原因：项目级 HybridCLR 未初始化，Unity 日志报 `BuildFailedException: You have not initialized HybridCLR, please install it via menu 'HybridCLR/Installer'`。
- 结论：这是当前项目构建环境阻塞，不是 KCP 代码编译错误；本次自定义构建入口已经使用 `.app` 输出路径，规避了 StandaloneOSX 目录路径参数问题。

StandaloneOSX Player Build（临时关闭 `HybridCLRSettings.enable` 后）：

- Result: Succeeded
- TotalErrors: 0
- TotalWarnings: 4
- TotalTime: 00:03:27.6976615
- TotalSize: 1766532746
- Output: `/tmp/GFXNetworkBuild/StandaloneOSX/GameFrameXNetworkValidation-nohybridclr.app`
- 结论：KCP 代码可通过 StandaloneOSX IL2CPP Player Build；原失败由 HybridCLR 初始化环境造成。测试后已恢复 `ProjectSettings/HybridCLRSettings.asset` 的 `enable: 1`。

## 仍未覆盖

以下验收项仍需后续补充集成测试或平台构建验证：

- 真实 UDP/TCP KCP echo 端到端消息收发。
- GameFrameX 消息序列化/反序列化完整链路。
- `MaxMessageSize` 超限发送/接收触发 `NetworkChannelError`。
- TCP 连接失败、发送失败、对端主动关闭。
- Channel 关闭事件幂等。
- `NetworkMissHeartBeat` 事件抛出。
- RPC 请求/响应/超时/关闭清理。
- WebSocket `kcp-ws` / `kcp-wss` 真实连接。
- WebSocket 宏关闭路径编译。
- Mono Player 构建。
- WebGL Player 构建。

## 结论

当前 KCP 架构的基础工厂路由、配置校验、UDP/TCP transport 基础协议行为、心跳触发和池化缓冲生命周期已经具备自动化测试覆盖，并通过 Unity 2022.3.62f2 在 StandaloneOSX 与 WebGL 目标下的 EditMode 编译/测试验收。StandaloneOSX IL2CPP Player Build 在临时关闭 `HybridCLRSettings.enable` 后通过，说明 KCP 代码本身可进入真实 Player 构建链路；默认配置下的完整 Player Build 仍需先初始化 HybridCLR 后再验收。

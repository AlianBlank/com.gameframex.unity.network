<div align="center">

<img src="https://download.alianblank.com/gameframex/gameframex_logo_320.png" alt="Game Frame X Logo" width="160" />

# Game Frame X Network

[![License](https://img.shields.io/github/license/GameFrameX/com.gameframex.unity.network)](https://github.com/GameFrameX/com.gameframex.unity.network/blob/main/LICENSE.md)
[![Version](https://img.shields.io/github/v/release/GameFrameX/com.gameframex.unity.network)](https://github.com/GameFrameX/com.gameframex.unity.network/releases)
[![Unity Version](https://img.shields.io/badge/Unity-2019.4-black?logo=unity)](https://unity.com/)
[![Documentation](https://img.shields.io/badge/Documentation-docs-blue)](https://gameframex.doc.alianblank.com)

独立游戏前后端一体化解决方案 · 独立游戏开发者的圆梦大使

<br />

[文档](https://gameframex.doc.alianblank.com) · [快速开始](#快速开始) · QQ群: 467608841 / 233840761

<br />

[English](README.md) | **简体中文** | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md)

</div>

## 项目简介

**Network 长连接网络组件** - 提供 Unity 长连接网络组件相关的接口，包括 TCP、WebSocket 和自定义协议支持，使网络功能的使用更加简单高效。

### 功能特性

- 长连接网络支持（TCP / WebSocket / KCP）
- RPC 调用机制及超时处理
- 心跳包机制（支持应用获得/失去焦点时发送配置）
- Reliable FIFO 可靠消息协议，支持 ACK / Resume、pending 队列、重复响应幂等处理和服务器踢人语义
- 可插拔的消息序列化（`IMessageSerializer` 接口），支持两级注册（全局默认 + 按频道覆盖）
- 网络消息序列化与反序列化
- 网络频道管理
- 网络事件系统

## Reliable FIFO 协议

当前版本引入 Reliable FIFO 协议破坏性升级：包头升级为 16B 基础头和 40B 可靠扩展头，旧 14B 包头不再兼容。客户端和服务端必须同步升级协议常量、ACK / Resume 语义、pending 队列容量限制和服务器踢人原因映射。

发布前请确认：

- TCP 与 KCP over UDP/TCP/WebSocket 支持矩阵都通过同一套 ACK / Resume / FIFO 行为测试
- `PendingQueueMaxCount`、`PendingQueueMaxBytes`、`PendingMessageMaxBytes` 与服务端保持一致
- UDP Endpoint 迁移和 KCP 重建后只能通过 Resume 验证恢复 pending
- `ServerKick`、`DuplicateLogin`、`SessionReplaced`、`AccountBanned`、`AdminKick`、`SessionExpired` 都映射为业务重连事件

## 快速开始

### 安装

选择以下任一方式：

1. 编辑 Unity 项目的 `Packages/manifest.json`，添加 `scopedRegistries` 部分：
   ```json
   {
     "scopedRegistries": [
       {
         "name": "GameFrameX",
         "url": "https://gameframex.upm.alianblank.uk",
         "scopes": [
           "com.gameframex"
         ]
       }
     ],
     "dependencies": {
       "com.gameframex.unity.network": "2.6.6"
     }
   }
   ```

   `scopes` 控制哪些包通过此注册表解析。只有以 `com.gameframex` 开头的包才会从这个注册表获取。

2. 直接在 `manifest.json` 的 `dependencies` 节点下添加以下内容：
   ```json
   {
      "com.gameframex.unity.network": "https://github.com/gameframex/com.gameframex.unity.network.git"
   }
   ```
3. 在 Unity 的 `Package Manager` 中使用 `Git URL` 的方式添加库，地址为：`https://github.com/gameframex/com.gameframex.unity.network.git`
4. 直接下载仓库放置到 Unity 项目的 `Packages` 目录下，会自动加载识别。
## 平台支持

| 平台 | 支持 |
|------|------|
| Windows | 是 |
| macOS | 是 |
| Linux | 是 |
| Android | 是 |
| iOS | 是 |
| WebGL | 是 |

## 文档与资源

- [文档](https://gameframex.doc.alianblank.com)
- [GitHub 仓库](https://github.com/gameframex/com.gameframex.unity.network)

## 社区与支持

- QQ群：通过 [二维码](https://qm.qq.com/cgi-bin/qm/qr?k=ikT9gA5m2sKwOyNOfYmQvSAPK_c3GmD6) 加入

## 更新日志

详见 [CHANGELOG.md](CHANGELOG.md)。


## 依赖

| 包 | 说明 |
|----|------|
| `com.gameframex.unity` | 1.1.1 |
| `com.gameframex.unity.event` | 1.0.0 |
## 开源协议

详见 [LICENSE.md](LICENSE.md) 文件。

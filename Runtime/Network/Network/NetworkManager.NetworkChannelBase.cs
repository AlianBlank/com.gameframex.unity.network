//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    public sealed partial class NetworkManager
    {
        /// <summary>
        /// 网络频道基类。
        /// </summary>
        public abstract class NetworkChannelBase : INetworkChannel, IDisposable
        {
            private const float DefaultHeartBeatInterval = 30f;
            private const int DefaultMissHeartBeatCountByClose = 10;

            protected readonly Queue<MessageObject> PSendPacketPool;
            protected readonly INetworkChannelHelper PNetworkChannelHelper;
            protected AddressFamily PAddressFamily;

            /// <summary>
            /// 当收到数据包时是否重置心跳流逝时长
            /// </summary>
            protected bool PResetHeartBeatElapseSecondsWhenReceivePacket;

            /// <summary>
            /// 心跳间隔
            /// </summary>
            protected float PHeartBeatInterval;

            /// <summary>
            /// 心跳丢失次数
            /// </summary>
            protected int MissHeartBeatCountByClose;

            protected INetworkSocket PSocket;
            protected readonly SendState PSendState;
            protected readonly ReceiveState PReceiveState;
            protected readonly HeartBeatState PHeartBeatState;
            protected readonly RpcState PRpcState;
            protected int PSentPacketCount;
            protected int PReceivedPacketCount;
            private bool m_Disposed;
            private bool m_PActive;

            protected bool PActive
            {
                get => m_PActive;
                set
                {
                    m_PActive = value;
                    Log.Debug(value);
                }
            }

            private IPacketSendHeaderHandler m_PacketSendHeaderHandler;
            private IPacketSendBodyHandler m_PacketSendBodyHandler;
            private IPacketReceiveHeaderHandler m_PacketReceiveHeaderHandler;
            private IPacketReceiveBodyHandler m_PacketReceiveBodyHandler;
            private IPacketHeartBeatHandler m_PacketHeartBeatHandler;

            public Action<NetworkChannelBase, object> NetworkChannelConnected;
            public Action<NetworkChannelBase> NetworkChannelClosed;
            public Action<NetworkChannelBase, int> NetworkChannelMissHeartBeat;
            public Action<NetworkChannelBase, NetworkErrorCode, SocketError, string> NetworkChannelError;
            public Action<NetworkChannelBase, object> NetworkChannelCustomError;
            private Queue<MessageHandlerAttribute> m_ExecutionQueue = new Queue<MessageHandlerAttribute>();

            /// <summary>
            /// 初始化网络频道基类的新实例。
            /// </summary>
            /// <param name="name">网络频道名称。</param>
            /// <param name="networkChannelHelper">网络频道辅助器。</param>
            public NetworkChannelBase(string name, INetworkChannelHelper networkChannelHelper)
            {
                Name = name ?? string.Empty;
                PSendPacketPool = new Queue<MessageObject>(128);
                PNetworkChannelHelper = networkChannelHelper;
                PAddressFamily = AddressFamily.Unknown;
                PResetHeartBeatElapseSecondsWhenReceivePacket = false;
                PHeartBeatInterval = DefaultHeartBeatInterval;
                MissHeartBeatCountByClose = DefaultMissHeartBeatCountByClose;
                PSocket = null;
                PSendState = new SendState();
                PReceiveState = new ReceiveState();
                PHeartBeatState = new HeartBeatState();
                PRpcState = new RpcState();
                PSentPacketCount = 0;
                PReceivedPacketCount = 0;
                PActive = false;
                m_Disposed = false;

                NetworkChannelConnected = null;
                NetworkChannelClosed = null;
                NetworkChannelMissHeartBeat = null;
                NetworkChannelError = null;
                NetworkChannelCustomError = null;

                networkChannelHelper.Initialize(this);
            }

            #region 属性

            /// <summary>
            /// 获取网络频道名称。
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// 获取网络频道所使用的 Socket。
            /// </summary>
            public INetworkSocket Socket
            {
                get { return PSocket; }
            }

            /// <summary>
            /// 获取是否已连接。
            /// </summary>
            public bool Connected
            {
                get
                {
                    if (PSocket != null)
                    {
                        return PSocket.IsConnected;
                    }

                    return false;
                }
            }

            /// <summary>
            /// 获取网络地址类型。
            /// </summary>
            public AddressFamily AddressFamily => PAddressFamily;

            /// <summary>
            /// 获取要发送的消息包数量。
            /// </summary>
            public int SendPacketCount
            {
                get
                {
                    lock (PSendPacketPool)
                    {
                        return PSendPacketPool.Count;
                    }
                }
            }

            /// <summary>
            /// 获取累计发送的消息包数量。
            /// </summary>
            public int SentPacketCount => PSentPacketCount;

            /// <summary>
            /// 获取累计已接收的消息包数量。
            /// </summary>
            public int ReceivedPacketCount => PReceivedPacketCount;

            /// <summary>
            /// 获取或设置当收到消息包时是否重置心跳流逝时间。
            /// </summary>
            public bool ResetHeartBeatElapseSecondsWhenReceivePacket
            {
                get => PResetHeartBeatElapseSecondsWhenReceivePacket;
                set => PResetHeartBeatElapseSecondsWhenReceivePacket = value;
            }

            /// <summary>
            /// 获取丢失心跳的次数。
            /// </summary>
            public int MissHeartBeatCount
            {
                get
                {
                    lock (PHeartBeatState)
                    {
                        return PHeartBeatState.MissHeartBeatCount;
                    }
                }
            }

            /// <summary>
            /// 获取或设置心跳间隔时长，以秒为单位。
            /// </summary>
            public float HeartBeatInterval
            {
                get => PHeartBeatInterval;
                set => PHeartBeatInterval = value;
            }

            /// <summary>
            /// 获取心跳等待时长，以秒为单位。
            /// </summary>
            public float HeartBeatElapseSeconds
            {
                get
                {
                    lock (PHeartBeatState)
                    {
                        return PHeartBeatState.HeartBeatElapseSeconds;
                    }
                }
            }

            /// <summary>
            /// 消息发送包头处理器
            /// </summary>
            public IPacketSendHeaderHandler PacketSendHeaderHandler => m_PacketSendHeaderHandler;

            /// <summary>
            /// 消息发送内容处理器
            /// </summary>
            public IPacketSendBodyHandler PacketSendBodyHandler => m_PacketSendBodyHandler;

            /// <summary>
            /// 消息接收包头处理器
            /// </summary>
            public IPacketReceiveHeaderHandler PacketReceiveHeaderHandler => m_PacketReceiveHeaderHandler;

            /// <summary>
            /// 心跳消息处理器
            /// </summary>
            public IPacketHeartBeatHandler PacketHeartBeatHandler => m_PacketHeartBeatHandler;

            /// <summary>
            /// 消息接收内容处理器
            /// </summary>
            public IPacketReceiveBodyHandler PacketReceiveBodyHandler => m_PacketReceiveBodyHandler;

            #endregion

            /// <summary>
            /// 网络频道轮询。
            /// </summary>
            /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
            /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
            public virtual void Update(float elapseSeconds, float realElapseSeconds)
            {
                if (PSocket == null || !PActive)
                {
                    return;
                }

                ProcessSend();
                ProcessReceive();
                if (PSocket == null || !PActive)
                {
                    return;
                }

                ProcessHeartBeat(realElapseSeconds);
                PRpcState.Update(elapseSeconds, realElapseSeconds);
				
				while (m_ExecutionQueue.Count > 0)
                {
                    m_ExecutionQueue.Dequeue()?.Invoke();
                }
            }

            /// <summary>
            /// 处理心跳
            /// </summary>
            /// <param name="realElapseSeconds"></param>
            private void ProcessHeartBeat(float realElapseSeconds)
            {
                if (PHeartBeatInterval > 0f)
                {
                    bool sendHeartBeat = false;
                    int missHeartBeatCount = 0;
                    lock (PHeartBeatState)
                    {
                        if (PSocket == null || !PActive)
                        {
                            return;
                        }

                        PHeartBeatState.HeartBeatElapseSeconds += realElapseSeconds;
                        if (PHeartBeatState.HeartBeatElapseSeconds >= PHeartBeatInterval)
                        {
                            sendHeartBeat = true;
                            missHeartBeatCount = PHeartBeatState.MissHeartBeatCount;
                            PHeartBeatState.HeartBeatElapseSeconds = 0f;
                            PHeartBeatState.MissHeartBeatCount++;
                        }

                        if (sendHeartBeat && PNetworkChannelHelper.SendHeartBeat())
                        {
                            if (missHeartBeatCount > 0 && NetworkChannelMissHeartBeat != null)
                            {
                                NetworkChannelMissHeartBeat(this, missHeartBeatCount);
                            }

                            // PHeartBeatState.Reset(this.ResetHeartBeatElapseSecondsWhenReceivePacket);
                            return;
                        }

                        if (PHeartBeatState.MissHeartBeatCount > MissHeartBeatCountByClose)
                        {
                            // 心跳丢失达到上线。触发断开
                            Close();
                        }
                    }
                }
            }

            /// <summary>
            /// 关闭网络频道。
            /// </summary>
            public virtual void Shutdown()
            {
                Close();
                PSendState.Reset();
                PNetworkChannelHelper.Shutdown();
            }


            /// <summary>
            /// 注册网络消息包处理函数。
            /// </summary>
            /// <param name="handler">要注册的网络消息包处理函数。</param>
            public void RegisterHandler(IPacketSendHeaderHandler handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                m_PacketSendHeaderHandler = handler;
            }


            /// <summary>
            /// 注册网络消息包处理函数。
            /// </summary>
            /// <param name="handler">要注册的网络消息包处理函数。</param>
            public void RegisterHandler(IPacketSendBodyHandler handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                m_PacketSendBodyHandler = handler;
            }

            /// <summary>
            /// 注册网络消息包处理函数。
            /// </summary>
            /// <param name="handler">要注册的网络消息包处理函数。</param>
            public void RegisterHandler(IPacketReceiveHeaderHandler handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                m_PacketReceiveHeaderHandler = handler;
            }

            /// <summary>
            /// 注册网络消息包处理函数。
            /// </summary>
            /// <param name="handler">要注册的网络消息包处理函数。</param>
            public void RegisterHandler(IPacketReceiveBodyHandler handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                m_PacketReceiveBodyHandler = handler;
            }

            /// <summary>
            /// 注册网络消息心跳处理函数，用于处理心跳消息
            /// </summary>
            /// <param name="handler">要注册的网络消息包处理函数</param>
            [Obsolete("Use RegisterHeartBeatHandler instead")]
            public void RegisterHandler(IPacketHeartBeatHandler handler)
            {
                RegisterHeartBeatHandler(handler);
            }

            /// <summary>
            /// 注册网络消息心跳处理函数，用于处理心跳消息
            /// </summary>
            /// <param name="handler">要注册的网络消息包处理函数</param>
            public void RegisterHeartBeatHandler(IPacketHeartBeatHandler handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                m_PacketHeartBeatHandler = handler;
                if (handler.HeartBeatInterval > 0)
                {
                    PHeartBeatInterval = handler.HeartBeatInterval;
                }

                if (handler.MissHeartBeatCountByClose > 0)
                {
                    MissHeartBeatCountByClose = handler.MissHeartBeatCountByClose;
                }
            }

            /// <summary>
            /// 设置RPC错误的处理函数
            /// </summary>
            /// <param name="handler"></param>
            public void SetRPCErrorHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                PRpcState.SetRPCErrorHandler(handler);
            }

            /// <summary>
            /// 设置RPC开始的处理函数
            /// </summary>
            /// <param name="handler"></param>
            public void SetRPCStartHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                PRpcState.SetRPCStartHandler(handler);
            }

            /// <summary>
            /// 设置RPC结束的处理函数
            /// </summary>
            /// <param name="handler"></param>
            public void SetRPCEndHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                PRpcState.SetRPCEndHandler(handler);
            }

            /// <summary>
            /// 连接到远程主机。
            /// </summary>
            /// <param name="ipAddress">远程主机的 IP 地址。</param>
            /// <param name="port">远程主机的端口号。</param>
            /// <param name="userData">用户自定义数据。</param>
            /// <param name="isSsl">是否加密</param>
            public virtual void Connect(IPAddress ipAddress, int port, object userData = null, bool isSsl = false)
            {
                if (PSocket != null)
                {
                    Close();
                    PSocket = null;
                }

                switch (ipAddress.AddressFamily)
                {
                    case System.Net.Sockets.AddressFamily.InterNetwork:
                        PAddressFamily = AddressFamily.IPv4;
                        break;

                    case System.Net.Sockets.AddressFamily.InterNetworkV6:
                        PAddressFamily = AddressFamily.IPv6;
                        break;

                    default:
                        string errorMessage = Utility.Text.Format("Not supported address family '{0}'.", ipAddress.AddressFamily);
                        if (NetworkChannelError != null)
                        {
                            NetworkChannelError(this, NetworkErrorCode.AddressFamilyError, SocketError.Success, errorMessage);
                            return;
                        }

                        throw new GameFrameworkException(errorMessage);
                }

                PSendState.Reset();
                PReceiveState.PrepareForPacketHeader();
            }

            /// <summary>
            /// 关闭连接并释放所有相关资源。
            /// </summary>
            public virtual void Close()
            {
                lock (this)
                {
                    if (PSocket == null)
                    {
                        return;
                    }

                    PActive = false;

                    try
                    {
                        PSocket.Shutdown();
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        PSocket.Close();
                        PSocket = null;

                        NetworkChannelClosed?.Invoke(this);
                    }

                    PSentPacketCount = 0;
                    PReceivedPacketCount = 0;

                    lock (PSendPacketPool)
                    {
                        PSendPacketPool.Clear();
                    }

                    lock (PHeartBeatState)
                    {
                        PHeartBeatState.Reset(true);
                    }
                }
            }

            /// <summary>
            /// 向远程主机发送消息包
            /// </summary>
            /// <param name="messageObject"></param>
            /// <typeparam name="TResult"></typeparam>
            public async Task<TResult> Call<TResult>(MessageObject messageObject) where TResult : MessageObject, IResponseMessage
            {
                GameFrameworkGuard.NotNull(messageObject, nameof(messageObject));
                Send(messageObject);
                var result = await PRpcState.Call(messageObject);
                return result as TResult;
            }

            /// <summary>
            /// 向远程主机发送消息包。
            /// </summary>
            /// <typeparam name="T">消息包类型。</typeparam>
            /// <param name="messageObject">要发送的消息包。</param>
            public void Send<T>(T messageObject) where T : MessageObject
            {
                GameFrameworkGuard.NotNull(messageObject, nameof(messageObject));
                if (PSocket == null)
                {
                    const string errorMessage = "You must connect first.";
                    if (NetworkChannelError != null)
                    {
                        NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, errorMessage);
                        return;
                    }

                    throw new GameFrameworkException(errorMessage);
                }

                if (!PActive)
                {
                    const string errorMessage = "Socket is not active.";
                    if (NetworkChannelError != null)
                    {
                        NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, errorMessage);
                        return;
                    }

                    throw new GameFrameworkException(errorMessage);
                }

                if (messageObject == null)
                {
                    const string errorMessage = "Packet is invalid.";
                    if (NetworkChannelError != null)
                    {
                        NetworkChannelError(this, NetworkErrorCode.SendError, SocketError.Success, errorMessage);
                        return;
                    }

                    throw new GameFrameworkException(errorMessage);
                }

                lock (PSendPacketPool)
                {
                    PSendPacketPool.Enqueue(messageObject);
                }
            }

            /// <summary>
            /// 释放资源。
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// 释放资源。
            /// </summary>
            /// <param name="disposing">释放资源标记。</param>
            private void Dispose(bool disposing)
            {
                if (m_Disposed)
                {
                    return;
                }

                if (disposing)
                {
                    Close();
                    PSendState.Dispose();
                    PReceiveState.Dispose();
                }

                m_Disposed = true;
            }

            /// <summary>
            /// 处理发送消息对象
            /// </summary>
            /// <param name="messageObject">消息对象</param>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            protected virtual bool ProcessSendMessage(MessageObject messageObject)
            {
                bool serializeResult = PNetworkChannelHelper.SerializePacketHeader(messageObject, PSendState.Stream, out var messageBodyBuffer);
                if (serializeResult)
                {
                    serializeResult = PNetworkChannelHelper.SerializePacketBody(messageBodyBuffer, PSendState.Stream);
                }
                else
                {
                    const string errorMessage = "Serialized packet failure.";
                    throw new InvalidOperationException(errorMessage);
                }

                return serializeResult;
            }

            /// <summary>
            /// 处理消息发送
            /// </summary>
            /// <returns></returns>
            /// <exception cref="GameFrameworkException"></exception>
            protected virtual bool ProcessSend()
            {
                lock (PSendPacketPool)
                {
                    if (PSendState.Stream.Length > 0 || PSendPacketPool.Count <= 0)
                    {
                        return false;
                    }


                    while (PSendPacketPool.Count > 0)
                    {
                        var messageObject = PSendPacketPool.Dequeue();

                        bool serializeResult = false;
                        try
                        {
                            serializeResult = ProcessSendMessage(messageObject);
                            DebugSendLog(messageObject);
                        }
                        catch (Exception exception)
                        {
                            PActive = false;
                            if (NetworkChannelError != null)
                            {
                                SocketException socketException = exception as SocketException;
                                NetworkChannelError(this, NetworkErrorCode.SerializeError, socketException?.SocketErrorCode ?? SocketError.Success, exception.ToString());
                                return false;
                            }

                            throw;
                        }

                        if (!serializeResult)
                        {
                            const string errorMessage = "Serialized packet failure.";
                            if (NetworkChannelError != null)
                            {
                                NetworkChannelError(this, NetworkErrorCode.SerializeError, SocketError.Success, errorMessage);
                                return false;
                            }

                            throw new GameFrameworkException(errorMessage);
                        }

                        // PSendState.Reset();
                    }

                    PSendState.Stream.Position = 0L;
                    return true;
                }
            }

            protected void ProcessReceive()
            {
            }

            protected void DebugSendLog(MessageObject messageObject)
            {
#if ENABLE_GAMEFRAMEX_NETWORK_SEND_LOG
                if (!IgnoreSendIds.Contains(PacketSendHeaderHandler.Id))
                {
                    Log.Debug($"发送消息 ID:[{PacketSendHeaderHandler.Id},{messageObject.UniqueId},{messageObject.GetType().Name}] 消息内容:{Utility.Json.ToJson(messageObject)}");
                }
#endif
            }

            protected void DebugReceiveLog(MessageObject messageObject)
            {
#if ENABLE_GAMEFRAMEX_NETWORK_RECEIVE_LOG
                if (!IgnoreReceiveIds.Contains(PacketReceiveHeaderHandler.Id))
                {
                    Log.Debug($"收到消息 ID:[{PacketReceiveHeaderHandler.Id},{messageObject.UniqueId},{messageObject.GetType().Name}] 消息内容:{Utility.Json.ToJson(messageObject)}");
                }
#endif
            }

            protected void InvokeMessageHandler(MessageObject messageObject)
            {
                var handlers = ProtoMessageHandler.GetHandlers(messageObject.GetType());
                foreach (var handler in handlers)
                {
                    try
                    {
                        lock (m_ExecutionQueue)
                        {
							handler.SetMessageObject(messageObject);
                            m_ExecutionQueue.Enqueue(handler);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            protected List<int> IgnoreSendIds = new List<int>();
            protected List<int> IgnoreReceiveIds = new List<int>();

            /// <summary>
            /// 设置忽略的消息打印列表
            /// </summary>
            /// <param name="sendIds">发送列表</param>
            /// <param name="receiveIds">接收列表</param>
            public void SetIgnoreLogNetworkIds(List<int> sendIds, List<int> receiveIds)
            {
                IgnoreSendIds = sendIds;
                IgnoreReceiveIds = receiveIds;
            }
        }
    }
}
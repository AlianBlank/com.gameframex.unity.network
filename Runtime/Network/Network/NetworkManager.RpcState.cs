// ==========================================================================================
//  GameFrameX 组织及其衍生项目的版权、商标、专利及其他相关权利
//  GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//  均受中华人民共和国及相关国际法律法规保护。
//  are protected by the laws of the People's Republic of China and relevant international regulations.
// 
//  使用本项目须严格遵守相应法律法规及开源许可证之规定。
//  Usage of this project must strictly comply with applicable laws, regulations, and open-source licenses.
// 
//  本项目采用 MIT 许可证与 Apache License 2.0 双许可证分发，
//  This project is dual-licensed under the MIT License and Apache License 2.0,
//  完整许可证文本请参见源代码根目录下的 LICENSE 文件。
//  please refer to the LICENSE file in the root directory of the source code for the full license text.
// 
//  禁止利用本项目实施任何危害国家安全、破坏社会秩序、
//  It is prohibited to use this project to engage in any activities that endanger national security, disrupt social order,
//  侵犯他人合法权益等法律法规所禁止的行为！
//  or infringe upon the legitimate rights and interests of others, as prohibited by laws and regulations!
//  因基于本项目二次开发所产生的一切法律纠纷与责任，
//  Any legal disputes and liabilities arising from secondary development based on this project
//  本项目组织与贡献者概不承担。
//  shall be borne solely by the developer; the project organization and contributors assume no responsibility.
// 
//  GitHub 仓库：https://github.com/GameFrameX
//  GitHub Repository: https://github.com/GameFrameX
//  Gitee  仓库：https://gitee.com/GameFrameX
//  Gitee Repository:  https://gitee.com/GameFrameX
//  官方文档：https://gameframex.doc.alianblank.com/
//  Official Documentation: https://gameframex.doc.alianblank.com/
// ==========================================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameFrameX.Runtime;

namespace GameFrameX.Network.Runtime
{
    public sealed partial class NetworkManager
    {
        public partial class RpcState : IDisposable
        {
            /// <summary>
            /// 等待回复处理对象字典
            /// </summary>
            private readonly ConcurrentDictionary<long, RpcMessageData> _waitingReplyHandlingObjects = new ConcurrentDictionary<long, RpcMessageData>();

            private readonly List<long> _timeoutIds = new List<long>(16);

            private EventHandler<MessageObject> _rpcStartHandler;
            private EventHandler<MessageObject> _rpcEndHandler;
            private EventHandler<MessageObject> _rpcErrorHandler;
            private EventHandler<MessageObject> _rpcErrorCodeHandler;
            private readonly int _rpcTimeout;
            private bool _disposed;

            public RpcState(int timeout)
            {
                _rpcTimeout = timeout;
                if (_rpcTimeout < 3000)
                {
                    throw new ArgumentOutOfRangeException(nameof(timeout), "RPC超时时间不能小于3000毫秒");
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                foreach (var kvp in _waitingReplyHandlingObjects)
                {
                    kvp.Value.Dispose();
                }

                _waitingReplyHandlingObjects.Clear();
                _disposed = true;
            }

            /// <summary>
            /// 重置 RPC 状态，用于重连场景。
            /// </summary>
            public void Reset()
            {
                foreach (var kvp in _waitingReplyHandlingObjects)
                {
                    kvp.Value.Dispose();
                }

                _waitingReplyHandlingObjects.Clear();
                _disposed = false;
            }


            /// <summary>
            /// 处理RPC回复消息。
            /// 此方法用于处理接收到的RPC回复消息，并触发相应的结束处理程序。
            /// </summary>
            /// <param name="message">要处理的消息对象，必须实现IResponseMessage接口。</param>
            /// <returns>如果成功处理回复消息，则返回true；否则返回false。</returns>
            public bool TryReply(MessageObject message)
            {
                if (message.GetType().IsImplWithInterface(typeof(IResponseMessage)))
                {
                    if (_waitingReplyHandlingObjects.TryRemove(message.UniqueId, out var messageActorObject))
                    {
                        try
                        {
                            var responseMessage = message as IResponseMessage;
                            messageActorObject.Reply(responseMessage);
                            _rpcEndHandler?.Invoke(this, message);
                            if (responseMessage?.ErrorCode != 0 && !messageActorObject.IsIgnoreErrorCode)
                            {
                                _rpcErrorCodeHandler?.Invoke(this, message);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Fatal(e);
                        }

                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// 调用RPC并等待返回结果。
            /// 此方法会发送一个请求消息并返回一个任务，任务将在收到响应时完成。
            /// 可能会引发超时异常。
            /// </summary>
            /// <param name="messageObject">要发送的消息对象，必须实现IRequestMessage接口。</param>
            /// <param name="isIgnoreErrorCode"></param>
            /// <returns>返回一个任务，该任务在收到响应时完成，并返回IResponseMessage。</returns>
            public Task<IResponseMessage> Call(MessageObject messageObject, bool isIgnoreErrorCode)
            {
                if (_waitingReplyHandlingObjects.TryGetValue(messageObject.UniqueId, out var messageActorObject))
                {
                    return messageActorObject.Task;
                }

                var defaultMessageActorObject = RpcMessageData.Create(messageObject as IRequestMessage, _rpcTimeout, isIgnoreErrorCode);
                if (!_waitingReplyHandlingObjects.TryAdd(messageObject.UniqueId, defaultMessageActorObject))
                {
                    return defaultMessageActorObject.Task;
                }
                try
                {
                    _rpcStartHandler?.Invoke(this, messageObject);
                }
                catch (Exception e)
                {
                    Log.Fatal(e);
                }

                return defaultMessageActorObject.Task;
            }

            /// <summary>
            /// 逻辑更新，处理计时和超时移除任务
            /// </summary>
            /// <param name="elapseSeconds"></param>
            /// <param name="realElapseSeconds"></param>
            public void Update(float elapseSeconds, float realElapseSeconds)
            {
                if (_waitingReplyHandlingObjects.Count <= 0)
                {
                    return;
                }

                var elapseSecondsTime = (long)(realElapseSeconds * 1000);
                _timeoutIds.Clear();
                foreach (var handlingObject in _waitingReplyHandlingObjects)
                {
                    bool isTimeout = handlingObject.Value.IncrementalElapseTime(elapseSecondsTime);
                    if (!isTimeout)
                    {
                        continue;
                    }

                    _timeoutIds.Add(handlingObject.Key);
                    try
                    {
                        _rpcErrorHandler?.Invoke(this, handlingObject.Value.RequestMessage as MessageObject);
                    }
                    catch (Exception e)
                    {
                        Log.Fatal(e);
                    }
                }

                if (_timeoutIds.Count <= 0)
                {
                    return;
                }

                foreach (var objectId in _timeoutIds)
                {
                    _waitingReplyHandlingObjects.TryRemove(objectId, out _);
                }
            }

            /// <summary>
            /// 让所有等待中的 RPC 立即失败。
            /// </summary>
            /// <param name="exception">失败原因。</param>
            /// <returns>被失败化的 RPC 数量。</returns>
            public int FailAll(Exception exception)
            {
                var failedCount = 0;
                foreach (var kvp in _waitingReplyHandlingObjects)
                {
                    try
                    {
                        kvp.Value.Fail(exception);
                    }
                    catch (Exception e)
                    {
                        Log.Fatal(e);
                    }

                    failedCount++;
                }

                _waitingReplyHandlingObjects.Clear();
                return failedCount;
            }

            /// <summary>
            /// 设置RPC错误Code的处理函数
            /// </summary>
            /// <param name="handler">处理函数</param>
            [UnityEngine.Scripting.Preserve]
            public void SetRPCErrorCodeHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                _rpcErrorCodeHandler = handler;
            }

            /// <summary>
            /// 设置RPC错误的处理函数
            /// </summary>
            /// <param name="handler">处理函数</param>
            [UnityEngine.Scripting.Preserve]
            public void SetRPCErrorHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                _rpcErrorHandler = handler;
            }

            /// <summary>
            /// 设置RPC开始的处理函数
            /// </summary>
            /// <param name="handler">处理函数</param>
            [UnityEngine.Scripting.Preserve]
            public void SetRPCStartHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                _rpcStartHandler = handler;
            }

            /// <summary>
            /// 设置RPC结束的处理函数
            /// </summary>
            /// <param name="handler">处理函数</param>
            [UnityEngine.Scripting.Preserve]
            public void SetRPCEndHandler(EventHandler<MessageObject> handler)
            {
                GameFrameworkGuard.NotNull(handler, nameof(handler));
                _rpcEndHandler = handler;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Text;
using GameFrameX.Network.Runtime;
using GameFrameX.Runtime;
using kcp2k;
using NUnit.Framework;
using UnityEditor;

namespace GameFrameX.Network.Tests
{
    internal sealed class UnitTests
    {
        [TearDown]
        public void TearDown()
        {
            Utility.Json.SetJsonHelper(new LitJsonHelper());
        }

        [Test]
        public void NetworkManagerCreatesAndDestroysChannels()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("main", new TestNetworkChannelHelper(), 3000);

            Assert.That(channel.Name, Is.EqualTo("main"));
            Assert.That(manager.NetworkChannelCount, Is.EqualTo(1));
            Assert.That(manager.HasNetworkChannel("main"), Is.True);
            Assert.That(manager.GetNetworkChannel("main"), Is.SameAs(channel));
            Assert.That(manager.GetAllNetworkChannels(), Has.Length.EqualTo(1));

            var channels = new List<INetworkChannel>();
            manager.GetAllNetworkChannels(channels);
            Assert.That(channels, Is.EquivalentTo(new[] { channel }));

            Assert.That(manager.DestroyNetworkChannel("main"), Is.True);
            Assert.That(manager.NetworkChannelCount, Is.EqualTo(0));
            Assert.That(manager.DestroyNetworkChannel("main"), Is.False);
        }

        [Test]
        public void NetworkManagerRejectsDuplicateChannelNames()
        {
            var manager = new NetworkManager();
            manager.CreateNetworkChannel("duplicate", new TestNetworkChannelHelper(), 3000);

            Assert.Throws<GameFrameworkException>(() =>
                manager.CreateNetworkChannel("duplicate", new TestNetworkChannelHelper(), 3000));

            manager.DestroyNetworkChannel("duplicate");
        }

        [Test]
        public void ServiceTypeKcpCreatesKcpChannel()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel(
                "kcp",
                new TestNetworkChannelHelper(),
                3000,
                ServiceType.Kcp,
                new KcpConfig());

            Assert.That(channel.GetType().Name, Is.EqualTo("KcpNetworkChannel"));

            manager.DestroyNetworkChannel("kcp");
        }

        [TestCase(ServiceType.KcpUdp)]
        [TestCase(ServiceType.KcpTcp)]
        [TestCase(ServiceType.KcpWebSocket)]
        public void KcpServiceTypesCreateKcpChannel(ServiceType serviceType)
        {
            var manager = new NetworkManager();
            var channelName = "kcp-" + serviceType;
            var channel = manager.CreateNetworkChannel(
                channelName,
                new TestNetworkChannelHelper(),
                3000,
                serviceType,
                new KcpConfig());

            Assert.That(channel.GetType().Name, Is.EqualTo("KcpNetworkChannel"));

            manager.DestroyNetworkChannel(channelName);
        }

        [Test]
        public void LegacyFactoryCreatesSystemTcpChannel()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("legacy", new TestNetworkChannelHelper(), 3000);

#if FORCE_ENABLE_GAME_FRAME_X_WEB_SOCKET
            Assert.That(channel.GetType().Name, Is.EqualTo("WebSocketNetworkChannel"));
#else
            var expectedChannelType = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL
                ? "WebSocketNetworkChannel"
                : "SystemTcpNetworkChannel";

            Assert.That(channel.GetType().Name, Is.EqualTo(expectedChannelType));
#endif

            manager.DestroyNetworkChannel("legacy");
        }

        [Test]
        public void KcpUriFactoryCreatesKcpChannel()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel(
                "kcp-uri",
                new TestNetworkChannelHelper(),
                3000,
                new KcpConfig());

            Assert.That(channel.GetType().Name, Is.EqualTo("KcpNetworkChannel"));

            manager.DestroyNetworkChannel("kcp-uri");
        }

        [Test]
        public void KcpUriChannelRejectsUnsupportedScheme()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel(
                "bad-uri",
                new TestNetworkChannelHelper(),
                3000,
                new KcpConfig());

            Assert.Throws<GameFrameworkException>(() =>
                channel.Connect(new Uri("http://127.0.0.1:9")));
            Assert.That(channel.Connected, Is.False);

            manager.DestroyNetworkChannel("bad-uri");
        }

        [Test]
        public void KcpChannelRejectsInvalidConfigBeforeOpeningTransport()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel(
                "bad-kcp",
                new TestNetworkChannelHelper(),
                3000,
                ServiceType.Kcp,
                new KcpConfig { Mtu = 10 });

            Assert.Throws<GameFrameworkException>(() =>
                channel.Connect(new Uri("kcp://127.0.0.1:9")));

            Assert.That(channel.Connected, Is.False);
            manager.DestroyNetworkChannel("bad-kcp");
        }

        [Test]
        public void KcpChannelRejectsInvalidWindowAndMessageConfigBeforeOpeningTransport()
        {
            AssertKcpConfigRejected("bad-send-window", new KcpConfig { SendWindowSize = 0 });
            AssertKcpConfigRejected("bad-receive-window", new KcpConfig { ReceiveWindowSize = 0 });
            AssertKcpConfigRejected("bad-message-size", new KcpConfig { MaxMessageSize = 0 });
        }

        [Test]
        public void KcpUdpChannelFiresConnectedEventAfterTransportConnects()
        {
            var manager = new NetworkManager();
            var connected = false;
            manager.NetworkConnected += (sender, args) => connected = args.NetworkChannel.Name == "udp-kcp";

            var channel = manager.CreateNetworkChannel(
                "udp-kcp",
                new TestNetworkChannelHelper(),
                3000,
                ServiceType.KcpUdp,
                new KcpConfig());

            channel.Connect(new Uri("kcp://127.0.0.1:9"));

            Assert.That(connected, Is.True);
            Assert.That(channel.Connected, Is.True);

            manager.DestroyNetworkChannel("udp-kcp");
        }

        [Test]
        public void KcpUdpTransportConnectInvokesConnectedCallback()
        {
            var connected = false;
            using (var transport = new KcpUdpTransport())
            {
                transport.SetOnConnected(() => connected = true);
                transport.Connect(new Uri("kcp://127.0.0.1:9"));

                Assert.That(connected, Is.True);
                Assert.That(transport.IsConnected, Is.True);
            }
        }

        [Test]
        public void KcpUdpTransportCloseInvokesClosedCallbackOnce()
        {
            var closedCount = 0;
            using (var transport = new KcpUdpTransport())
            {
                transport.SetOnClosed(() => closedCount++);
                transport.Connect(new Uri("kcp://127.0.0.1:9"));

                transport.Close();
                transport.Close();
            }

            Assert.That(closedCount, Is.EqualTo(1));
        }

        [Test]
        public void KcpChannelHeartbeatUsesNetworkChannelBaseHeartbeat()
        {
            var helper = new TestNetworkChannelHelper
            {
                HeartBeatSendResult = true
            };
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel(
                "heartbeat-kcp",
                helper,
                3000,
                ServiceType.KcpUdp,
                new KcpConfig());

            channel.Connect(new Uri("kcp://127.0.0.1:9"));
            channel.HeartBeatInterval = 0.01f;

            InvokeChannelUpdate(channel, 0.02f, 0.02f);
            InvokeChannelUpdate(channel, 0.02f, 0.02f);

            Assert.That(helper.HeartBeatSendCount, Is.EqualTo(2));

            manager.DestroyNetworkChannel("heartbeat-kcp");
        }

        [Test]
        public void KcpConfigDefaultsAreStable()
        {
            var config = new KcpConfig();

            Assert.That(config.NoDelay, Is.True);
            Assert.That(config.Interval, Is.EqualTo(10u));
            Assert.That(config.Resend, Is.EqualTo(2));
            Assert.That(config.NoCongestionControl, Is.True);
            Assert.That(config.Mtu, Is.EqualTo(1200u));
            Assert.That(config.SendWindowSize, Is.EqualTo(256u));
            Assert.That(config.ReceiveWindowSize, Is.EqualTo(256u));
            Assert.That(config.MaxMessageSize, Is.EqualTo(256 * 1024));
            Assert.That(config.ConversationId, Is.EqualTo(0u));
        }

        [Test]
        public void PooledBufferCopyFromKeepsIndependentCopy()
        {
            var source = new byte[] { 1, 2, 3, 4, 5 };
            using (var buffer = PooledBuffer.CopyFrom(source, 1, 3))
            {
                source[2] = 9;

                Assert.That(buffer.Length, Is.EqualTo(3));
                Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(3));
                Assert.That(buffer.Buffer[0], Is.EqualTo(2));
                Assert.That(buffer.Buffer[1], Is.EqualTo(3));
                Assert.That(buffer.Buffer[2], Is.EqualTo(4));
            }
        }

        [Test]
        public void PooledBufferDisposeIsIdempotentAndRejectsUseAfterDispose()
        {
            var buffer = PooledBuffer.Rent(8);

            Assert.That(buffer.Length, Is.EqualTo(0));
            Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(8));

            buffer.Length = 4;
            buffer.Dispose();
            buffer.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                var ignored = buffer.Buffer;
            });
            Assert.Throws<ObjectDisposedException>(() =>
            {
                var ignored = buffer.Length;
            });
            Assert.Throws<ObjectDisposedException>(() => buffer.Length = 1);
        }

        [Test]
        public void KcpTcpTransportRejectsInvalidMaxFrameSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KcpTcpTransport(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KcpTcpTransport(-1));
        }

        [Test]
        public void KcpTcpTransportReceivesLengthPrefixedFrames()
        {
            var received = new byte[0];
            using (var ready = new ManualResetEventSlim(false))
            using (var done = new ManualResetEventSlim(false))
            using (var listener = new TcpListenerWrapper())
            using (var transport = new KcpTcpTransport(64))
            {
                listener.Start(client =>
                {
                    ready.Set();
                    WriteFrame(client.GetStream(), new byte[] { 1, 2, 3, 4 });
                });

                transport.SetOnDataReceived((data, length) =>
                {
                    received = new byte[length];
                    Buffer.BlockCopy(data, 0, received, 0, length);
                    done.Set();
                });

                transport.Connect(new Uri("kcp-tcp://127.0.0.1:" + listener.Port));

                Assert.That(ready.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(done.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(received, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            }
        }

        [Test]
        public void KcpTcpTransportSendRawWritesLengthPrefixedFrame()
        {
            byte[] received = null;
            using (var done = new ManualResetEventSlim(false))
            using (var listener = new TcpListenerWrapper())
            using (var transport = new KcpTcpTransport(64))
            {
                listener.Start(client =>
                {
                    received = ReadExactly(client.GetStream(), 7);
                    done.Set();
                });

                transport.Connect(new Uri("kcp-tcp://127.0.0.1:" + listener.Port));
                transport.SendRaw(new byte[] { 9, 8, 7 }, 0, 3);

                Assert.That(done.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(received, Is.EqualTo(new byte[] { 0, 0, 0, 3, 9, 8, 7 }));
            }
        }

        [Test]
        public void KcpTcpTransportReceivesSplitLengthAndBody()
        {
            var received = new byte[0];
            using (var done = new ManualResetEventSlim(false))
            using (var listener = new TcpListenerWrapper())
            using (var transport = new KcpTcpTransport(64))
            {
                listener.Start(client =>
                {
                    WriteFrameInChunks(client.GetStream(), new byte[] { 5, 6, 7 }, 1);
                });

                transport.SetOnDataReceived((data, length) =>
                {
                    received = new byte[length];
                    Buffer.BlockCopy(data, 0, received, 0, length);
                    done.Set();
                });

                transport.Connect(new Uri("kcp-tcp://127.0.0.1:" + listener.Port));

                Assert.That(done.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(received, Is.EqualTo(new byte[] { 5, 6, 7 }));
            }
        }

        [Test]
        public void KcpTcpTransportReceivesConsecutiveFrames()
        {
            var received = new List<byte[]>();
            using (var done = new ManualResetEventSlim(false))
            using (var listener = new TcpListenerWrapper())
            using (var transport = new KcpTcpTransport(64))
            {
                listener.Start(client =>
                {
                    var stream = client.GetStream();
                    WriteFrame(stream, new byte[] { 1, 2 });
                    WriteFrame(stream, new byte[] { 3, 4, 5 });
                });

                transport.SetOnDataReceived((data, length) =>
                {
                    var copy = new byte[length];
                    Buffer.BlockCopy(data, 0, copy, 0, length);
                    lock (received)
                    {
                        received.Add(copy);
                        if (received.Count == 2)
                        {
                            done.Set();
                        }
                    }
                });

                transport.Connect(new Uri("kcp-tcp://127.0.0.1:" + listener.Port));

                Assert.That(done.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(received[0], Is.EqualTo(new byte[] { 1, 2 }));
                Assert.That(received[1], Is.EqualTo(new byte[] { 3, 4, 5 }));
            }
        }

        [Test]
        public void KcpTcpTransportClosesOnOversizedFrame()
        {
            using (var closed = new ManualResetEventSlim(false))
            using (var listener = new TcpListenerWrapper())
            using (var transport = new KcpTcpTransport(4))
            {
                listener.Start(client =>
                {
                    WriteFrame(client.GetStream(), new byte[] { 1, 2, 3, 4, 5 });
                });

                transport.SetOnClosed(() => closed.Set());
                transport.Connect(new Uri("kcp-tcp://127.0.0.1:" + listener.Port));

                Assert.That(closed.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(transport.IsConnected, Is.False);
            }
        }

        [Test]
        public void KcpCoreRejectsMessagesThatRequireTooManyFragments()
        {
            var kcp = new Kcp(1, (bytes, count) => { });
            kcp.SetMtu(100);

            Assert.Throws<Exception>(() => kcp.Send(new byte[30000], 0, 30000));
        }

        [Test]
        public void DefaultPacketSendBodyHandlerWritesBodyToDestination()
        {
            var handler = new DefaultPacketSendBodyHandler();
            var body = new byte[] { 7, 8, 9 };
            using (var stream = new MemoryStream())
            {
                Assert.That(handler.Handler(body, stream), Is.True);
                Assert.That(stream.ToArray(), Is.EqualTo(body));
            }
        }

        [Test]
        public void DefaultPacketSendBodyHandlerRejectsNullInputs()
        {
            var handler = new DefaultPacketSendBodyHandler();

            Assert.That(handler.Handler(null, new MemoryStream()), Is.False);
            Assert.That(handler.Handler(new byte[] { 1 }, null), Is.False);
        }

        [Test]
        public void DefaultPacketReceiveHeaderHandlerRejectsShortBuffers()
        {
            var handler = new DefaultPacketReceiveHeaderHandler();

            Assert.That(handler.PacketHeaderLength, Is.EqualTo(PacketHeaderLayout.BaseHeaderLength));
            Assert.That(handler.Handler(new byte[handler.PacketHeaderLength - 1]), Is.False);
            Assert.That(handler.Handler(null), Is.False);
        }

        [Test]
        public void DefaultPacketSendHeaderHandlerWritesReliableFifoBaseHeader()
        {
            var handler = new DefaultPacketSendHeaderHandler();
            handler.ChannelSerializer = new FixedSerializer(new byte[] { 7, 8, 9 });
            var message = new SnapshotMessageObject();
            message.SetUpdateUniqueId(1234);

            using (var stream = new MemoryStream())
            {
                Assert.That(handler.Handler(message, null, stream, out var body), Is.True);

                Assert.That(body, Is.EqualTo(new byte[] { 7, 8, 9 }));
                Assert.That(handler.PacketHeaderLength, Is.EqualTo(PacketHeaderLayout.BaseHeaderLength));
                Assert.That(handler.PacketLength, Is.EqualTo(PacketHeaderLayout.BaseHeaderLength + body.Length));

                var packet = stream.ToArray();
                Assert.That(packet, Has.Length.EqualTo(PacketHeaderLayout.BaseHeaderLength));

                var offset = 0;
                Assert.That(packet.ReadUInt(ref offset), Is.EqualTo((uint)(PacketHeaderLayout.BaseHeaderLength + body.Length)));
                Assert.That(packet.ReadByte(ref offset), Is.EqualTo(4));
                Assert.That(packet.ReadByte(ref offset), Is.EqualTo(0));
                var headerFlags = packet.ReadUShort(ref offset);
                Assert.That(PacketHeaderFlags.GetProtocolVersion(headerFlags), Is.EqualTo(PacketHeaderLayout.ProtocolVersion));
                Assert.That((headerFlags & PacketHeaderFlags.Reliable) != 0, Is.False);
                Assert.That(packet.ReadInt(ref offset), Is.EqualTo(1234));
                Assert.That(packet.ReadInt(ref offset), Is.EqualTo(handler.Id));
                Assert.That(offset, Is.EqualTo(PacketHeaderLayout.BaseHeaderLength));
            }
        }

        [Test]
        public void DefaultPacketReceiveHeaderHandlerRejectsLegacyFourteenByteHeader()
        {
            var handler = new DefaultPacketReceiveHeaderHandler();

            Assert.That(handler.Handler(new byte[14]), Is.False);
        }

        [Test]
        public void DefaultPacketReceiveHeaderHandlerReadsReliableFifoBaseHeader()
        {
            var packet = new byte[PacketHeaderLayout.BaseHeaderLength];
            var offset = 0;
            packet.WriteUInt(32, ref offset);
            packet.WriteByte(4, ref offset);
            packet.WriteByte(1, ref offset);
            packet.WriteUShort(PacketHeaderFlags.WithProtocolVersion(PacketHeaderFlags.Ack), ref offset);
            packet.WriteInt(222, ref offset);
            packet.WriteInt(333, ref offset);
            var handler = new DefaultPacketReceiveHeaderHandler();

            Assert.That(handler.Handler(packet), Is.True);

            Assert.That(handler.PacketHeaderLength, Is.EqualTo(PacketHeaderLayout.BaseHeaderLength));
            Assert.That(handler.PacketLength, Is.EqualTo(32));
            Assert.That(handler.OperationType, Is.EqualTo(4));
            Assert.That(handler.ZipFlag, Is.EqualTo(1));
            Assert.That(handler.HeaderFlags & PacketHeaderFlags.Ack, Is.Not.EqualTo(0));
            Assert.That(handler.ProtocolVersion, Is.EqualTo(PacketHeaderLayout.ProtocolVersion));
            Assert.That(handler.UniqueId, Is.EqualTo(222));
            Assert.That(handler.Id, Is.EqualTo(333));
            Assert.That(handler.HasReliableExtension, Is.False);
        }

        [Test]
        public void DefaultPacketReceiveHeaderHandlerReadsReliableExtension()
        {
            var packet = new byte[PacketHeaderLayout.ReliableHeaderLength];
            var offset = 0;
            packet.WriteUInt(PacketHeaderLayout.ReliableHeaderLength, ref offset);
            packet.WriteByte(4, ref offset);
            packet.WriteByte(0, ref offset);
            packet.WriteUShort(PacketHeaderFlags.WithProtocolVersion(PacketHeaderFlags.Reliable | PacketHeaderFlags.Ack), ref offset);
            packet.WriteInt(456, ref offset);
            packet.WriteInt(789, ref offset);
            WriteUInt64BigEndian(packet, 9876543210ul, ref offset);
            WriteUInt64BigEndian(packet, 11ul, ref offset);
            WriteUInt64BigEndian(packet, 10ul, ref offset);
            var handler = new DefaultPacketReceiveHeaderHandler();

            Assert.That(handler.Handler(packet), Is.True);

            Assert.That(handler.PacketHeaderLength, Is.EqualTo(PacketHeaderLayout.ReliableHeaderLength));
            Assert.That(handler.HasReliableExtension, Is.True);
            Assert.That(handler.SessionId, Is.EqualTo(9876543210ul));
            Assert.That(handler.ReliableSequence, Is.EqualTo(11ul));
            Assert.That(handler.AckSequence, Is.EqualTo(10ul));
        }

        [Test]
        public void NetworkReliableFifo_005_2_ReceiveHeaderExposesDuplicateFlag()
        {
            var packet = new byte[PacketHeaderLayout.ReliableHeaderLength];
            var offset = 0;
            packet.WriteUInt(PacketHeaderLayout.ReliableHeaderLength, ref offset);
            packet.WriteByte(4, ref offset);
            packet.WriteByte(0, ref offset);
            packet.WriteUShort(PacketHeaderFlags.WithProtocolVersion(PacketHeaderFlags.Reliable | PacketHeaderFlags.Ack | PacketHeaderFlags.Duplicate), ref offset);
            packet.WriteInt(456, ref offset);
            packet.WriteInt(789, ref offset);
            WriteUInt64BigEndian(packet, 9876543210ul, ref offset);
            WriteUInt64BigEndian(packet, 11ul, ref offset);
            WriteUInt64BigEndian(packet, 10ul, ref offset);
            var handler = new DefaultPacketReceiveHeaderHandler();

            Assert.That(handler.Handler(packet), Is.True);

            Assert.That(handler.IsDuplicate, Is.True);
            Assert.That(handler.HeaderFlags & PacketHeaderFlags.Duplicate, Is.Not.EqualTo(0));
            Assert.That(handler.SessionId, Is.EqualTo(9876543210ul));
            Assert.That(handler.ReliableSequence, Is.EqualTo(11ul));
            Assert.That(handler.AckSequence, Is.EqualTo(10ul));
        }

        [Test]
        public void ReliableFifoProtocolConstantsFreezeHeaderLayout()
        {
            Assert.That(PacketHeaderLayout.ProtocolVersion, Is.EqualTo(1));
            Assert.That(PacketHeaderLayout.BaseHeaderLength, Is.EqualTo(16));
            Assert.That(PacketHeaderLayout.ReliableExtensionLength, Is.EqualTo(24));
            Assert.That(PacketHeaderLayout.ReliableHeaderLength, Is.EqualTo(40));

            Assert.That(PacketHeaderLayout.PacketLengthOffset, Is.EqualTo(0));
            Assert.That(PacketHeaderLayout.OperationTypeOffset, Is.EqualTo(4));
            Assert.That(PacketHeaderLayout.ZipFlagOffset, Is.EqualTo(5));
            Assert.That(PacketHeaderLayout.HeaderFlagsOffset, Is.EqualTo(6));
            Assert.That(PacketHeaderLayout.UniqueIdOffset, Is.EqualTo(8));
            Assert.That(PacketHeaderLayout.MessageIdOffset, Is.EqualTo(12));
            Assert.That(PacketHeaderLayout.SessionIdOffset, Is.EqualTo(16));
            Assert.That(PacketHeaderLayout.ReliableSequenceOffset, Is.EqualTo(24));
            Assert.That(PacketHeaderLayout.AckSequenceOffset, Is.EqualTo(32));
        }

        [Test]
        public void ReliableFifoProtocolConstantsFreezeHeaderFlags()
        {
            Assert.That(PacketHeaderFlags.ProtocolVersionMask, Is.EqualTo(0xF000));
            Assert.That(PacketHeaderFlags.ProtocolVersionShift, Is.EqualTo(12));
            Assert.That(PacketHeaderFlags.Reliable, Is.EqualTo((ushort)(1 << 7)));
            Assert.That(PacketHeaderFlags.Ack, Is.EqualTo((ushort)(1 << 6)));
            Assert.That(PacketHeaderFlags.Control, Is.EqualTo((ushort)(1 << 5)));
            Assert.That(PacketHeaderFlags.Resume, Is.EqualTo((ushort)(1 << 4)));
            Assert.That(PacketHeaderFlags.LatestOnly, Is.EqualTo((ushort)(1 << 3)));
            Assert.That(PacketHeaderFlags.NoRetry, Is.EqualTo((ushort)(1 << 2)));
            Assert.That(PacketHeaderFlags.Duplicate, Is.EqualTo((ushort)(1 << 1)));

            var flags = PacketHeaderFlags.WithProtocolVersion(PacketHeaderFlags.Reliable | PacketHeaderFlags.Ack);
            Assert.That(PacketHeaderFlags.GetProtocolVersion(flags), Is.EqualTo(PacketHeaderLayout.ProtocolVersion));
            Assert.That((flags & PacketHeaderFlags.Reliable) != 0, Is.True);
            Assert.That((flags & PacketHeaderFlags.Ack) != 0, Is.True);
        }

        [Test]
        public void ReliableFifoProtocolConstantsFreezeDefaultOptions()
        {
            Assert.That(ReliableFifoProtocolOptions.HeartbeatWarnTimeoutSeconds, Is.EqualTo(5));
            Assert.That(ReliableFifoProtocolOptions.SilentResumeWindowSeconds, Is.EqualTo(10));
            Assert.That(ReliableFifoProtocolOptions.ServerSessionTtlSeconds, Is.EqualTo(30));
            Assert.That(ReliableFifoProtocolOptions.ReliableMessageTimeoutSeconds, Is.EqualTo(15));
            Assert.That(ReliableFifoProtocolOptions.ReliableRetryLimit, Is.EqualTo(5));
            Assert.That(ReliableFifoProtocolOptions.PendingQueueMaxCount, Is.EqualTo(1024));
            Assert.That(ReliableFifoProtocolOptions.PendingQueueMaxBytes, Is.EqualTo(4 * 1024 * 1024));
            Assert.That(ReliableFifoProtocolOptions.PendingMessageMaxBytes, Is.EqualTo(256 * 1024));
        }

        [Test]
        public void ReliableFifoProtocolConstantsFreezeServerContract()
        {
            Assert.That(ReliableFifoServerContract.SessionIdGeneratedByServer, Is.True);
            Assert.That(ReliableFifoServerContract.LoginRequestInitialSessionId, Is.EqualTo(0ul));
            Assert.That(ReliableFifoServerContract.InitialReliableSequence, Is.EqualTo(1ul));
            Assert.That(ReliableFifoServerContract.ResponseCacheMaxCount, Is.EqualTo(256));
            Assert.That(ReliableFifoServerContract.ResponseCacheTtlSeconds, Is.EqualTo(30));
            Assert.That(ReliableFifoServerContract.LoginResponseCacheTtlSeconds, Is.EqualTo(30));
            Assert.That(ReliableFifoServerContract.RequiresResumeTokenValidation, Is.True);
        }

        [Test]
        public void NetworkReliableFifo_007_2_ServerKickCloseReasonAndErrorCodesAreDefined()
        {
            Assert.That(NetworkCloseReason.ServerKick, Is.EqualTo("ServerKick"));
            Assert.That(NetworkCloseReason.DuplicateLogin, Is.EqualTo("DuplicateLogin"));
            Assert.That(NetworkCloseReason.SessionReplaced, Is.EqualTo("SessionReplaced"));
            Assert.That(NetworkCloseReason.AccountBanned, Is.EqualTo("AccountBanned"));
            Assert.That(NetworkCloseReason.AdminKick, Is.EqualTo("AdminKick"));
            Assert.That(NetworkCloseReason.SessionExpired, Is.EqualTo("SessionExpired"));

            Assert.That(NetworkErrorCode.ServerKickError, Is.GreaterThan(NetworkErrorCode.DisposeError));
            Assert.That(NetworkErrorCode.DuplicateLoginError, Is.GreaterThan(NetworkErrorCode.ServerKickError));
            Assert.That(NetworkErrorCode.SessionReplacedError, Is.GreaterThan(NetworkErrorCode.DuplicateLoginError));
            Assert.That(NetworkErrorCode.AccountBannedError, Is.GreaterThan(NetworkErrorCode.SessionReplacedError));
            Assert.That(NetworkErrorCode.AdminKickError, Is.GreaterThan(NetworkErrorCode.AccountBannedError));
            Assert.That(NetworkErrorCode.SessionExpiredError, Is.GreaterThan(NetworkErrorCode.AdminKickError));
        }

        [Test]
        public void NetworkReliableFifo_007_3_ServerKickReasonsMapToBusinessReconnect()
        {
            var reasons = new[]
            {
                NetworkLivenessReason.ServerKick,
                NetworkLivenessReason.DuplicateLogin,
                NetworkLivenessReason.SessionReplaced,
                NetworkLivenessReason.AccountBanned,
                NetworkLivenessReason.AdminKick,
                NetworkLivenessReason.SessionExpired
            };

            foreach (var reason in reasons)
            {
                Assert.That(NetworkManager.NetworkChannelBase.IsBusinessReconnectReason(reason), Is.True, reason);
            }

            Assert.That(NetworkManager.NetworkChannelBase.IsBusinessReconnectReason(NetworkLivenessReason.ResumeAccepted), Is.False);
            Assert.That(NetworkManager.NetworkChannelBase.IsBusinessReconnectReason(NetworkLivenessReason.HeartbeatTimeout), Is.False);
        }

        [Test]
        public void NetworkReliableFifo_007_4_ServerKickClearsPendingAndFailsRpcState()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("server-kick-fail", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            var rpcTask = channel.Call<TestResponseMessage>(new TestRequestMessage(), false);
            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(3));

            baseChannel.HandleServerKickControl(NetworkLivenessReason.ServerKick, 789ul, out var diagnostics);
            var clearedCount = baseChannel.FailReliableSessionForBusinessReconnect(NetworkLivenessReason.ServerKick);

            Assert.That(clearedCount, Is.EqualTo(3));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(rpcTask.IsFaulted, Is.True);
            Assert.That(rpcTask.Exception, Is.Not.Null);
            Assert.That(rpcTask.Exception.InnerException, Is.TypeOf<GameFrameworkException>());
            Assert.That(diagnostics.PendingCount, Is.EqualTo(3));
            ReferencePool.Release(diagnostics);

            manager.DestroyNetworkChannel("server-kick-fail");
        }

        [Test]
        public void NetworkReliableFifo_007_5_ServerKickDiagnosticsAreReferencePoolSafe()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("server-kick-pool", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            baseChannel.HandleServerKickControl(NetworkLivenessReason.DuplicateLogin, 321ul, out var diagnostics);
            Assert.That(diagnostics.ChannelName, Is.EqualTo("server-kick-pool"));
            Assert.That(diagnostics.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.ResumeRejected));
            Assert.That(diagnostics.State, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(diagnostics.Reason, Is.EqualTo(NetworkLivenessReason.DuplicateLogin));
            Assert.That(diagnostics.PendingCount, Is.EqualTo(0));
            ReferencePool.Release(diagnostics);

            var recycled = ReferencePool.Acquire<NetworkLivenessEventArgs>();
            Assert.That(recycled.ChannelName, Is.Null);
            Assert.That(recycled.Reason, Is.Null);
            Assert.That(recycled.PendingCount, Is.EqualTo(0));
            ReferencePool.Release(recycled);

            manager.DestroyNetworkChannel("server-kick-pool");
        }

        [Test]
        public void NetworkReliableFifo_004_1_ReliableSequenceGeneratorStartsAtServerContractInitialSequence()
        {
            var generator = new ReliableSequenceGenerator();

            Assert.That(generator.PeekNext(), Is.EqualTo(ReliableFifoServerContract.InitialReliableSequence));
            Assert.That(generator.Next(), Is.EqualTo(1ul));
            Assert.That(generator.Next(), Is.EqualTo(2ul));
            Assert.That(generator.PeekNext(), Is.EqualTo(3ul));

            generator.Reset(10ul);

            Assert.That(generator.PeekNext(), Is.EqualTo(10ul));
            Assert.That(generator.Next(), Is.EqualTo(10ul));
            Assert.That(generator.Next(), Is.EqualTo(11ul));
        }

        [Test]
        public void NetworkReliableFifo_004_2_PendingReliableMessageCapturesImmutablePayloadAndRetryState()
        {
            var message = new SnapshotMessageObject { Name = "pending", Value = 7 };
            var payload = new byte[] { 1, 2, 3 };

            var pending = PendingReliableMessage.Create(
                1234ul,
                42ul,
                41ul,
                message,
                payload,
                1.5f);

            payload[0] = 9;

            Assert.That(pending.SessionId, Is.EqualTo(1234ul));
            Assert.That(pending.ReliableSequence, Is.EqualTo(42ul));
            Assert.That(pending.AckSequence, Is.EqualTo(41ul));
            Assert.That(pending.MessageObject, Is.SameAs(message));
            Assert.That(pending.Payload, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(pending.PayloadByteCount, Is.EqualTo(3));
            Assert.That(pending.EnqueuedTimeSeconds, Is.EqualTo(1.5f));
            Assert.That(pending.RetryCount, Is.EqualTo(0));

            pending.MarkRetried(2.5f);

            Assert.That(pending.RetryCount, Is.EqualTo(1));
            Assert.That(pending.LastSendTimeSeconds, Is.EqualTo(2.5f));
        }

        [Test]
        public void NetworkReliableFifo_004_3_ReliableSendQueuePreservesFifoAndRemovesAcknowledgedMessages()
        {
            var queue = new ReliableSendQueue();
            var first = PendingReliableMessage.Create(10ul, 1ul, 0ul, new SnapshotMessageObject { Name = "first" }, new byte[] { 1, 2 }, 0.1f);
            var second = PendingReliableMessage.Create(10ul, 2ul, 0ul, new SnapshotMessageObject { Name = "second" }, new byte[] { 3, 4, 5 }, 0.2f);
            var third = PendingReliableMessage.Create(10ul, 4ul, 0ul, new SnapshotMessageObject { Name = "third" }, new byte[] { 6 }, 0.3f);

            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.That(queue.TotalPayloadBytes, Is.EqualTo(0));
            Assert.That(queue.TryPeek(out _), Is.False);

            queue.Enqueue(first);
            queue.Enqueue(second);
            queue.Enqueue(third);

            Assert.That(queue.Count, Is.EqualTo(3));
            Assert.That(queue.TotalPayloadBytes, Is.EqualTo(6));
            Assert.That(queue.TryPeek(out var peeked), Is.True);
            Assert.That(peeked, Is.SameAs(first));

            Assert.That(queue.AcknowledgeThrough(2ul), Is.EqualTo(2));
            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.TotalPayloadBytes, Is.EqualTo(1));
            Assert.That(queue.TryPeek(out peeked), Is.True);
            Assert.That(peeked, Is.SameAs(third));

            Assert.That(queue.AcknowledgeThrough(2ul), Is.EqualTo(0));
            Assert.That(queue.Count, Is.EqualTo(1));

            Assert.That(queue.AcknowledgeThrough(4ul), Is.EqualTo(1));
            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.That(queue.TotalPayloadBytes, Is.EqualTo(0));
            Assert.That(queue.TryPeek(out _), Is.False);
        }

        [Test]
        public void NetworkReliableFifo_004_4_SendEnqueuesReliablePendingMessage()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("reliable-send", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new SnapshotMessageObject { Name = "send", Value = 9 });

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(1));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(2ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(1));

            manager.DestroyNetworkChannel("reliable-send");
        }

        [Test]
        public void NetworkReliableFifo_004_5_ControlMessagesBypassReliableBusinessFifo()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("control-send", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new TestHeartBeatMessage());

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(1));

            manager.DestroyNetworkChannel("control-send");
        }

        [Test]
        public void NetworkReliableFifo_004_6_SendRejectsOversizedPendingMessageBeforeEnqueue()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("oversized-send", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            var largePayload = new byte[ReliableFifoProtocolOptions.PendingMessageMaxBytes + 1];
            var oversizeMessage = new LargePayloadMessageObject
            {
                Payload = largePayload
            };

            var errorRaised = false;
            baseChannel.NetworkChannelError += (_, errorCode, _, message) =>
            {
                if (errorCode == NetworkErrorCode.SendError && message.Contains("PendingMessageMaxBytes"))
                {
                    errorRaised = true;
                }
            };

            Assert.DoesNotThrow(() => channel.Send(oversizeMessage));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(errorRaised, Is.True);
            Assert.That(channel.SendPacketCount, Is.EqualTo(0));

            manager.DestroyNetworkChannel("oversized-send");
        }

        [Test]
        public void NetworkReliableFifo_004_6_SendClearsPendingWhenQueueCapacityIsExceeded()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("capacity-send", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            NetworkLivenessEventArgs livenessArgs = null;
            baseChannel.NetworkChannelLivenessChanged += (_, args) =>
            {
                livenessArgs = NetworkLivenessEventArgs.Create(
                    args.ChannelName,
                    args.InputEvent,
                    args.State,
                    args.Reason,
                    args.TransportType,
                    args.SessionId,
                    args.LastAckSequence,
                    args.PendingCount,
                    args.ElapsedSeconds,
                    args.Recoverable,
                    args.ErrorCode,
                    args.Message);
            };

            for (var i = 0; i < ReliableFifoProtocolOptions.PendingQueueMaxCount; i++)
            {
                channel.Send(new SnapshotMessageObject { Name = "msg-" + i, Value = i });
            }

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(ReliableFifoProtocolOptions.PendingQueueMaxCount));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo((ulong)(ReliableFifoProtocolOptions.PendingQueueMaxCount + 1)));

            Assert.Throws<GameFrameworkException>(() =>
                channel.Send(new SnapshotMessageObject { Name = "overflow", Value = 999 }));

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(livenessArgs, Is.Not.Null);
            Assert.That(livenessArgs.ChannelName, Is.EqualTo("capacity-send"));
            Assert.That(livenessArgs.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.ResumeRejected));
            Assert.That(livenessArgs.State, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(livenessArgs.Reason, Is.EqualTo(ReliableFifoProtocolOptions.PendingQueueOverflowReason));
            Assert.That(livenessArgs.PendingCount, Is.EqualTo(0));
            Assert.That(livenessArgs.Message, Does.Contain("Reliable pending queue capacity exceeded."));
            Assert.That(livenessArgs.Message, Does.Contain("ChannelName=capacity-send"));
            Assert.That(livenessArgs.Message, Does.Contain("PendingCount=0"));

            manager.DestroyNetworkChannel("capacity-send");
            ReferencePool.Release(livenessArgs);
        }

        [Test]
        public void NetworkReliableFifo_005_1_CumulativeAckRemovesAcknowledgedPendingMessages()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("ack-send", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            channel.Send(new SnapshotMessageObject { Name = "third", Value = 3 });

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(3));

            Assert.That(baseChannel.AcknowledgeReliablePendingThrough(2ul), Is.EqualTo(2));

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(1));
            Assert.That(baseChannel.AcknowledgeReliablePendingThrough(2ul), Is.EqualTo(0));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(1));
            Assert.That(baseChannel.AcknowledgeReliablePendingThrough(3ul), Is.EqualTo(1));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));

            manager.DestroyNetworkChannel("ack-send");
        }

        [Test]
        public void NetworkReliableFifo_005_3_DuplicateResponseCompletesRpcByOriginalUniqueId()
        {
            var rpcState = new NetworkManager.RpcState(3000);
            var request = new TestRequestMessage();
            var response = new TestResponseMessage
            {
                Value = "cached-response"
            };
            response.SetUpdateUniqueId(request.UniqueId);

            var task = rpcState.Call(request, false);

            Assert.That(rpcState.TryReply(response), Is.True);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result, Is.SameAs(response));
            Assert.That(((TestResponseMessage)task.Result).Value, Is.EqualTo("cached-response"));

            rpcState.Dispose();
        }

        [Test]
        public void NetworkReliableFifo_005_4_AckControlMessagesBypassReliableBusinessFifo()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("ack-control", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new TestAckControlMessage());

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(1));

            channel.Send(new SnapshotMessageObject { Name = "business", Value = 1 });

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(1));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(2ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(2));

            manager.DestroyNetworkChannel("ack-control");
        }

        [Test]
        public void NetworkReliableFifo_005_5_AckDiagnosticsExposeProtocolFields()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("ack-diagnostics", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            channel.Send(new SnapshotMessageObject { Name = "third", Value = 3 });

            var removed = baseChannel.AcknowledgeReliablePendingThrough(
                999ul,
                2ul,
                out var diagnostics);

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(diagnostics.ChannelName, Is.EqualTo("ack-diagnostics"));
            Assert.That(diagnostics.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.AckReceived));
            Assert.That(diagnostics.State, Is.EqualTo(NetworkLivenessState.Connected));
            Assert.That(diagnostics.Reason, Is.EqualTo(NetworkLivenessReason.AckReceived));
            Assert.That(diagnostics.TransportType, Is.EqualTo(baseChannel.AddressFamily.ToString()));
            Assert.That(diagnostics.SessionId, Is.EqualTo(999ul));
            Assert.That(diagnostics.LastAckSequence, Is.EqualTo(2ul));
            Assert.That(diagnostics.PendingCount, Is.EqualTo(1));
            Assert.That(diagnostics.Recoverable, Is.False);
            Assert.That(diagnostics.Message, Does.Contain("ChannelName=ack-diagnostics"));
            Assert.That(diagnostics.Message, Does.Contain("SessionId=999"));
            Assert.That(diagnostics.Message, Does.Contain("AckSequence=2"));
            Assert.That(diagnostics.Message, Does.Contain("RemovedCount=2"));
            Assert.That(diagnostics.Message, Does.Contain("PendingCount=1"));

            ReferencePool.Release(diagnostics);
            manager.DestroyNetworkChannel("ack-diagnostics");
        }

        [Test]
        public void UnifiedLivenessStateDefinesExpectedStates()
        {
            Assert.That((int)NetworkLivenessState.Connected, Is.EqualTo(0));
            Assert.That((int)NetworkLivenessState.SuspectDisconnected, Is.EqualTo(1));
            Assert.That((int)NetworkLivenessState.SilentReconnecting, Is.EqualTo(2));
            Assert.That((int)NetworkLivenessState.Resuming, Is.EqualTo(3));
            Assert.That((int)NetworkLivenessState.BusinessReconnecting, Is.EqualTo(4));
            Assert.That((int)NetworkLivenessState.Closed, Is.EqualTo(5));
        }

        [Test]
        public void UnifiedLivenessInputEventsDefineExpectedNames()
        {
            Assert.That(NetworkLivenessInputEvent.TransportConnected, Is.EqualTo("TransportConnected"));
            Assert.That(NetworkLivenessInputEvent.TransportDisconnected, Is.EqualTo("TransportDisconnected"));
            Assert.That(NetworkLivenessInputEvent.TransportError, Is.EqualTo("TransportError"));
            Assert.That(NetworkLivenessInputEvent.ClosedByUser, Is.EqualTo("ClosedByUser"));
            Assert.That(NetworkLivenessInputEvent.PacketReceived, Is.EqualTo("PacketReceived"));
            Assert.That(NetworkLivenessInputEvent.HeartbeatAckReceived, Is.EqualTo("HeartbeatAckReceived"));
            Assert.That(NetworkLivenessInputEvent.HeartbeatTimeout, Is.EqualTo("HeartbeatTimeout"));
            Assert.That(NetworkLivenessInputEvent.NoPacketTimeout, Is.EqualTo("NoPacketTimeout"));
            Assert.That(NetworkLivenessInputEvent.ResumeAccepted, Is.EqualTo("ResumeAccepted"));
            Assert.That(NetworkLivenessInputEvent.ResumeRejected, Is.EqualTo("ResumeRejected"));
        }

        [Test]
        public void UnifiedLivenessEventArgsCarryDiagnostics()
        {
            var args = NetworkLivenessEventArgs.Create(
                "main",
                NetworkLivenessInputEvent.HeartbeatTimeout,
                NetworkLivenessState.SuspectDisconnected,
                NetworkLivenessReason.HeartbeatTimeout,
                "tcp",
                1234ul,
                25ul,
                3,
                4.5f,
                true,
                1001,
                "timeout");

            Assert.That(args.ChannelName, Is.EqualTo("main"));
            Assert.That(args.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.HeartbeatTimeout));
            Assert.That(args.State, Is.EqualTo(NetworkLivenessState.SuspectDisconnected));
            Assert.That(args.Reason, Is.EqualTo(NetworkLivenessReason.HeartbeatTimeout));
            Assert.That(args.TransportType, Is.EqualTo("tcp"));
            Assert.That(args.SessionId, Is.EqualTo(1234ul));
            Assert.That(args.LastAckSequence, Is.EqualTo(25ul));
            Assert.That(args.PendingCount, Is.EqualTo(3));
            Assert.That(args.ElapsedSeconds, Is.EqualTo(4.5f));
            Assert.That(args.Recoverable, Is.True);
            Assert.That(args.ErrorCode, Is.EqualTo(1001));
            Assert.That(args.Message, Is.EqualTo("timeout"));
        }

        [Test]
        public void NetworkChannelBaseExposesUnifiedLivenessState()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("liveness", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;

            Assert.That(baseChannel, Is.Not.Null);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.Connected));

            baseChannel.HandleLivenessInput(NetworkLivenessInputEvent.ResumeStarted, NetworkLivenessReason.ResumeStarted);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.Resuming));

            baseChannel.HandleLivenessInput(NetworkLivenessInputEvent.ResumeAccepted, NetworkLivenessReason.ResumeAccepted);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.Connected));

            manager.DestroyNetworkChannel("liveness");
        }

        [Test]
        public void NetworkReliableFifo_006_1_ResumeControlMessagesEnterResumingStateAndBypassReliableBusinessFifo()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("resume-control", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            NetworkLivenessEventArgs livenessArgs = null;
            baseChannel.NetworkChannelLivenessChanged += (_, args) =>
            {
                if (livenessArgs != null)
                {
                    ReferencePool.Release(livenessArgs);
                }
                livenessArgs = NetworkLivenessEventArgs.Create(
                    args.ChannelName,
                    args.InputEvent,
                    args.State,
                    args.Reason,
                    args.TransportType,
                    args.SessionId,
                    args.LastAckSequence,
                    args.PendingCount,
                    args.ElapsedSeconds,
                    args.Recoverable,
                    args.ErrorCode,
                    args.Message);
            };

            baseChannel.HandleLivenessInput(NetworkLivenessInputEvent.ResumeAccepted, NetworkLivenessReason.ResumeAccepted);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.Connected));

            baseChannel.HandleLivenessInput(NetworkLivenessInputEvent.ResumeRejected, NetworkLivenessReason.ResumeRejected);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));

            baseChannel.HandleLivenessInput(NetworkLivenessInputEvent.HeartbeatTimeout, NetworkLivenessReason.HeartbeatTimeout);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.SuspectDisconnected));

            channel.Send(new TestResumeControlMessage());
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(1));

            channel.Send(new SnapshotMessageObject { Name = "business", Value = 10 });
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(1));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(2ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(2));

            Assert.That(livenessArgs, Is.Not.Null);
            Assert.That(livenessArgs.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.HeartbeatTimeout));
            Assert.That(livenessArgs.State, Is.EqualTo(NetworkLivenessState.SuspectDisconnected));

            ReferencePool.Release(livenessArgs);
            manager.DestroyNetworkChannel("resume-control");
        }

        [Test]
        public void NetworkReliableFifo_006_2_ResumeResultAcceptedConsumesAckAndRejectedRequiresBusinessReconnect()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("resume-result", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            channel.Send(new SnapshotMessageObject { Name = "third", Value = 3 });
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(3));

            var accepted = baseChannel.HandleResumeResult(
                true,
                777ul,
                2ul,
                out var acceptedDiagnostics);

            Assert.That(accepted, Is.True);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.Connected));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(1));
            Assert.That(acceptedDiagnostics.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.ResumeAccepted));
            Assert.That(acceptedDiagnostics.State, Is.EqualTo(NetworkLivenessState.Connected));
            Assert.That(acceptedDiagnostics.Reason, Is.EqualTo(NetworkLivenessReason.ResumeAccepted));
            Assert.That(acceptedDiagnostics.SessionId, Is.EqualTo(777ul));
            Assert.That(acceptedDiagnostics.LastAckSequence, Is.EqualTo(2ul));
            Assert.That(acceptedDiagnostics.PendingCount, Is.EqualTo(1));
            Assert.That(acceptedDiagnostics.Message, Does.Contain("Accepted=True"));
            Assert.That(acceptedDiagnostics.Message, Does.Contain("AckSequence=2"));

            ReferencePool.Release(acceptedDiagnostics);

            var rejected = baseChannel.HandleResumeResult(
                false,
                777ul,
                2ul,
                out var rejectedDiagnostics);

            Assert.That(rejected, Is.False);
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(rejectedDiagnostics.InputEvent, Is.EqualTo(NetworkLivenessInputEvent.ResumeRejected));
            Assert.That(rejectedDiagnostics.State, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(rejectedDiagnostics.Reason, Is.EqualTo(NetworkLivenessReason.ResumeRejected));
            Assert.That(rejectedDiagnostics.SessionId, Is.EqualTo(777ul));
            Assert.That(rejectedDiagnostics.LastAckSequence, Is.EqualTo(2ul));
            Assert.That(rejectedDiagnostics.PendingCount, Is.EqualTo(1));
            Assert.That(rejectedDiagnostics.Message, Does.Contain("Accepted=False"));

            ReferencePool.Release(rejectedDiagnostics);
            manager.DestroyNetworkChannel("resume-result");
        }

        [Test]
        public void NetworkReliableFifo_006_3_ResumeAcceptedReplaysRemainingPendingMessagesInFifoOrder()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("resume-replay", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            channel.Send(new SnapshotMessageObject { Name = "third", Value = 3 });
            Assert.That(channel.SendPacketCount, Is.EqualTo(3));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(3));

            Assert.That(baseChannel.HandleResumeResult(true, 888ul, 1ul, out var diagnostics), Is.True);
            ReferencePool.Release(diagnostics);
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(2));

            var replayed = baseChannel.ReplayReliablePendingMessages();

            Assert.That(replayed, Is.EqualTo(2));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(2));
            Assert.That(channel.SendPacketCount, Is.EqualTo(5));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(4ul));

            manager.DestroyNetworkChannel("resume-replay");
        }

        [Test]
        public void NetworkReliableFifo_006_4_ResumeRejectedClearsPendingAndFailsOutstandingRpc()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("resume-reject", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            var rpcTask = channel.Call<TestResponseMessage>(new TestRequestMessage(), false);

            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(3));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(4ul));

            var result = baseChannel.HandleResumeResult(false, 999ul, 0ul, out var diagnostics);
            var failedRpcCount = baseChannel.FailReliableSessionForResumeFailure("Resume rejected.");

            Assert.That(result, Is.False);
            Assert.That(failedRpcCount, Is.EqualTo(3));
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(rpcTask.IsFaulted, Is.True);
            Assert.That(rpcTask.Exception, Is.Not.Null);
            Assert.That(rpcTask.Exception.InnerException, Is.TypeOf<GameFrameworkException>());
            Assert.That(diagnostics.PendingCount, Is.EqualTo(3));
            Assert.That(diagnostics.Message, Does.Contain("PendingCount=3"));

            ReferencePool.Release(diagnostics);
            manager.DestroyNetworkChannel("resume-reject");
        }

        [Test]
        public void NetworkReliableFifo_006_5_SilentWindowAndSessionTtlRejectExpiredResume()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("resume-boundary", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);

            Assert.That(baseChannel.EvaluateResumeBoundary(9.9f, 29.9f, 123ul, out var withinDiagnostics), Is.True);
            Assert.That(withinDiagnostics.State, Is.EqualTo(NetworkLivenessState.Resuming));
            Assert.That(withinDiagnostics.Reason, Is.EqualTo(NetworkLivenessReason.ResumeStarted));
            Assert.That(withinDiagnostics.SessionId, Is.EqualTo(123ul));
            Assert.That(withinDiagnostics.Recoverable, Is.True);
            ReferencePool.Release(withinDiagnostics);

            Assert.That(baseChannel.EvaluateResumeBoundary(10.1f, 20f, 123ul, out var silentExpiredDiagnostics), Is.False);
            Assert.That(silentExpiredDiagnostics.State, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(silentExpiredDiagnostics.Reason, Is.EqualTo(NetworkLivenessReason.ResumeRejected));
            Assert.That(silentExpiredDiagnostics.Message, Does.Contain("SilentResumeWindowSeconds=10"));
            ReferencePool.Release(silentExpiredDiagnostics);

            Assert.That(baseChannel.EvaluateResumeBoundary(5f, 30.1f, 123ul, out var sessionExpiredDiagnostics), Is.False);
            Assert.That(sessionExpiredDiagnostics.State, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(sessionExpiredDiagnostics.Reason, Is.EqualTo(NetworkLivenessReason.SessionExpired));
            Assert.That(sessionExpiredDiagnostics.Message, Does.Contain("ServerSessionTtlSeconds=30"));
            ReferencePool.Release(sessionExpiredDiagnostics);

            manager.DestroyNetworkChannel("resume-boundary");
        }

        [Test]
        public void NetworkReliableFifo_007_1_ServerKickControlMessageBypassesFifoAndRequiresBusinessReconnect()
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel("server-kick", new TestNetworkChannelHelper(), 3000);
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            ActivateChannelForSend(baseChannel);

            channel.Send(new TestServerKickControlMessage());
            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(0));
            Assert.That(baseChannel.NextReliableSequence, Is.EqualTo(1ul));
            Assert.That(channel.SendPacketCount, Is.EqualTo(1));

            baseChannel.HandleServerKickControl(NetworkLivenessReason.ServerKick, 456ul, out var diagnostics);

            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(diagnostics.ChannelName, Is.EqualTo("server-kick"));
            Assert.That(diagnostics.State, Is.EqualTo(NetworkLivenessState.BusinessReconnecting));
            Assert.That(diagnostics.Reason, Is.EqualTo(NetworkLivenessReason.ServerKick));
            Assert.That(diagnostics.SessionId, Is.EqualTo(456ul));
            Assert.That(diagnostics.Recoverable, Is.False);
            Assert.That(diagnostics.Message, Does.Contain("Server kick control received."));
            Assert.That(diagnostics.Message, Does.Contain("Reason=ServerKick"));

            ReferencePool.Release(diagnostics);
            manager.DestroyNetworkChannel("server-kick");
        }

        [TestCase(ServiceType.Tcp, "SystemTcpNetworkChannel")]
        [TestCase(ServiceType.KcpUdp, "KcpNetworkChannel")]
        [TestCase(ServiceType.KcpTcp, "KcpNetworkChannel")]
        [TestCase(ServiceType.KcpWebSocket, "KcpNetworkChannel")]
        public void NetworkReliableFifo_008_1_SupportedTransportsShareAckResumeFifoSemantics(
            ServiceType serviceType,
            string expectedChannelTypeName)
        {
            var manager = new NetworkManager();
            var channelName = "cross-transport-" + serviceType;
            var channel = manager.CreateNetworkChannel(
                channelName,
                new TestNetworkChannelHelper(),
                3000,
                serviceType,
                new KcpConfig());
            var baseChannel = channel as NetworkManager.NetworkChannelBase;
            Assert.That(baseChannel, Is.Not.Null);
            Assert.That(channel.GetType().Name, Is.EqualTo(expectedChannelTypeName));
            ActivateChannelForSend(baseChannel);

            channel.Send(new SnapshotMessageObject { Name = "first", Value = 1 });
            channel.Send(new SnapshotMessageObject { Name = "second", Value = 2 });
            channel.Send(new SnapshotMessageObject { Name = "third", Value = 3 });

            Assert.That(baseChannel.ReliablePendingCount, Is.EqualTo(3));
            Assert.That(baseChannel.HandleResumeResult(true, 6000ul, 2ul, out var diagnostics), Is.True);
            Assert.That(diagnostics.TransportType, Is.EqualTo(baseChannel.AddressFamily.ToString()));
            Assert.That(diagnostics.SessionId, Is.EqualTo(6000ul));
            Assert.That(diagnostics.LastAckSequence, Is.EqualTo(2ul));
            Assert.That(diagnostics.PendingCount, Is.EqualTo(1));
            ReferencePool.Release(diagnostics);

            Assert.That(baseChannel.ReplayReliablePendingMessages(), Is.EqualTo(1));
            Assert.That(channel.SendPacketCount, Is.EqualTo(4));
            Assert.That(baseChannel.LivenessState, Is.EqualTo(NetworkLivenessState.Connected));

            manager.DestroyNetworkChannel(channelName);
        }

        [Test]
        public void NetworkReliableFifo_008_4_ReadmeAndChangelogDocumentBreakingUpgrade()
        {
            var readme = File.ReadAllText("Packages/com.gameframex.unity.network/README.zh-CN.md");
            var changelog = File.ReadAllText("Packages/com.gameframex.unity.network/CHANGELOG.md");

            Assert.That(readme, Does.Contain("Reliable FIFO"));
            Assert.That(readme, Does.Contain("破坏性升级"));
            Assert.That(readme, Does.Contain("ACK / Resume"));

            Assert.That(changelog, Does.Contain("Reliable FIFO"));
            Assert.That(changelog, Does.Contain("破坏性升级"));
            Assert.That(changelog, Does.Contain("2.6.9"));
        }

        private static void WriteFrame(Stream stream, byte[] payload)
        {
            var length = payload.Length;
            var frame = new byte[4 + length];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }

        private static void WriteUInt64BigEndian(byte[] buffer, ulong value, ref int offset)
        {
            for (var i = 7; i >= 0; i--)
            {
                buffer[offset++] = (byte)(value >> (i * 8));
            }
        }

        private static void WriteFrameInChunks(Stream stream, byte[] payload, int chunkSize)
        {
            var length = payload.Length;
            var frame = new byte[4 + length];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

            for (var offset = 0; offset < frame.Length; offset += chunkSize)
            {
                var count = Math.Min(chunkSize, frame.Length - offset);
                stream.Write(frame, offset, count);
                stream.Flush();
                Thread.Sleep(10);
            }
        }

        private static byte[] ReadExactly(Stream stream, int length)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var bytesRead = stream.Read(buffer, offset, length - offset);
                if (bytesRead <= 0)
                {
                    throw new EndOfStreamException();
                }

                offset += bytesRead;
            }

            return buffer;
        }

        private static void InvokeChannelUpdate(INetworkChannel channel, float elapseSeconds, float realElapseSeconds)
        {
            var updateMethod = channel.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(updateMethod, Is.Not.Null);
            updateMethod.Invoke(channel, new object[] { elapseSeconds, realElapseSeconds });
        }

        private static void ActivateChannelForSend(NetworkManager.NetworkChannelBase channel)
        {
            var socketField = typeof(NetworkManager.NetworkChannelBase).GetField("PSocket", BindingFlags.Instance | BindingFlags.NonPublic);
            var activeField = typeof(NetworkManager.NetworkChannelBase).GetField("m_PActive", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(socketField, Is.Not.Null);
            Assert.That(activeField, Is.Not.Null);

            socketField.SetValue(channel, new TestNetworkSocket());
            activeField.SetValue(channel, true);
        }

        private static void AssertKcpConfigRejected(string channelName, KcpConfig config)
        {
            var manager = new NetworkManager();
            var channel = manager.CreateNetworkChannel(
                channelName,
                new TestNetworkChannelHelper(),
                3000,
                ServiceType.Kcp,
                config);

            Assert.Throws<GameFrameworkException>(() =>
                channel.Connect(new Uri("kcp://127.0.0.1:9")));
            Assert.That(channel.Connected, Is.False);

            manager.DestroyNetworkChannel(channelName);
        }

        [Test]
        public void MessageObjectJsonSnapshot_UsesLitJsonWithoutLeakingUniqueId()
        {
            var message = new SnapshotMessageObject
            {
                Name = "login",
                Value = 42,
            };

            string litJson = new LitJsonHelper().ToJson(message);

            Assert.That(litJson, Does.Not.Contain("UniqueId"));
            Assert.That(litJson, Is.EqualTo("{\"Name\":\"login\",\"Value\":42}"));
        }

        [Test]
        public void MessageObjectToString_UsesLitJsonHelperWithoutLeakingUniqueId()
        {
            Utility.Json.SetJsonHelper(new LitJsonHelper());
            var message = new SnapshotMessageObject
            {
                Name = "login",
                Value = 42,
            };

            string json = message.ToString();

            Assert.That(json, Is.EqualTo("{\"Name\":\"login\",\"Value\":42}"));
            Assert.That(json, Does.Not.Contain("UniqueId"));
        }

        [Test]
        public void MessageHttpObjectJsonSnapshot_UsesLitJsonWithoutLeakingBody()
        {
            var message = new MessageHttpObject
            {
                Id = 1001,
                UniqueId = 2002,
                Body = new byte[] { 1, 2, 3 },
            };

            string litJson = new LitJsonHelper().ToJson(message);

            Assert.That(litJson, Does.Not.Contain("Body"));
            Assert.That(litJson, Is.EqualTo("{\"Id\":1001,\"UniqueId\":2002}"));
        }

        private sealed class TestNetworkChannelHelper : INetworkChannelHelper
        {
            public int HeartBeatSendCount { get; private set; }

            public bool HeartBeatSendResult { get; set; }

            public void Initialize(INetworkChannel networkChannel)
            {
            }

            public void Shutdown()
            {
            }

            public void PrepareForConnecting()
            {
            }

            public bool SendHeartBeat()
            {
                HeartBeatSendCount++;
                return HeartBeatSendResult;
            }

            public bool SerializePacketHeader<T>(T messageObject, MemoryStream destination, out byte[] messageBodyBuffer)
                where T : MessageObject
            {
                switch (messageObject)
                {
                    case LargePayloadMessageObject largePayloadMessageObject:
                        messageBodyBuffer = largePayloadMessageObject.Payload ?? Array.Empty<byte>();
                        break;
                    case TestHeartBeatMessage:
                        messageBodyBuffer = Encoding.UTF8.GetBytes("hb");
                        break;
                    default:
                        messageBodyBuffer = Encoding.UTF8.GetBytes("ok");
                        break;
                }

                return true;
            }

            public bool SerializePacketBody(byte[] messageBodyBuffer, MemoryStream destination)
            {
                return false;
            }

            public bool DeserializePacketHeader(byte[] source)
            {
                return false;
            }

            public bool DeserializePacketBody(byte[] source, int messageId, out MessageObject messageObject)
            {
                messageObject = null;
                return false;
            }
        }

        private sealed class SnapshotMessageObject : MessageObject
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }

        private sealed class LargePayloadMessageObject : MessageObject
        {
            public byte[] Payload { get; set; }
        }

        private sealed class TestHeartBeatMessage : MessageObject, IHeartBeatMessage
        {
        }

        private sealed class TestAckControlMessage : MessageObject, IAckControlMessage
        {
        }

        private sealed class TestResumeControlMessage : MessageObject, IResumeControlMessage
        {
        }

        private sealed class TestServerKickControlMessage : MessageObject, IServerKickControlMessage
        {
        }

        private sealed class TestRequestMessage : MessageObject, IRequestMessage
        {
        }

        private sealed class TestResponseMessage : MessageObject, IResponseMessage
        {
            public int ErrorCode => 0;

            public string Value { get; set; }
        }

        private sealed class FixedSerializer : IMessageSerializer
        {
            private readonly byte[] _bytes;

            public FixedSerializer(byte[] bytes)
            {
                _bytes = bytes;
            }

            public byte[] Serialize<T>(T message) where T : MessageObject
            {
                return _bytes;
            }

            public object Deserialize(byte[] bytes, Type messageType)
            {
                return Activator.CreateInstance(messageType);
            }
        }

        private sealed class TestNetworkSocket : INetworkSocket
        {
            public bool IsConnected => true;
            public bool IsClosed => false;
            public EndPoint LocalEndPoint => null;
            public EndPoint RemoteEndPoint => null;
            public int ReceiveBufferSize { get; set; }
            public int SendBufferSize { get; set; }
            public void Shutdown()
            {
            }

            public void Close()
            {
            }
        }

        private sealed class TcpListenerWrapper : IDisposable
        {
            private readonly TcpListener _listener;
            private Thread _thread;

            public TcpListenerWrapper()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
            }

            public int Port
            {
                get { return ((IPEndPoint)_listener.LocalEndpoint).Port; }
            }

            public void Start(Action<TcpClient> onClientAccepted)
            {
                _listener.Start();
                _thread = new Thread(() =>
                {
                    try
                    {
                        using (var client = _listener.AcceptTcpClient())
                        {
                            onClientAccepted(client);
                        }
                    }
                    catch (SocketException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                });
                _thread.IsBackground = true;
                _thread.Start();
            }

            public void Dispose()
            {
                _listener.Stop();
                if (_thread != null && !_thread.Join(TimeSpan.FromSeconds(1)))
                {
                    _thread = null;
                }
            }
        }
    }
}

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
using System.Buffers;

namespace GameFrameX.Network.Runtime
{
    /// <summary>
    /// 基于 <see cref="ArrayPool{T}"/> 的字节缓冲区所有权封装。
    /// </summary>
    internal sealed class PooledBuffer : IDisposable
    {
        private readonly ArrayPool<byte> m_ArrayPool;
        private byte[] m_Buffer;
        private int m_Length;
        private bool m_Disposed;

        private PooledBuffer(ArrayPool<byte> arrayPool, byte[] buffer, int length)
        {
            m_ArrayPool = arrayPool;
            m_Buffer = buffer;
            m_Length = length;
            m_Disposed = false;
        }

        /// <summary>
        /// 获取租借的底层缓冲区。
        /// </summary>
        public byte[] Buffer
        {
            get
            {
                ThrowIfDisposed();
                return m_Buffer;
            }
        }

        /// <summary>
        /// 获取或设置当前有效数据长度。
        /// </summary>
        public int Length
        {
            get
            {
                ThrowIfDisposed();
                return m_Length;
            }
            set
            {
                ThrowIfDisposed();
                if (value < 0 || value > m_Buffer.Length)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                m_Length = value;
            }
        }

        /// <summary>
        /// 获取底层缓冲区容量。
        /// </summary>
        public int Capacity
        {
            get
            {
                ThrowIfDisposed();
                return m_Buffer.Length;
            }
        }

        /// <summary>
        /// 从共享数组池租借指定最小容量的缓冲区。
        /// </summary>
        /// <param name="minimumLength">最小容量。</param>
        /// <returns>租借的缓冲区。</returns>
        public static PooledBuffer Rent(int minimumLength)
        {
            if (minimumLength < 0)
            {
                throw new ArgumentOutOfRangeException("minimumLength");
            }

            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength);
            return new PooledBuffer(ArrayPool<byte>.Shared, buffer, 0);
        }

        /// <summary>
        /// 从源数组拷贝有效数据到池化缓冲区。
        /// </summary>
        /// <param name="source">源数组。</param>
        /// <param name="offset">源数组偏移。</param>
        /// <param name="count">拷贝长度。</param>
        /// <returns>包含拷贝数据的池化缓冲区。</returns>
        public static PooledBuffer CopyFrom(byte[] source, int offset, int count)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (offset < 0 || count < 0 || offset > source.Length - count)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            var pooledBuffer = Rent(count);
            if (count > 0)
            {
                System.Buffer.BlockCopy(source, offset, pooledBuffer.m_Buffer, 0, count);
            }

            pooledBuffer.m_Length = count;
            return pooledBuffer;
        }

        /// <summary>
        /// 将缓冲区归还数组池。重复调用安全。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            var buffer = m_Buffer;
            m_Buffer = null;
            m_Length = 0;

            if (buffer != null)
            {
                m_ArrayPool.Return(buffer);
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(PooledBuffer));
            }
        }
    }
}

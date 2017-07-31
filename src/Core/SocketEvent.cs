using System;
using System.Net.Sockets;
using FastNet.Memory;

namespace FastNet.Core
{
    public sealed class SocketEvent : SocketAsyncEventArgs, IPoolable
    {
        public ArraySegment<byte> Segment { get; private set; }
        private readonly BufferPool pool;

        public SocketEvent(BufferPool pool, EventHandler<SocketAsyncEventArgs> callback)
        {
            this.pool = pool;
            Completed += callback;
        }

        public void CheckIn()
        {
            this.pool.Release(Segment);
            SetBuffer(null, 0, 0);
        }

        public void CheckOut()
        {
            Segment = this.pool.Acquire();
            SetBuffer(Segment.Array, Segment.Offset, Segment.Count);
        }
    }
}
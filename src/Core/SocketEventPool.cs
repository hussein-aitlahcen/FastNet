using System;
using System.Net.Sockets;
using FastNet.Memory;

namespace FastNet.Core
{
    public sealed class SocketEventPool : ObjectPool<SocketEvent>
    {
        public SocketEventPool(BufferPool bufferPool, EventHandler<SocketAsyncEventArgs> callback)
         : base(() => new SocketEvent(bufferPool, callback))
        {
        }
    }
}
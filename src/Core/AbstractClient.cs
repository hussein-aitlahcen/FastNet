using System.Net.Sockets;

namespace FastNet.Core
{
    public abstract class AbstractClient
    {
        public ulong Id { get; }

        public Socket Socket { get; }

        protected AbstractClient(ulong id, Socket socket)
        {
            Id = id;
            Socket = socket;
        }
    }
}
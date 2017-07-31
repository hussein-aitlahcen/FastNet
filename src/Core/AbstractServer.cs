using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using FastNet.Memory;
using System.Threading.Tasks;

namespace FastNet.Core
{
    public enum ServerEventType
    {
        ClientConnected,
        ClientDisconnected,
        DataReceived,
        DataSent
    }


    public abstract class AbstractServer
    {
        public interface IServerEvent
        {
            ServerEventType Type { get; }

            AbstractClient Client { get; }
        }

        public sealed class ClientConnectedEvent : IServerEvent
        {
            public ServerEventType Type => ServerEventType.ClientConnected;
            public AbstractClient Client { get; }

            public ClientConnectedEvent(AbstractClient client)
            {
                Client = client;
            }
        }

        public sealed class ClientDisconnectedEvent : IServerEvent
        {
            public ServerEventType Type => ServerEventType.ClientDisconnected;
            public AbstractClient Client { get; }

            public ClientDisconnectedEvent(AbstractClient client)
            {
                Client = client;
            }
        }

        public sealed class DataReceivedEvent : IServerEvent
        {
            public ServerEventType Type => ServerEventType.DataReceived;
            public AbstractClient Client { get; }
            public byte[] Buffer { get; }
            public int BytesTransferred { get; }

            public DataReceivedEvent(AbstractClient client, byte[] buffer, int bytesTransferred)
            {
                Client = client;
                Buffer = buffer;
                BytesTransferred = bytesTransferred;
            }
        }

        public sealed class DataSentEvent : IServerEvent
        {
            public ServerEventType Type => ServerEventType.DataSent;
            public AbstractClient Client { get; }
            public byte[] Buffer { get; }

            public DataSentEvent(AbstractClient client, byte[] buffer)
            {
                Client = client;
                Buffer = buffer;
            }
        }

        private const int DefaultBacklog = 100;
        private const int DefaultBufferCount = 1000;
        private const int DefaultBufferSize = 1024;

        public IConnectableObservable<IServerEvent> EventStream { get; }
        private readonly IScheduler scheduler;
        private readonly Queue<IServerEvent> events;
        private readonly Dictionary<ulong, AbstractClient> clients;
        private readonly Socket socket;
        private readonly BufferPool bufferPool;
        private readonly IObjectPool<SocketEvent> receiveEventPool;
        private ulong nextClientId;

        protected AbstractServer(IScheduler scheduler)
        {
            this.nextClientId = 1;
            this.scheduler = scheduler;
            this.bufferPool = new BufferPool(DefaultBufferCount, DefaultBufferSize);
            this.receiveEventPool = new CheckableObjectPool<SocketEvent>(new SocketEventPool(this.bufferPool, this.OnIOCompleted));
            this.events = new Queue<IServerEvent>();
            this.clients = new Dictionary<ulong, AbstractClient>();
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            EventStream = Observable
                .Create<IServerEvent>(observable =>
                {
                    return scheduler.Schedule(async recurse =>
                    {
                        try
                        {
                            while (this.events.Count > 0)
                            {
                                observable.OnNext(this.events.Dequeue());
                            }

                            await Task.Delay(1);

                            recurse();
                        }
                        catch (Exception e)
                        {
                            observable.OnError(e);
                        }
                    });
                })
                .SubscribeOn(scheduler)
                .Publish();
            EventStream.Connect();
        }

        public void Start(string ip, int port)
        {
            this.socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            this.socket.Listen(DefaultBacklog);
            StartAccept(null);
        }

        private void StartAccept(SocketAsyncEventArgs socketEvent)
        {
            this.scheduler.Schedule(() =>
            {
                if (socketEvent == null)
                {
                    socketEvent = new SocketAsyncEventArgs();
                    socketEvent.Completed += OnIOCompleted;
                }
                else
                {
                    socketEvent.AcceptSocket = null;
                }

                if (!this.socket.AcceptAsync(socketEvent))
                {
                    OnAccepted(socketEvent);
                }
            });
        }

        private void OnIOCompleted(object sender, SocketAsyncEventArgs socketEvent)
        {
            switch (socketEvent.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    OnAccepted(socketEvent);
                    break;

                case SocketAsyncOperation.Receive:
                    OnReceived((SocketEvent)socketEvent);
                    break;

                case SocketAsyncOperation.Send:
                    OnSent(socketEvent);
                    break;

                case SocketAsyncOperation.Disconnect:
                    OnDisconnected((SocketEvent)socketEvent);
                    break;
            }
        }

        private void OnAccepted(SocketAsyncEventArgs socketEvent)
        {
            this.scheduler.Schedule(() =>
            {
                var client = CreateClient(nextClientId++, socketEvent.AcceptSocket);
                var receiveEvent = this.receiveEventPool.Acquire();
                receiveEvent.AcceptSocket = client.Socket;
                receiveEvent.UserToken = client;
                this.clients[client.Id] = client;
                StartReceive(receiveEvent);
                StartAccept(socketEvent);
                PublishEvent(new ClientConnectedEvent(client));
            });
        }

        private void StartReceive(SocketEvent socketEvent)
        {
            this.scheduler.Schedule(() =>
            {
                if (!socketEvent.AcceptSocket.ReceiveAsync(socketEvent))
                {
                    OnReceived(socketEvent);
                }
            });
        }

        private void OnReceived(SocketEvent socketEvent)
        {
            this.scheduler.Schedule(() =>
            {
                if (socketEvent.BytesTransferred > 0)
                {
                    var incommingData = new byte[socketEvent.BytesTransferred];
                    for (var i = 0; i < socketEvent.BytesTransferred; i++)
                    {
                        incommingData[i] = socketEvent.Segment[i];
                    }
                    PublishEvent(new DataReceivedEvent((AbstractClient)socketEvent.UserToken, incommingData, socketEvent.BytesTransferred));
                    StartReceive(socketEvent);
                }
                else
                {
                    Disconnect(socketEvent);
                }
            });
        }

        public void Send(AbstractClient client, byte[] data)
        {
            var sendEvent = new SocketAsyncEventArgs();
            sendEvent.Completed += OnIOCompleted;
            sendEvent.SetBuffer(data, 0, data.Length);
            if (!client.Socket.SendAsync(sendEvent))
            {
                OnSent(sendEvent);
            }
        }

        public void Send(ulong clientId, byte[] data)
        {
            this.scheduler.Schedule(() =>
            {
                var client = GetClient(clientId);
                if (client != null)
                {
                    Send(client, data);
                }
            });
        }

        private void OnSent(SocketAsyncEventArgs socketEvent)
        {
            this.scheduler.Schedule(() =>
            {
                PublishEvent(new DataSentEvent((AbstractClient)socketEvent.UserToken, socketEvent.Buffer));
                socketEvent.SetBuffer(null, 0, 0);
                socketEvent.Dispose();
            });
        }

        private void OnDisconnected(SocketEvent socketEvent)
        {
            Disconnect(socketEvent);
        }

        private void Disconnect(SocketEvent socketEvent)
        {
            this.scheduler.Schedule(() =>
            {
                socketEvent.AcceptSocket.Shutdown(SocketShutdown.Both);
                if (socketEvent.AcceptSocket.Connected)
                {
                    socketEvent.AcceptSocket.Disconnect(true);
                }
                var client = (AbstractClient)socketEvent.UserToken;
                this.clients.Remove(client.Id);
                socketEvent.UserToken = null;
                this.receiveEventPool.Release(socketEvent);
                PublishEvent(new ClientDisconnectedEvent(client));
            });
        }

        private void PublishEvent(IServerEvent serverEvent)
        {
            this.scheduler.Schedule(() =>
            {
                this.events.Enqueue(serverEvent);
            });
        }

        private AbstractClient GetClient(SocketAsyncEventArgs socketEvent) => GetClient((ulong)socketEvent.UserToken);

        public AbstractClient GetClient(ulong clientId) => this.clients.ContainsKey(clientId) ? this.clients[clientId] : null;

        protected abstract AbstractClient CreateClient(ulong clientId, Socket socket);
    }
}
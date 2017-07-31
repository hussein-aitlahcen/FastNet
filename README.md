# .NET Core 2.0 High Performance Tcp Server 

More doc comming soon

# Simple echo server

```cs
class Client : AbstractClient
{
    public Client(ulong id, Socket socket) : base(id, socket)
    {
    }
}

class Server : AbstractServer
{
    public Server(IScheduler scheduler) : base(scheduler)
    {
    }

    protected override AbstractClient CreateClient(ulong clientId, Socket socket)
    {
        return new Client(clientId, socket);
    }
}

class Program
{
    static void Main(string[] args)
    {
        var server = new Server(new EventLoopScheduler());
        server.Start("0.0.0.0", 1337);
        server.EventStream.Subscribe(ev =>
        {
            // consume event
            switch (ev)
            {
                case Server.ClientConnectedEvent connectedEvent:
                    break;
                case Server.ClientDisconnectedEvent disconnectedEvent:
                    break;
                case Server.DataReceivedEvent dataReceived:
                    // echo
                    server.Send(dataReceived.Client, dataReceived.Buffer);
                    break;
                case Server.DataSentEvent dataSent:
                    break;
            }
            Console.WriteLine(ev);
        }, error =>
        {
            Console.WriteLine(error);
        });
        Console.Read();
    }
}
```
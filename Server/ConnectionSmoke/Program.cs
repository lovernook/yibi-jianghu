using System.Net;
using System.Net.Sockets;
using System.Text;

// M0 transport smoke only. No rooms, battle rules or Unity client integration yet.
if (args.Length != 2 || !int.TryParse(args[1], out int port) || port < 1024 || port > 65535)
    throw new ArgumentException("Usage: ConnectionSmoke server|client <port>");
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
if (args[0] == "server")
{
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    Console.WriteLine("M0_LISTENING " + port);
    try
    {
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        using var stream = peer.GetStream();
        byte[] request = new byte[4];
        await stream.ReadExactlyAsync(request, timeout.Token);
        if (Encoding.ASCII.GetString(request) != "PING") throw new InvalidDataException("Expected PING");
        await stream.WriteAsync(Encoding.ASCII.GetBytes("PONG"), timeout.Token);
        Console.WriteLine("M0_SERVER_OK PING -> PONG");
    }
    finally { listener.Stop(); }
}
else if (args[0] == "client")
{
    using var peer = new TcpClient();
    await peer.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
    using var stream = peer.GetStream();
    await stream.WriteAsync(Encoding.ASCII.GetBytes("PING"), timeout.Token);
    byte[] response = new byte[4];
    await stream.ReadExactlyAsync(response, timeout.Token);
    if (Encoding.ASCII.GetString(response) != "PONG") throw new InvalidDataException("Expected PONG");
    Console.WriteLine("M0_CLIENT_OK PONG received");
}
else throw new ArgumentException("Unknown mode");

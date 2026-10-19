using System.Net.Sockets;

namespace ChatApplication.Server;

public class ClientSession
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string Username { get; }
    public string Channel { get; set; } = ChatApplication.Common.ChatProtocol.BroadcastChannel;
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }

    public ClientSession(string username, TcpClient client)
    {
        Username = username.Trim();
        Client = client;
        Stream = client.GetStream();
    }

    public async Task SendAsync(string message)
    {
        await _sendLock.WaitAsync();
        try
        {
            await Common.Message.SendMessageAsync(Stream, message);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
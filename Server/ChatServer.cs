using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using ChatApplication.Common;

namespace ChatApplication.Server;

public class ChatServer
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

    public ChatServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task StartAsync()
    {
        _listener.Start();

        Console.WriteLine("Server started.");
        Console.WriteLine("Waiting for clients...");

        while (true)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        ClientSession? session = null;

        try
        {
            NetworkStream stream = client.GetStream();

            await Message.SendMessageAsync(stream, "USERNAME");
            string username = (await Message.ReadMessageAsync(stream)).Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                username.Length > 32 ||
                username.Any(char.IsControl))
            {
                await Message.SendMessageAsync(stream, "ERROR Invalid username.");
                return;
            }

            ClientSession newSession = new ClientSession(username, client);
            if (!_sessions.TryAdd(newSession.Username, newSession))
            {
                await Message.SendMessageAsync(stream, "ERROR Username is already in use.");
                return;
            }

            session = newSession;
            Console.WriteLine($"{username} connected.");
            await BroadcastAsync($"{username} joined the chat.", session);

            while (true)
            {
                string message = (await Message.ReadMessageAsync(stream)).Trim();
                if (message.Length == 0)
                {
                    continue;
                }

                Console.WriteLine($"{username}: {message}");
                await BroadcastAsync($"{username}: {message}", null);
            }
        }
        catch (IOException)
        {
            // A closed socket is the normal way a client leaves.
        }
        catch (SocketException)
        {
            // The client can disappear between reads.
        }
        finally
        {
            if (session != null)
            {
                _sessions.TryRemove(session.Username, out _);
                Console.WriteLine($"{session.Username} disconnected.");
                await BroadcastAsync($"{session.Username} left the chat.", session);
            }

            client.Close();
        }
    }

    private async Task BroadcastAsync(string message, ClientSession? excludedSession)
    {
        foreach (ClientSession session in _sessions.Values)
        {
            if (session == excludedSession)
            {
                continue;
            }

            try
            {
                await session.SendAsync(message);
            }
            catch (IOException)
            {
                _sessions.TryRemove(session.Username, out _);
            }
            catch (SocketException)
            {
                _sessions.TryRemove(session.Username, out _);
            }
        }
    }

    public void Stop()
    {
        _listener.Stop();
        Console.WriteLine("Server stopped.");
    }
}
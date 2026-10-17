using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using ChatApplication.Common;

namespace ChatApplication.Server;

public class ChatServer
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
    private volatile bool _isRunning;

    public ChatServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public bool IsRunning => _isRunning;
    public IReadOnlyCollection<string> ConnectedUsernames => _sessions.Keys.OrderBy(username => username).ToArray();
    public event Action<string>? ActivityLogged;

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _listener.Start();
        }
        catch (SocketException exception)
        {
            Log($"Unable to start server: {exception.Message}");
            return;
        }

        _isRunning = true;

        Log("Server started.");
        Log("Waiting for clients...");

        try
        {
            while (_isRunning)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (ObjectDisposedException) when (!_isRunning)
                {
                    break;
                }
                catch (SocketException) when (!_isRunning)
                {
                    break;
                }
                catch (SocketException exception)
                {
                    Log($"Server accept error: {exception.Message}");
                    break;
                }

                _ = HandleClientAsync(client);
            }
        }
        finally
        {
            _isRunning = false;
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
            Log($"{username} connected.");
            await BroadcastAsync($"{username} joined the chat.", session);

            while (true)
            {
                string message = (await Message.ReadMessageAsync(stream)).Trim();
                if (message.Length == 0)
                {
                    continue;
                }

                Log($"{username}: {message}");
                await BroadcastAsync($"{username}: {message}", null);
            }
        }
        catch (IOException exception)
        {
            // A closed socket is the normal way a client leaves.
            Log($"Connection error for {session?.Username ?? "unknown client"}: {exception.Message}");
        }
        catch (SocketException exception)
        {
            // The client can disappear between reads.
            Log($"Socket error for {session?.Username ?? "unknown client"}: {exception.Message}");
        }
        finally
        {
            if (session != null)
            {
                _sessions.TryRemove(session.Username, out _);
                Log($"{session.Username} disconnected.");
                await BroadcastAsync($"{session.Username} left the chat.", session);
            }

            client.Close();
        }
    }

    public Task BroadcastAsync(string message)
    {
        return BroadcastAsync(message, null);
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
                Log($"Failed to send a broadcast to {session.Username}.");
            }
            catch (SocketException)
            {
                _sessions.TryRemove(session.Username, out _);
                Log($"Failed to send a broadcast to {session.Username}.");
            }
        }
    }

    public async Task<bool> SendPrivateMessageAsync(string username, string message)
    {
        if (!_sessions.TryGetValue(username, out ClientSession? session))
        {
            return false;
        }

        try
        {
            await session.SendAsync(message);
            return true;
        }
        catch (IOException exception)
        {
            Log($"Error sending a private message to {username}: {exception.Message}");
        }
        catch (SocketException exception)
        {
            Log($"Socket error sending a private message to {username}: {exception.Message}");
        }

        _sessions.TryRemove(username, out _);
        return false;
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _listener.Stop();
        Log("Server stopped.");
    }

    private void Log(string message)
    {
        ActivityLogged?.Invoke(message);
    }
}
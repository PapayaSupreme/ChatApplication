using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using ChatApplication.Common;

namespace ChatApplication.Server;

public class ChatServer
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
    private readonly ConcurrentDictionary<string, List<string>> _channelHistory = new();
    private readonly object _historyLock = new();
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
            session.Channel = ChatProtocol.BroadcastChannel;
            Log($"{username} connected.");
            await AddAndSendAsync(
                ChatProtocol.BroadcastChannel,
                $"{username} joined the chat.",
                session);
            await SendHistoryAsync(session, ChatProtocol.BroadcastChannel);

            while (true)
            {
                string command = (await Message.ReadMessageAsync(stream)).Trim();
                if (command.Length == 0)
                {
                    continue;
                }

                if (ChatProtocol.TryParseJoin(command, out string channel, out string? privateUsername))
                {
                    await SwitchChannelAsync(session, channel, privateUsername);
                    continue;
                }

                if (ChatProtocol.TryParseMessage(command, out string message))
                {
                    await SendToCurrentChannelAsync(session, message);
                    continue;
                }

                await session.SendAsync("ERROR Unknown command.");
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
                await AddAndSendAsync(
                    ChatProtocol.BroadcastChannel,
                    $"{session.Username} left the chat.",
                    session);
            }

            client.Close();
        }
    }

    public Task BroadcastAsync(string message)
    {
        return AddAndSendAsync(ChatProtocol.BroadcastChannel, message, null);
    }

    private async Task SwitchChannelAsync(
        ClientSession session,
        string channel,
        string? privateUsername)
    {
        if (channel == ChatProtocol.BroadcastChannel)
        {
            session.Channel = ChatProtocol.BroadcastChannel;
            await session.SendAsync("CHANNEL broadcast");
            await SendHistoryAsync(session, ChatProtocol.BroadcastChannel);
            return;
        }

        if (privateUsername is null ||
            !_sessions.ContainsKey(privateUsername) ||
            privateUsername.Equals(session.Username, StringComparison.OrdinalIgnoreCase))
        {
            await session.SendAsync("ERROR Private user is not connected.");
            return;
        }

        session.Channel = ChatProtocol.PrivateChannelKey(session.Username, privateUsername);
        await session.SendAsync($"CHANNEL private {privateUsername}");
        await SendHistoryAsync(session, session.Channel);
    }

    private async Task SendToCurrentChannelAsync(ClientSession sender, string message)
    {
        string formatted = $"{sender.Username}: {message}";
        Log(formatted);
        await AddAndSendAsync(sender.Channel, formatted, null);

        if (ChatProtocol.TryGetPrivateParticipants(
                sender.Channel,
                out string firstUsername,
                out string secondUsername))
        {
            string recipientUsername = firstUsername.Equals(
                sender.Username,
                StringComparison.OrdinalIgnoreCase)
                ? secondUsername
                : firstUsername;

            if (_sessions.TryGetValue(recipientUsername, out ClientSession? recipient) &&
                recipient.Channel.Equals(
                    ChatProtocol.BroadcastChannel,
                    StringComparison.OrdinalIgnoreCase))
            {
                await recipient.SendAsync(
                    ChatProtocol.PrivateMessageNotification(sender.Username));
            }
        }
    }

    private async Task AddAndSendAsync(
        string channel,
        string message,
        ClientSession? excludedSession)
    {
        lock (_historyLock)
        {
            if (!_channelHistory.TryGetValue(channel, out List<string>? history))
            {
                history = new List<string>();
                _channelHistory[channel] = history;
            }

            history.Add(message);
        }

        foreach (ClientSession session in _sessions.Values)
        {
            if (session == excludedSession ||
                !session.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
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
                Log($"Failed to send a channel message to {session.Username}.");
            }
            catch (SocketException)
            {
                _sessions.TryRemove(session.Username, out _);
                Log($"Failed to send a channel message to {session.Username}.");
            }
        }
    }

    private async Task SendHistoryAsync(ClientSession session, string channel)
    {
        List<string> history;
        lock (_historyLock)
        {
            history = _channelHistory.TryGetValue(channel, out List<string>? storedHistory)
                ? [.. storedHistory]
                : [];
        }

        foreach (string message in history)
        {
            await session.SendAsync(message);
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
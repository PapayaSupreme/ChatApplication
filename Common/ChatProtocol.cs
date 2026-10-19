namespace ChatApplication.Common;

public static class ChatProtocol
{
    public const string BroadcastChannel = "broadcast";

    public static string JoinBroadcastCommand => "JOIN broadcast";

    public static string JoinPrivateCommand(string username) =>
        $"JOIN private {username}";

    public static string MessageCommand(string message) =>
        $"MESSAGE {message}";

    public static bool TryParseJoin(
        string command,
        out string channel,
        out string? privateUsername)
    {
        string[] parts = command.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        channel = string.Empty;
        privateUsername = null;

        if (parts.Length == 2 && parts[0].Equals("JOIN", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals(BroadcastChannel, StringComparison.OrdinalIgnoreCase))
        {
            channel = BroadcastChannel;
            return true;
        }

        if (parts.Length == 3 && parts[0].Equals("JOIN", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals("private", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parts[2]))
        {
            channel = "private";
            privateUsername = parts[2].Trim();
            return true;
        }

        return false;
    }

    public static bool TryParseMessage(string command, out string message)
    {
        const string prefix = "MESSAGE ";
        if (command.StartsWith(prefix, StringComparison.Ordinal))
        {
            message = command[prefix.Length..].Trim();
            return message.Length > 0;
        }

        message = string.Empty;
        return false;
    }

    public static string PrivateChannelKey(string firstUsername, string secondUsername)
    {
        string[] usernames = [firstUsername, secondUsername];
        Array.Sort(usernames, StringComparer.OrdinalIgnoreCase);
        return $"private:{usernames[0]}:{usernames[1]}";
    }
}

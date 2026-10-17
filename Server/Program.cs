namespace ChatApplication.Server;

public class Program
{
    public static async Task Main()
    {
        ChatServer server = new ChatServer(5000);
        server.ActivityLogged += message =>
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");

        Task? serverTask = null;
        MessageSender broadcast = server.BroadcastAsync;

        Console.WriteLine("Chat server administration");
        Console.WriteLine("Commands: start, stop, clients, broadcast, private, exit");

        while (true)
        {
            Console.Write("> ");
            string? command = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (command)
            {
                case "start":
                    if (server.IsRunning)
                    {
                        Console.WriteLine("The server is already running.");
                        break;
                    }

                    serverTask = server.StartAsync();
                    Console.WriteLine("Server start requested.");
                    break;

                case "stop":
                    server.Stop();
                    if (serverTask is not null)
                    {
                        await serverTask;
                        serverTask = null;
                    }

                    break;

                case "clients":
                    IReadOnlyCollection<string> clients = server.ConnectedUsernames;
                    Console.WriteLine(clients.Count == 0
                        ? "No clients are connected."
                        : $"Connected clients ({clients.Count}): {string.Join(", ", clients)}");
                    break;

                case "broadcast":
                    Console.Write("Message: ");
                    string? broadcastMessage = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(broadcastMessage))
                    {
                        await broadcast(broadcastMessage);
                        Console.WriteLine("Broadcast sent.");
                    }

                    break;

                case "private":
                    Console.Write("Username: ");
                    string? username = Console.ReadLine()?.Trim();
                    Console.Write("Message: ");
                    string? privateMessage = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(username) ||
                        string.IsNullOrWhiteSpace(privateMessage))
                    {
                        Console.WriteLine("Username and message are required.");
                        break;
                    }

                    Console.WriteLine(await server.SendPrivateMessageAsync(username, privateMessage)
                        ? "Private message sent."
                        : "That user is not connected.");
                    break;

                case "exit":
                    server.Stop();
                    if (serverTask is not null)
                    {
                        await serverTask;
                    }

                    return;

                case "":
                    break;

                default:
                    Console.WriteLine("Unknown command. Use start, stop, clients, broadcast, private, or exit.");
                    break;
            }
        }
    }

    private delegate Task MessageSender(string message);
}
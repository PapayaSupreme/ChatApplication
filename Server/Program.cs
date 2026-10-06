namespace ChatApplication.Server;

public class Program
{
    public static async Task Main()
    {
        ChatServer server = new ChatServer(5000);

        await server.StartAsync();
    }
}
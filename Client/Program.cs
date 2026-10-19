using System.Net.Sockets;
using ChatApplication.Common;

namespace ChatApplication.Client;

public class Program
{
    public static async Task Main()
    {
        TcpClient client = new TcpClient();

        await client.ConnectAsync("127.0.0.1", 5000);

        Console.WriteLine("Connected to server.");

        NetworkStream stream = client.GetStream();

        string request = await Message.ReadMessageAsync(stream);

        if (request == "USERNAME")
        {
            Console.Write("Username: ");

            string? username = Console.ReadLine();

            await Message.SendMessageAsync(stream, username ?? "");
            Console.WriteLine("Chatting in broadcast. Use /private <username> or /broadcast to switch channels.");

            Task receiveTask = ReceiveMessagesAsync(stream);
            try
            {
                while (true)
                {
                    string? message = Console.ReadLine();
                    if (message == null)
                    {
                        break;
                    }

                    if (message.Equals("/broadcast", StringComparison.OrdinalIgnoreCase))
                    {
                        await Message.SendMessageAsync(stream, ChatProtocol.JoinBroadcastCommand);
                    }
                    else if (message.StartsWith("/private ", StringComparison.OrdinalIgnoreCase))
                    {
                        string privateUsername = message["/private ".Length..].Trim();
                        if (privateUsername.Length > 0)
                        {
                            await Message.SendMessageAsync(
                                stream,
                                ChatProtocol.JoinPrivateCommand(privateUsername));
                        }
                    }
                    else if (message.Length > 0)
                    {
                        await Message.SendMessageAsync(stream, ChatProtocol.MessageCommand(message));
                    }
                }
            }
            finally
            {
                client.Close();
                await receiveTask;
            }
        }
        else
        {
            Console.WriteLine(request);
            client.Close();
        }
    }

    private static async Task ReceiveMessagesAsync(NetworkStream stream)
    {
        try
        {
            while (true)
            {
                Console.WriteLine(await Message.ReadMessageAsync(stream));
            }
        }
        catch (IOException)
        {
            Console.WriteLine("Disconnected from server.");
        }
        catch (SocketException)
        {
            Console.WriteLine("Disconnected from server.");
        }
    }
}
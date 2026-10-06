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

                    if (message.Length > 0)
                    {
                        await Message.SendMessageAsync(stream, message);
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
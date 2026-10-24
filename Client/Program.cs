using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;
using ChatApplication.Common;

namespace ChatApplication.Client;

public class Program
{
    private static string _currentChannel = ChatProtocol.BroadcastChannel;

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
            Console.WriteLine(
                "Chatting in broadcast. Use /private <username>, /broadcast, or stock <ticker>.");

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
                    else if (message.Equals("stock", StringComparison.OrdinalIgnoreCase) ||
                             message.StartsWith("stock ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!StockPriceClient.TryParseCommand(message, out string ticker))
                        {
                            Console.WriteLine("Usage: stock <ticker> (for example, stock AAPL).");
                            continue;
                        }

                        if (!string.Equals(
                                Volatile.Read(ref _currentChannel),
                                ChatProtocol.BroadcastChannel,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("The stock command is only available in the broadcast channel.");
                            continue;
                        }

                        try
                        {
                            decimal price = await StockPriceClient.GetCurrentPriceAsync(ticker);
                            await Message.SendMessageAsync(
                                stream,
                                ChatProtocol.MessageCommand(
                                    $"Stock {ticker}: ${price.ToString("0.00", CultureInfo.InvariantCulture)}"));
                        }
                        catch (HttpRequestException exception)
                        {
                            Console.WriteLine($"Unable to fetch {ticker}: {exception.Message}");
                        }
                        catch (TaskCanceledException)
                        {
                            Console.WriteLine($"The request for {ticker} timed out.");
                        }
                        catch (JsonException)
                        {
                            Console.WriteLine($"The stock service returned an invalid value for {ticker}.");
                        }
                        catch (InvalidOperationException exception)
                        {
                            Console.WriteLine(exception.Message);
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
                string message = await Message.ReadMessageAsync(stream);
                if (ChatProtocol.TryParsePrivateMessageNotification(
                        message,
                        out string senderUsername))
                {
                    Console.WriteLine($"*** New private message from {senderUsername}. ***");
                    continue;
                }

                if (message.Equals("CHANNEL broadcast", StringComparison.OrdinalIgnoreCase))
                {
                    Volatile.Write(ref _currentChannel, ChatProtocol.BroadcastChannel);
                }
                else if (message.StartsWith("CHANNEL private ", StringComparison.OrdinalIgnoreCase))
                {
                    Volatile.Write(ref _currentChannel, "private");
                }

                Console.WriteLine(message);
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
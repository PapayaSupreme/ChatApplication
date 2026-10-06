using System.Net.Sockets;
using System.Text;

namespace ChatApplication.Common;

public static class Message
{
    public static async Task SendMessageAsync(
        NetworkStream stream,
        string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");

        await stream.WriteAsync(data);
    }

    public static async Task<string> ReadMessageAsync(
        NetworkStream stream)
    {
        List<byte> buffer = new();
        byte[] singleByte = new byte[1];

        while (true)
        {
            int bytesRead = await stream.ReadAsync(singleByte);

            if (bytesRead == 0)
            {
                throw new IOException("Connection closed.");
            }

            if (singleByte[0] == '\n')
            {
                break;
            }

            if (singleByte[0] != '\r')
            {
                buffer.Add(singleByte[0]);
            }
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
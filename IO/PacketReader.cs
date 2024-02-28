using System.Net.Sockets;
using System.Text;
using System.Transactions;
using Rinha2024.Dotnet;

namespace Rinha2024.VirtualDb;

public static class PacketReader
{
    public static async Task<int[]> ReadMessageAsync(this NetworkStream stream)
    {
        var receivedBuffer = new byte[8];
        var result = new int[2];
        _ = await stream.ReadAsync(receivedBuffer);
        for (var i = 0; i < 2; i++)
        {
            result[i] = BitConverter.ToInt32(receivedBuffer, i * 4);
        }
        return result;
    }
    
    
    public static async Task<(int[], List<TransactionDto>)> ReadMessageWithTransactionAsync(this NetworkStream stream)
    {
        var transactions = new List<TransactionDto>();
        var receivedBuffer = new byte[1024];
        var position = 0;
        
        var result = new int[2];
        _ = await stream.ReadAsync(receivedBuffer);
        for (var i = 0; i < 2; i++)
        {
            position = i * 4;
            result[i] = BitConverter.ToInt32(receivedBuffer, position);
        }
        position += 4;
        var size = BitConverter.ToInt32(receivedBuffer, position);
        position += 4;
        for (int i = 0; i < size; i++)
        {
            var value = BitConverter.ToInt32(receivedBuffer, position);
            position += 4;
            var type = BitConverter.ToChar(receivedBuffer, position);
            position += 2;
            var builder = new StringBuilder();
            for (var j = 0; j < 10; j++)
            {
                builder.Append(BitConverter.ToChar(receivedBuffer, position));
                position += 2;
            }
            var description = builder.ToString();
            builder.Clear();
            for (var j = 0; j < 19; j++)
            {
                builder.Append(BitConverter.ToChar(receivedBuffer, position));
                position += 2;
            }
            transactions.Add(new TransactionDto(value, type, description.Replace("_", ""), builder.ToString()));            
        }
        return (result, transactions);
    }
    
}
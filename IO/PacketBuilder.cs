namespace Rinha2024.Dotnet.IO;

public static class PacketBuilder
{
    
    public static async Task<byte[]> WriteMessage(int[] message)
    {
        await using var ms = new MemoryStream();
        foreach (var item in message)
        {
            await ms.WriteAsync(BitConverter.GetBytes(item));
        }
        return ms.ToArray();
    }
    
    public static async Task<byte[]> WriteMessage(int[] message, string description)
    {
        await using var ms = new MemoryStream();
        foreach (var item in message)
        {
            await ms.WriteAsync(BitConverter.GetBytes(item));
        }
        while(description.Length < 10)
        {
            description += '_';
        }
        foreach (var c in description)
        {
            await ms.WriteAsync(BitConverter.GetBytes(c));
        }
        return ms.ToArray();
    }
    
}
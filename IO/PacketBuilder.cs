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
    
}
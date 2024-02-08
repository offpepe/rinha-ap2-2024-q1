namespace Rinha2024.Dotnet.Extensions;

public static class TaskExtensions
{
    public static async void DoNotWait(this Task task)
    {
        await task.ConfigureAwait(false);
    } 

}
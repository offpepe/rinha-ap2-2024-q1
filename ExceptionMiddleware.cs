using System.Runtime.CompilerServices;
using System.Text;
using Rinha2024.Dotnet.Exceptions;

namespace Rinha2024.Dotnet;

public class ExceptionMiddleware : IMiddleware
{
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message, e.StackTrace);
            context.Response.StatusCode = e switch
            {
                NotFoundException notFound => (int) notFound.StatusCode,
                UnprocessableContentException unprocessableContentException => (int) unprocessableContentException
                    .StatusCode,
                _ => 500    
            };
        }
    }
}
using System.Net;

namespace Rinha2024.Dotnet.Exceptions;

public class UnprocessableContentException : Exception
{
    public HttpStatusCode StatusCode { get; } = HttpStatusCode.UnprocessableContent;
}
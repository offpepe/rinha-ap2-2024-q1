using System.Net;

namespace Rinha2024.Dotnet.Exceptions;

public class NotFoundException : Exception
{
    public HttpStatusCode StatusCode { get; } = HttpStatusCode.NotFound;
}
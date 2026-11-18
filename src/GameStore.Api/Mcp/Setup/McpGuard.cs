using GameStore.Api.Exceptions;
using ModelContextProtocol;

namespace GameStore.Api.Mcp.Setup;

/// <summary>
/// Your services throw AppException (NotFoundException, BadRequestException...) and the REST
/// pipeline turns them into ProblemDetails. MCP has no such pipeline: an exception that is not
/// an McpException reaches the client as a generic "an error occurred". This wrapper keeps the
/// useful message by converting AppException into McpException.
/// </summary>
internal static class McpGuard
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (AppException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}
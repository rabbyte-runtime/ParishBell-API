using Microsoft.AspNetCore.Mvc;
using ParishBell.Core.Exceptions;

namespace ParishBell.API.Controllers;

// NOTE: The try/catch every action runs inside, defined once instead of copied into each.
// IMPORTANT: Domain exceptions are rethrown untouched - they carry their own status and code.
// IMPORTANT: Swallowing them here would collapse every 404 and 422 into a 500.
// NOTE: What this adds is the failing action name and arguments in the log.
// NOTE: Middleware sitting outside routing cannot know either.
public abstract class ApiControllerBase(ILogger logger) : ControllerBase
{
    private readonly ILogger _logger = logger;

    protected async Task<IActionResult> ExecuteAsync(string operation, Func<Task<IActionResult>> handler, params object?[] context)
    {
        try
        {
            return await handler();
        }
        catch (ParishBellException)
        {
            // NOTE: An expected outcome - not found, forbidden, a failed validation. The middleware renders it.
            throw;
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // NOTE: The client hung up. Nothing failed, so this is not worth an error log.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed. Context: {@Context}", operation, context);
            throw;
        }
    }
}

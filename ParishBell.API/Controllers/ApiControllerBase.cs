using Microsoft.AspNetCore.Mvc;
using ParishBell.Core.Exceptions;

namespace ParishBell.API.Controllers;

// NOTE: The try/catch every action runs inside, defined once rather than copied into each one - same guarantee, and a
// NOTE:  single place to change how failures are treated.
// IMPORTANT: Domain exceptions are rethrown untouched. They already carry their status and PB code, and GlobalExceptionMiddleware
// IMPORTANT:  turns them into the coded envelope the clients parse - swallowing them here would collapse every 404 and 422 into a 500.
// NOTE: What this adds over the middleware is the failing action's name and arguments in the log, which a middleware
//       sitting outside routing cannot know.
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

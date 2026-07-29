namespace ManpowerAllocation.Application.Common;

/// <summary>
/// Base type for expected, business-level failures. The web layer maps these to
/// specific HTTP status codes while still returning a generic, non-revealing message
/// body to the client.
/// </summary>
public abstract class ApplicationException : Exception
{
    /// <summary>Initialises the exception with a human-readable, non-sensitive message.</summary>
    /// <param name="message">The message describing the failure.</param>
    protected ApplicationException(string message) : base(message)
    {
    }
}

/// <summary>Raised when a requested record does not exist.</summary>
public sealed class NotFoundException : ApplicationException
{
    /// <summary>Initialises the exception for a missing entity.</summary>
    /// <param name="entityName">The entity type that was not found.</param>
    /// <param name="key">The key that was searched for.</param>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
    }
}

/// <summary>Raised when a business rule is violated (for example, a duplicate department name).</summary>
public sealed class BusinessRuleException : ApplicationException
{
    /// <summary>Initialises the exception with the rule that was violated.</summary>
    /// <param name="message">A description of the violated rule.</param>
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>Raised when the current user is not permitted to perform an operation.</summary>
public sealed class ForbiddenException : ApplicationException
{
    /// <summary>Initialises the exception.</summary>
    /// <param name="message">A description of the missing permission.</param>
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}

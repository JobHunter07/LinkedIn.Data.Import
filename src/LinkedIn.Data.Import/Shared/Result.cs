namespace LinkedIn.Data.Import.Shared;

// ---------------------------------------------------------------------------
// ERROR-HANDLING RULE
// ---------------------------------------------------------------------------
// All known, foreseeable failure conditions MUST be communicated through the
// Result / Result<T> response pattern — never thrown as exceptions.
//
// The ONLY legitimate throws are:
//   • ArgumentNullException — when a required parameter (e.g. ImportOptions)
//     is null, indicating a programmer error.
//   • OperationCanceledException — surfaced by CancellationToken cancellation;
//     callers must handle this per standard .NET convention.
//
// All other failure scenarios must be wrapped with Result.Fail(ErrorCode, msg)
// and returned to the caller so that error-handling is explicit and
// compile-time visible, with no hidden exception types to catch.
// ---------------------------------------------------------------------------

/// <summary>
/// Generic response wrapper that encapsulates either a success value or a
/// known failure code with a descriptive message.
/// </summary>
/// <typeparam name="T">The success payload type.</typeparam>
public sealed class Result<T>
{
    private Result() { }

    /// <summary>Whether this result represents a successful operation.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// The success payload. Only valid when <see cref="IsSuccess"/> is
    /// <see langword="true"/>; accessing this on a failure result throws.
    /// </summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw new InvalidOperationException(
                    "Cannot access Value on a failed Result.");
            return _value!;
        }
    }

    private T? _value;

    /// <summary>The error code describing the failure. Only meaningful when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public ErrorCode ErrorCode { get; private init; }

    /// <summary>A human-readable description of the failure.</summary>
    public string ErrorMessage { get; private init; } = string.Empty;

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Ok(T value) =>
        new() { IsSuccess = true, _value = value };

    /// <summary>Creates a failure result with the given <paramref name="code"/> and <paramref name="message"/>.</summary>
    public static Result<T> Fail(ErrorCode code, string message) =>
        new() { IsSuccess = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// Non-generic response wrapper for operations that do not return a payload
/// on success.
/// </summary>
public sealed class Result
{
    private Result() { }

    /// <summary>Whether this result represents a successful operation.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>The error code describing the failure. Only meaningful when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public ErrorCode ErrorCode { get; private init; }

    /// <summary>A human-readable description of the failure.</summary>
    public string ErrorMessage { get; private init; } = string.Empty;

    /// <summary>Creates a successful result.</summary>
    public static Result Ok() => new() { IsSuccess = true };

    /// <summary>Creates a failure result with the given <paramref name="code"/> and <paramref name="message"/>.</summary>
    public static Result Fail(ErrorCode code, string message) =>
        new() { IsSuccess = false, ErrorCode = code, ErrorMessage = message };
}

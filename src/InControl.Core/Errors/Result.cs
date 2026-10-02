using System.Diagnostics.CodeAnalysis;

namespace InControl.Core.Errors;

/// <summary>
/// Represents a void/unit value for Result types that don't return data.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The singleton Unit value.
    /// </summary>
    public static Unit Value { get; } = new();

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Use this instead of exceptions for expected failure cases.
/// </summary>
public readonly struct Result
{
    private readonly InControlError? _error;

    private Result(InControlError? error)
    {
        _error = error;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => _error is null;

    /// <summary>
    /// Whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => _error is not null;

    /// <summary>
    /// The error if the operation failed; null otherwise.
    /// </summary>
    public InControlError? Error => _error;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(InControlError error) => new(error);

    /// <summary>
    /// Creates a failed result with the specified error code and message.
    /// </summary>
    public static Result Failure(ErrorCode code, string message) =>
        new(InControlError.Create(code, message));

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static Result FromException(Exception exception, ErrorCode? code = null) =>
        new(InControlError.FromException(exception, code));

    /// <summary>
    /// Implicit conversion from InControlError to failed Result.
    /// </summary>
    public static implicit operator Result(InControlError error) => Failure(error);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess) action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<InControlError> action)
    {
        if (IsFailure) action(Error);
        return this;
    }

    /// <summary>
    /// Throws an exception if the result is a failure.
    /// </summary>
    public void ThrowIfFailure()
    {
        if (IsFailure)
        {
            throw new InvalidOperationException(Error.Message);
        }
    }
}

/// <summary>
/// Represents the result of an operation that returns a value or fails.
/// Use this instead of exceptions for expected failure cases.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly InControlError? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(InControlError error)
    {
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    /// <remarks>
    /// For reference-type <typeparamref name="T"/>, the compiler narrows <see cref="Value"/> to non-null after a <c>true</c> check.
    /// For value-type <typeparamref name="T"/>, use <see cref="Unwrap"/> to obtain the non-nullable value.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => _error is null;

    /// <summary>
    /// Whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => _error is not null;

    /// <summary>
    /// The value if the operation succeeded.
    /// </summary>
    public T? Value => _value;

    /// <summary>
    /// The error if the operation failed; null otherwise.
    /// </summary>
    public InControlError? Error => _error;

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result<T> Failure(InControlError error) => new(error);

    /// <summary>
    /// Creates a failed result with the specified error code and message.
    /// </summary>
    public static Result<T> Failure(ErrorCode code, string message) =>
        new(InControlError.Create(code, message));

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static Result<T> FromException(Exception exception, ErrorCode? code = null) =>
        new(InControlError.FromException(exception, code));

    /// <summary>
    /// Implicit conversion from T to successful Result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicit conversion from InControlError to failed Result.
    /// </summary>
    public static implicit operator Result<T>(InControlError error) => Failure(error);

    /// <summary>
    /// Converts to a non-generic Result (discards the value).
    /// </summary>
    public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(Error);

    /// <summary>
    /// Gets the value or throws if failed.
    /// </summary>
    public T GetValueOrThrow()
    {
        if (IsFailure)
        {
            throw new InvalidOperationException(Error.Message);
        }
        return Value;
    }

    /// <summary>
    /// Gets the success value, throwing <see cref="InvalidOperationException"/> if the result is a failure.
    /// Equivalent to <see cref="GetValueOrThrow"/> but shorter — preferred for value-type <typeparamref name="T"/>
    /// where <see cref="System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute"/> cannot narrow <see cref="Value"/> from <c>T?</c> to <c>T</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Unwrap()
    {
        if (IsFailure)
        {
            throw new InvalidOperationException(Error.Message);
        }
        return _value!;
    }

    /// <summary>
    /// Gets the value or returns the specified default.
    /// </summary>
    public T GetValueOrDefault(T defaultValue) => IsSuccess ? Value : defaultValue;

    /// <summary>
    /// Gets the value or returns the result of the factory function.
    /// </summary>
    public T GetValueOrDefault(Func<InControlError, T> factory) =>
        IsSuccess ? Value : factory(Error!);

    /// <summary>
    /// Transforms the value if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Success(mapper(Value)) : Result<TNew>.Failure(Error);

    /// <summary>
    /// Transforms the value if successful, or returns a new Result.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(Value) : Result<TNew>.Failure(Error);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess) action(Value);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<InControlError> action)
    {
        if (IsFailure) action(Error);
        return this;
    }

    /// <summary>
    /// Pattern matching: executes one of two functions based on success/failure.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<InControlError, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}

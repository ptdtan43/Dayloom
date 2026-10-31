namespace Dayloom.Application.Common.Results;

/// <summary>Either a value or an <see cref="Results.Error"/>, never both.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error) => Error = error;

    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    // Lets a handler simply "return value;" or "return error;".
    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}

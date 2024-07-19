namespace StoreListings.Library;

public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Exception? _exception;

    public bool IsSuccess { get; }

    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw _exception!;
            return _value!;
        }
    }

    public Exception Exception
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("The operation was successful.");
            return _exception!;
        }
    }

    private Result(T value)
    {
        _value = value;
        _exception = null;
        IsSuccess = true;
    }

    private Result(Exception exception)
    {
        _value = default;
        _exception = exception;
        IsSuccess = false;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Exception exception) => new(exception);
}
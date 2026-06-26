namespace Application;

public interface IAsyncContext<T>
    where T : class
{
    T? Value { get; set; }
}

public class AsyncContext<T> : IAsyncContext<T>
    where T : class
{
    private readonly AsyncLocal<T?> _asyncLocal = new();

    public T? Value
    {
        get => _asyncLocal.Value;
        set => _asyncLocal.Value = value;
    }
}

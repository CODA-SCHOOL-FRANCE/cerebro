namespace Cerebro.Shared;

public static class FunctionalExtensions
{
    public static TResult Map<T, TResult>(this T obj, Func<T, TResult> func)
        => obj == null
            ? throw new ArgumentNullException(nameof(obj))
            : func(obj);
}
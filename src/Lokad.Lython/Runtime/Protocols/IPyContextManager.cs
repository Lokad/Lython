namespace Lokad.Lython.Runtime;

internal interface IPyContextManager
{
    object Enter();

    bool Exit(object exceptionType, object exceptionValue, object traceback);
}

internal interface IPyAsyncContextManager : IPyContextManager
{
    ValueTask<object> EnterAsync();

    ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback);
}

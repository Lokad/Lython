namespace Lokad.Lython.Runtime;

internal interface IPyContextManager
{
    object Enter();

    bool Exit(object exceptionType, object exceptionValue, object traceback);
}

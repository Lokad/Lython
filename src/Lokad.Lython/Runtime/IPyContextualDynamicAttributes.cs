namespace Lokad.Lython.Runtime;

internal interface IPyContextualDynamicAttributes
{
    bool TryGetMember(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value);
}

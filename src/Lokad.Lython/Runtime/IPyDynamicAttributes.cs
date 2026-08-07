namespace Lokad.Lython.Runtime;

internal interface IPyDynamicAttributes
{
    bool TryGetMember(string name, [MaybeNullWhen(false)] out object value);

    bool TrySetMember(string name, object value);
}

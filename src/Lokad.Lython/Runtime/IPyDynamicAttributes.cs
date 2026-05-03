namespace Lokad.Lython.Runtime;

internal interface IPyDynamicAttributes
{
    bool TryGetMember(string name, out object value);

    bool TrySetMember(string name, object value);
}

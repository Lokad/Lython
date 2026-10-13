namespace Lokad.Lython.Runtime;

// Native iterators with guest callbacks use Python special-method invocation,
// which supplies the caller, instead of the context-free intrinsic pull API.
internal interface IPyProtocolIterator : IPyDynamicAttributes
{
}

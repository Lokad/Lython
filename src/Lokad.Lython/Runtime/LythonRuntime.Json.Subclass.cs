using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // JSON subclasses use ordinary Python attributes and binding. Only decoder
    // scanner snapshots need auxiliary native state.
    internal static class JsonSubclassSupport
    {
        private static readonly ConditionalWeakTable<PyInstance, JsonDecoderObject> Decoders = new();

        internal static bool HasGuestInit(PyType type, ExecutionContext context)
        {
            if (!context.TryGetBuiltin("object", out var root) || root is not PyType objectRoot)
            {
                return true;
            }

            foreach (var member in type.Mro)
            {
                if (ReferenceEquals(member, objectRoot))
                {
                    continue;
                }

                if (member.TryGetOwnMember("__init__", out _))
                {
                    return true;
                }
            }

            return false;
        }

        // Runs the inherited engine constructor when a JSON subclass defines
        // no __init__ of its own, exactly where CPython would dispatch to the
        // base __init__ through the MRO.
        internal static void RunEngineInit(PyType type, PyInstance instance, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (type.JsonBase == JsonBaseKind.Encoder)
            {
                _ = JsonEncoderClass.ConstructAsync(arguments, instance, span, context, false).GetAwaiter().GetResult();
            }
            else
            {
                AttachDecoderPeer(instance, JsonDecoderClass.BindDecoderOptions(arguments, span, context));
            }
        }

        internal static async ValueTask RunEngineInitAsync(PyType type, PyInstance instance, CallArgumentValue[] arguments,
            LythonSourceSpan span, ExecutionContext context)
        {
            if (type.JsonBase == JsonBaseKind.Encoder)
                _ = await JsonEncoderClass.ConstructAsync(arguments, instance, span, context, true).ConfigureAwait(false);
            else AttachDecoderPeer(instance, await JsonDecoderClass.BindDecoderOptionsAsync(arguments, span, context).ConfigureAwait(false));
        }

        internal static void AttachDecoderPeer(PyInstance instance, JsonDecoderObject peer)
        {
            Decoders.Remove(instance);
            Decoders.Add(instance, peer);
            foreach (var pair in peer.Attributes) instance.SetAttribute(pair.Key, pair.Value);
        }

        internal static bool TryGetDecoderPeer(PyInstance instance, [MaybeNullWhen(false)] out JsonDecoderObject peer)
        {
            if (Decoders.TryGetValue(instance, out var typed))
            {
                peer = typed;
                return true;
            }

            peer = null;
            return false;
        }

        // Encoder methods bind directly to ordinary guest state. Decoder peers
        // retain the constructor-captured scanner independently of later writes.
        internal static bool TryGetEngineMember(PyInstance instance, string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = PyNone.Instance;
            if (instance.Type.JsonBase == JsonBaseKind.Encoder)
            {
                return JsonEncoderMembers.TryGetMember(new JsonEncoderObject(instance), memberName, out value);
            }

            if (instance.Type.JsonBase == JsonBaseKind.Decoder)
            {
                if (!TryGetDecoderPeer(instance, out var decoder))
                {
                    throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context);
                }

                return memberName is "decode" or "raw_decode" && JsonDecoderMembers.TryGetMember(decoder, memberName, out value);
            }

            return false;
        }

        internal static bool TryGetSuperEngineMember(PySuper superObject, string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = PyNone.Instance;
            if (superObject.BoundType is not PyType boundType || boundType.JsonBase == JsonBaseKind.None)
            {
                return false;
            }

            if (superObject.BoundObject is not PyInstance instance)
            {
                return false;
            }

            if (memberName == "__init__")
            {
                value = new JsonSuperInitCallable(instance, boundType.JsonBase);
                return true;
            }

            // Only methods delegate through super: option attributes live on
            // instances in CPython, so super() must miss them like it does.
            if (boundType.JsonBase == JsonBaseKind.Encoder)
            {
                return memberName is "encode" or "iterencode" or "default" &&
                    JsonEncoderMembers.TryGetMember(new JsonEncoderObject(instance), memberName, out value);
            }

            if (!TryGetDecoderPeer(instance, out var decoder))
            {
                throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context);
            }

            if (!JsonDecoderMembers.TryGetMember(decoder, memberName, out var decoderMember) || decoderMember is not ICallable)
            {
                return false;
            }

            value = decoderMember;
            return true;
        }

        // Binds super().__init__() to the engine constructor for the bound
        // instance, preserving ordinary initialization effects and scanner state.
        private sealed class JsonSuperInitCallable : ICallable
        {
            private readonly PyInstance _instance;
            private readonly JsonBaseKind _kind;

            public JsonSuperInitCallable(PyInstance instance, JsonBaseKind kind)
            {
                _instance = instance;
                _kind = kind;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (_kind == JsonBaseKind.Encoder)
                {
                    _ = JsonEncoderClass.ConstructAsync(arguments, _instance, span, context, false).GetAwaiter().GetResult();
                }
                else
                {
                    AttachDecoderPeer(_instance, JsonDecoderClass.BindDecoderOptions(arguments, span, context));
                }

                return PyNone.Instance;
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                if (_kind == JsonBaseKind.Encoder)
                {
                    _ = await JsonEncoderClass.ConstructAsync(arguments, _instance, span, context, true).ConfigureAwait(false);
                    return PyNone.Instance;
                }
                AttachDecoderPeer(_instance, await JsonDecoderClass.BindDecoderOptionsAsync(arguments, span, context).ConfigureAwait(false));
                return PyNone.Instance;
            }
        }
    }
}

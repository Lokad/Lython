using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Guest subclasses of json.JSONEncoder/JSONDecoder (N39). Instances stay
    // ordinary PyInstance values using the guest class machinery (attribute
    // lookup, super(), isinstance), while the engine peer below carries the
    // parse/emit options. Peers die with their instances through the table,
    // so no instance layout changes are needed.
    internal static class JsonSubclassSupport
    {
        private static readonly ConditionalWeakTable<PyInstance, object> Peers = new();

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
                AttachEncoderPeer(instance, JsonEncoderClass.BindEncoderOptions(arguments, span, context));
            }
            else
            {
                AttachDecoderPeer(instance, JsonDecoderClass.BindDecoderOptions(arguments, span, context));
            }
        }

        internal static void AttachDecoderPeer(PyInstance instance, JsonDecoderObject peer)
        {
            Peers.Remove(instance);
            Peers.Add(instance, peer);
            foreach (var pair in peer.Attributes) instance.SetAttribute(pair.Key, pair.Value);
        }

        internal static void AttachEncoderPeer(PyInstance instance, JsonEncoderObject peer)
        {
            peer.GuestOwner = instance;
            Peers.Remove(instance);
            Peers.Add(instance, peer);
            foreach (var name in new[] { "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "sort_keys", "indent", "item_separator", "key_separator" })
            {
                if (JsonEncoderMembers.TryGetMember(peer, name, out var value)) instance.SetAttribute(name, value);
            }
            if (peer.DefaultHook is not null) instance.SetAttribute("default", peer.DefaultHook);
        }

        internal static bool TryGetEncoderPeer(PyInstance instance, [MaybeNullWhen(false)] out JsonEncoderObject peer)
        {
            if (Peers.TryGetValue(instance, out var raw) && raw is JsonEncoderObject typed)
            {
                peer = typed;
                return true;
            }

            peer = null;
            return false;
        }

        internal static bool TryGetDecoderPeer(PyInstance instance, [MaybeNullWhen(false)] out JsonDecoderObject peer)
        {
            if (Peers.TryGetValue(instance, out var raw) && raw is JsonDecoderObject typed)
            {
                peer = typed;
                return true;
            }

            peer = null;
            return false;
        }

        // Resolves engine members where a base-class entry would sit: after
        // instance and guest-class lookup, so overrides and writes win. Peer
        // construction always precedes use; a missing peer means guest
        // __init__ skipped its super() call, which CPython also rejects.
        internal static bool TryGetEngineMember(PyInstance instance, string memberName, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = PyNone.Instance;
            if (instance.Type.JsonBase == JsonBaseKind.Encoder)
            {
                if (!TryGetEncoderPeer(instance, out var encoder))
                {
                    throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context);
                }

                return memberName is "encode" or "iterencode" or "default" && JsonEncoderMembers.TryGetMember(encoder, memberName, out value);
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
                if (!TryGetEncoderPeer(instance, out var encoder))
                {
                    throw PyMemberAccess.CreateMissingMemberError(instance, memberName, span, context);
                }

                if (memberName == "default")
                {
                    value = BoundCallable.Create((arguments, callSpan, callContext) => encoder.Default(arguments, callSpan, callContext), LythonKnownCallableSignatures.JsonEncoderDefault);
                    return true;
                }

                if (!JsonEncoderMembers.TryGetMember(encoder, memberName, out var member) || member is not ICallable)
                {
                    return false;
                }

                value = member;
                return true;
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
        // instance: validates like a direct construction, then (re)builds
        // the peer instead of returning a standalone engine object.
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
                    AttachEncoderPeer(_instance, JsonEncoderClass.BindEncoderOptions(arguments, span, context));
                }
                else
                {
                    AttachDecoderPeer(_instance, JsonDecoderClass.BindDecoderOptions(arguments, span, context));
                }

                return PyNone.Instance;
            }
        }
    }
}

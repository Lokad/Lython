using System.Numerics;
using System.Security.Cryptography;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum HashlibAlgorithm
    {
        Md5,
        Sha1,
        Sha256,
        Sha384,
        Sha512,
    }

    private sealed class HashlibModule : PyModule
    {
        public static readonly HashlibModule Instance = new();

        private static readonly string[] AlgorithmNames = ["md5", "sha1", "sha256", "sha384", "sha512"];
        private static readonly string[] Members =
        [
            "md5",
            "sha1",
            "sha256",
            "sha384",
            "sha512",
            "new",
            "algorithms_available",
            "algorithms_guaranteed",
            "file_digest",
        ];

        private HashlibModule() : base("hashlib")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "md5" => new HashlibCallable("hashlib.md5", HashlibAlgorithm.Md5),
                "sha1" => new HashlibCallable("hashlib.sha1", HashlibAlgorithm.Sha1),
                "sha256" => new HashlibCallable("hashlib.sha256", HashlibAlgorithm.Sha256),
                "sha384" => new HashlibCallable("hashlib.sha384", HashlibAlgorithm.Sha384),
                "sha512" => new HashlibCallable("hashlib.sha512", HashlibAlgorithm.Sha512),
                "new" => new HashlibCallable(),
                "algorithms_available" => CreateAlgorithmSet(),
                "algorithms_guaranteed" => CreateAlgorithmSet(),
                "file_digest" => new BuiltinCallable(
                    LythonKnownCallableSignatures.HashlibFileDigest,
                    (_, span, _) => throw new LythonRuntimeException(
                        "NotImplementedError",
                        "hashlib.file_digest() is unsupported because Lython does not expose generic binary file handles; use in-memory bytes or a contained high-level file API.",
                        span)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PySet CreateAlgorithmSet()
            => new(AlgorithmNames.Select(PyString.FromString));

        public static PyBytes RequireHashBytes(object value, string owner, LythonSourceSpan span)
        {
            if (value is not PyBytes bytes)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}() requires a bytes-like object", span);
            }

            return bytes;
        }
    }

    private sealed class HashlibCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        private readonly HashlibAlgorithm? _algorithm;

        public HashlibCallable(string name, HashlibAlgorithm algorithm)
        {
            Name = name;
            _algorithm = algorithm;
        }

        public HashlibCallable()
        {
            Name = "hashlib.new";
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _algorithm is { } algorithm
                ? InvokeConstructor(arguments, algorithm, span, context)
                : InvokeNew(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        private object InvokeConstructor(
            CallArgumentValue[] arguments,
            HashlibAlgorithm algorithm,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            object? data = null;
            var dataAssigned = false;
            var usedForSecurityAssigned = false;
            var positionalCount = 0;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    if (positionalCount++ >= 1 || dataAssigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{Name}() accepts at most one positional argument", span);
                    }

                    data = argument.Value;
                    dataAssigned = true;
                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "string" when !dataAssigned:
                        data = argument.Value;
                        dataAssigned = true;
                        break;
                    case "usedforsecurity" when !usedForSecurityAssigned:
                        usedForSecurityAssigned = true;
                        break;
                    case "string":
                    case "usedforsecurity":
                        throw new LythonRuntimeException("TypeError", $"{Name}() got multiple values for argument '{argument.KeywordName}'", span);
                    default:
                        throw new LythonRuntimeException("TypeError", $"{Name}() got an unexpected keyword argument '{argument.KeywordName}'", span);
                }
            }

            if (dataAssigned)
            {
                var initial = HashlibModule.RequireHashBytes(data.RequireNotNull(), Name, span);
                return new HashlibHashObject(algorithm, initial.Bytes, context.MemoryGovernor, span);
            }

            return new HashlibHashObject(algorithm, ReadOnlySpan<byte>.Empty, context.MemoryGovernor, span);
        }

        private static object InvokeNew(
            CallArgumentValue[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            object? name = null;
            object? data = null;
            var nameAssigned = false;
            var dataAssigned = false;
            var usedForSecurityAssigned = false;
            var positionalCount = 0;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    switch (positionalCount++)
                    {
                        case 0 when !nameAssigned:
                            name = argument.Value;
                            nameAssigned = true;
                            break;
                        case 1 when !dataAssigned:
                            data = argument.Value;
                            dataAssigned = true;
                            break;
                        default:
                            throw new LythonRuntimeException("TypeError", "hashlib.new() accepts at most two positional arguments", span);
                    }

                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "name" when !nameAssigned:
                        name = argument.Value;
                        nameAssigned = true;
                        break;
                    case "data" when !dataAssigned:
                        data = argument.Value;
                        dataAssigned = true;
                        break;
                    case "usedforsecurity" when !usedForSecurityAssigned:
                        usedForSecurityAssigned = true;
                        break;
                    case "name":
                    case "data":
                    case "usedforsecurity":
                        throw new LythonRuntimeException("TypeError", $"hashlib.new() got multiple values for argument '{argument.KeywordName}'", span);
                    default:
                        throw new LythonRuntimeException("TypeError", $"hashlib.new() got an unexpected keyword argument '{argument.KeywordName}'", span);
                }
            }

            if (!nameAssigned)
            {
                throw new LythonRuntimeException("TypeError", "hashlib.new() missing required argument 'name'", span);
            }

            if (!PyStringOps.TryAsString(name.RequireNotNull(), out var nameText))
            {
                throw new LythonRuntimeException("TypeError", "hashlib.new(name[, data]) expects name to be a string.", span);
            }

            if (!TryParseHashAlgorithm(nameText.AsString(), out var algorithm))
            {
                throw new LythonRuntimeException("ValueError", $"unsupported hash type {nameText.AsString()}", span);
            }

            if (dataAssigned)
            {
                var initial = HashlibModule.RequireHashBytes(data.RequireNotNull(), "hashlib.new", span);
                return new HashlibHashObject(algorithm, initial.Bytes, context.MemoryGovernor, span);
            }

            return new HashlibHashObject(algorithm, ReadOnlySpan<byte>.Empty, context.MemoryGovernor, span);
        }
    }

    private sealed class HashlibHashObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private const long ObjectOverhead = 96;

        private readonly HashlibAlgorithm _algorithm;
        private readonly MemoryGovernor _governor;
        private readonly IncrementalHash _hash;

        public HashlibHashObject(
            HashlibAlgorithm algorithm,
            ReadOnlySpan<byte> input,
            MemoryGovernor governor,
            LythonSourceSpan? allocationSpan)
        {
            _algorithm = algorithm;
            _governor = governor;
            governor.Reserve(ObjectOverhead, allocationSpan);
            _hash = IncrementalHash.CreateHash(HashAlgorithmName);
            _hash.AppendData(input);
            governor.Commit(ObjectOverhead);
        }

        private HashlibHashObject(
            HashlibAlgorithm algorithm,
            IncrementalHash hash,
            MemoryGovernor governor)
        {
            _algorithm = algorithm;
            _hash = hash;
            _governor = governor;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "update" => new BoundCallable(Update, $"{AlgorithmName}.update", ["data"]),
                "digest" => new BoundCallable(Digest, $"{AlgorithmName}.digest", []),
                "hexdigest" => new BoundCallable(HexDigest, $"{AlgorithmName}.hexdigest", []),
                "copy" => new BoundCallable(Copy, $"{AlgorithmName}.copy", []),
                "name" => PyString.FromString(AlgorithmName),
                "digest_size" => new BigInteger(DigestSize),
                "block_size" => new BigInteger(BlockSize),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<{AlgorithmName} _hashlib.HASH object>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Update(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0] is not PyBytes bytes)
            {
                throw new LythonRuntimeException("TypeError", "hash.update(data) requires a bytes-like object", span);
            }

            _hash.AppendData(bytes.Bytes);
            return PyNone.Instance;
        }

        private object Digest(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            return CreateBytes(ComputeHash(), context, span);
        }

        private object HexDigest(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            return CreateString(Convert.ToHexString(ComputeHash()).ToLowerInvariant(), context, span);
        }

        private object Copy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            _governor.Reserve(ObjectOverhead, span);
            var clone = _hash.Clone();
            _governor.Commit(ObjectOverhead);
            return new HashlibHashObject(_algorithm, clone, _governor);
        }

        private byte[] ComputeHash() => _hash.GetCurrentHash();

        private HashAlgorithmName HashAlgorithmName => _algorithm switch
        {
            HashlibAlgorithm.Md5 => System.Security.Cryptography.HashAlgorithmName.MD5,
            HashlibAlgorithm.Sha1 => System.Security.Cryptography.HashAlgorithmName.SHA1,
            HashlibAlgorithm.Sha256 => System.Security.Cryptography.HashAlgorithmName.SHA256,
            HashlibAlgorithm.Sha384 => System.Security.Cryptography.HashAlgorithmName.SHA384,
            HashlibAlgorithm.Sha512 => System.Security.Cryptography.HashAlgorithmName.SHA512,
            _ => throw new InvalidOperationException("Unknown hash algorithm."),
        };

        private string AlgorithmName => _algorithm switch
        {
            HashlibAlgorithm.Md5 => "md5",
            HashlibAlgorithm.Sha1 => "sha1",
            HashlibAlgorithm.Sha256 => "sha256",
            HashlibAlgorithm.Sha384 => "sha384",
            HashlibAlgorithm.Sha512 => "sha512",
            _ => throw new InvalidOperationException("Unknown hash algorithm."),
        };

        private int DigestSize => _algorithm switch
        {
            HashlibAlgorithm.Md5 => 16,
            HashlibAlgorithm.Sha1 => 20,
            HashlibAlgorithm.Sha256 => 32,
            HashlibAlgorithm.Sha384 => 48,
            HashlibAlgorithm.Sha512 => 64,
            _ => throw new InvalidOperationException("Unknown hash algorithm."),
        };

        private int BlockSize => _algorithm is HashlibAlgorithm.Sha384 or HashlibAlgorithm.Sha512 ? 128 : 64;
    }

    private static bool TryParseHashAlgorithm(string name, out HashlibAlgorithm algorithm)
    {
        var normalized = name.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        algorithm = normalized switch
        {
            "md5" => HashlibAlgorithm.Md5,
            "sha1" => HashlibAlgorithm.Sha1,
            "sha256" => HashlibAlgorithm.Sha256,
            "sha384" => HashlibAlgorithm.Sha384,
            "sha512" => HashlibAlgorithm.Sha512,
            _ => default,
        };

        return normalized is
            "md5" or "sha1" or "sha256" or "sha384" or "sha512";
    }
}

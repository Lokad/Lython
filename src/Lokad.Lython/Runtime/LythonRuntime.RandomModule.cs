using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class RandomModule : PyModule
    {
        private const double TwoPi = Math.PI * 2.0;
        private const string StateTag = "lython.random.state";
        // Fixed getstate tag: share one constant forever so per-call state
        // tags alias stably instead of allocating a fresh string per call.
        private static readonly PyString SharedStateTag = PyString.FromString(StateTag);
        private static readonly BigInteger MaxUInt64 = new(ulong.MaxValue);

        public static readonly PyBuiltinRuntimeType RandomType = new("random.Random", CreateRandom);

        private readonly PyRandomState _state;

        public RandomModule(PyRandomState state) : base("random")
        {
            _state = state;
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Random" => RandomType,
                "SystemRandom" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.SystemRandom, UnsupportedSystemRandom),
                "BPF" => new BigInteger(53),
                "RECIP_BPF" => 1.0 / (1UL << 53),
                "seed" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Seed, (arguments, span, context) => Seed(_state, arguments, span, context)),
                "random" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.RandomRandom, (arguments, span, context) => Random(_state, arguments, span, context)),
                "getstate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.GetState, (arguments, span, context) => GetState(_state, arguments, span, context)),
                "setstate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.SetState, (arguments, span, context) => SetState(_state, arguments, span, context)),
                "randrange" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.RandRange, (arguments, span, context) => RandRange(_state, arguments, span, context), (arguments, span, context) => RandRangeAsync(_state, arguments, span, context)),
                "randint" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.RandInt, (arguments, span, context) => RandInt(_state, arguments, span, context)),
                "choice" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Choice, (arguments, span, context) => Choice(_state, arguments, span, context)),
                "choices" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Choices, (arguments, span, context) => Choices(_state, arguments, span, context)),
                "shuffle" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Shuffle, (arguments, span, context) => Shuffle(_state, arguments, span, context)),
                "sample" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Sample, (arguments, span, context) => Sample(_state, arguments, span, context)),
                "getrandbits" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.GetRandBits, (arguments, span, context) => GetRandBits(_state, arguments, span, context), (arguments, span, context) => GetRandBitsAsync(_state, arguments, span, context)),
                "randbytes" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.RandBytes, (arguments, span, context) => RandBytes(_state, arguments, span, context), (arguments, span, context) => RandBytesAsync(_state, arguments, span, context)),
                "uniform" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Uniform, (arguments, span, context) => Uniform(_state, arguments, span, context)),
                "triangular" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Triangular, (arguments, span, context) => Triangular(_state, arguments, span, context)),
                "betavariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.BetaVariate, (arguments, span, context) => BetaVariate(_state, arguments, span, context)),
                "expovariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.ExpVariate, (arguments, span, context) => ExpVariate(_state, arguments, span, context)),
                "gammavariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.GammaVariate, (arguments, span, context) => GammaVariate(_state, arguments, span, context)),
                "gauss" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.Gauss, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.gauss")),
                "normalvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.NormalVariate, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.normalvariate")),
                "lognormvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.LogNormVariate, (arguments, span, context) => LogNormVariate(_state, arguments, span, context)),
                "paretovariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.ParetoVariate, (arguments, span, context) => ParetoVariate(_state, arguments, span, context)),
                "vonmisesvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.VonMisesVariate, (arguments, span, context) => VonMisesVariate(_state, arguments, span, context)),
                "weibullvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.Random.WeibullVariate, (arguments, span, context) => WeibullVariate(_state, arguments, span, context)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}

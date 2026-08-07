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
                "SystemRandom" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSystemRandom, UnsupportedSystemRandom),
                "BPF" => new BigInteger(53),
                "RECIP_BPF" => 1.0 / (1UL << 53),
                "seed" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSeed, (arguments, span, context) => Seed(_state, arguments, span, context)),
                "random" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandom, (arguments, span, context) => Random(_state, arguments, span, context)),
                "getstate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGetState, (arguments, span, context) => GetState(_state, arguments, span, context)),
                "setstate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSetState, (arguments, span, context) => SetState(_state, arguments, span, context)),
                "randrange" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandRange, (arguments, span, context) => RandRange(_state, arguments, span, context)),
                "randint" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandInt, (arguments, span, context) => RandInt(_state, arguments, span, context)),
                "choice" => new BuiltinCallable(LythonKnownCallableSignatures.RandomChoice, (arguments, span, context) => Choice(_state, arguments, span, context)),
                "choices" => new BuiltinCallable(LythonKnownCallableSignatures.RandomChoices, (arguments, span, context) => Choices(_state, arguments, span, context)),
                "shuffle" => new BuiltinCallable(LythonKnownCallableSignatures.RandomShuffle, (arguments, span, context) => Shuffle(_state, arguments, span, context)),
                "sample" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSample, (arguments, span, context) => Sample(_state, arguments, span, context)),
                "getrandbits" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGetRandBits, (arguments, span, context) => GetRandBits(_state, arguments, span, context)),
                "randbytes" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandBytes, (arguments, span, context) => RandBytes(_state, arguments, span, context)),
                "uniform" => new BuiltinCallable(LythonKnownCallableSignatures.RandomUniform, (arguments, span, context) => Uniform(_state, arguments, span, context)),
                "triangular" => new BuiltinCallable(LythonKnownCallableSignatures.RandomTriangular, (arguments, span, context) => Triangular(_state, arguments, span, context)),
                "betavariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomBetaVariate, (arguments, span, context) => BetaVariate(_state, arguments, span, context)),
                "expovariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomExpVariate, (arguments, span, context) => ExpVariate(_state, arguments, span, context)),
                "gammavariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGammaVariate, (arguments, span, context) => GammaVariate(_state, arguments, span, context)),
                "gauss" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGauss, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.gauss")),
                "normalvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomNormalVariate, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.normalvariate")),
                "lognormvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomLogNormVariate, (arguments, span, context) => LogNormVariate(_state, arguments, span, context)),
                "paretovariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomParetoVariate, (arguments, span, context) => ParetoVariate(_state, arguments, span, context)),
                "vonmisesvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomVonMisesVariate, (arguments, span, context) => VonMisesVariate(_state, arguments, span, context)),
                "weibullvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomWeibullVariate, (arguments, span, context) => WeibullVariate(_state, arguments, span, context)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}

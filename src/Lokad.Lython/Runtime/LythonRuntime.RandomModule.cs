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
                "SystemRandom" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomSystemRandom, UnsupportedSystemRandom),
                "BPF" => new BigInteger(53),
                "RECIP_BPF" => 1.0 / (1UL << 53),
                "seed" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomSeed, (arguments, span, context) => Seed(_state, arguments, span, context)),
                "random" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomRandom, (arguments, span, context) => Random(_state, arguments, span, context)),
                "getstate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomGetState, (arguments, span, context) => GetState(_state, arguments, span, context)),
                "setstate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomSetState, (arguments, span, context) => SetState(_state, arguments, span, context)),
                "randrange" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomRandRange, (arguments, span, context) => RandRange(_state, arguments, span, context)),
                "randint" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomRandInt, (arguments, span, context) => RandInt(_state, arguments, span, context)),
                "choice" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomChoice, (arguments, span, context) => Choice(_state, arguments, span, context)),
                "choices" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomChoices, (arguments, span, context) => Choices(_state, arguments, span, context)),
                "shuffle" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomShuffle, (arguments, span, context) => Shuffle(_state, arguments, span, context)),
                "sample" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomSample, (arguments, span, context) => Sample(_state, arguments, span, context)),
                "getrandbits" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomGetRandBits, (arguments, span, context) => GetRandBits(_state, arguments, span, context)),
                "randbytes" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomRandBytes, (arguments, span, context) => RandBytes(_state, arguments, span, context)),
                "uniform" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomUniform, (arguments, span, context) => Uniform(_state, arguments, span, context)),
                "triangular" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomTriangular, (arguments, span, context) => Triangular(_state, arguments, span, context)),
                "betavariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomBetaVariate, (arguments, span, context) => BetaVariate(_state, arguments, span, context)),
                "expovariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomExpVariate, (arguments, span, context) => ExpVariate(_state, arguments, span, context)),
                "gammavariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomGammaVariate, (arguments, span, context) => GammaVariate(_state, arguments, span, context)),
                "gauss" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomGauss, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.gauss")),
                "normalvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomNormalVariate, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.normalvariate")),
                "lognormvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomLogNormVariate, (arguments, span, context) => LogNormVariate(_state, arguments, span, context)),
                "paretovariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomParetoVariate, (arguments, span, context) => ParetoVariate(_state, arguments, span, context)),
                "vonmisesvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomVonMisesVariate, (arguments, span, context) => VonMisesVariate(_state, arguments, span, context)),
                "weibullvariate" => BuiltinCallable.Create(LythonKnownCallableSignatures.RandomWeibullVariate, (arguments, span, context) => WeibullVariate(_state, arguments, span, context)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}

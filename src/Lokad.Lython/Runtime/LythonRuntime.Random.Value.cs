using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class RandomModule : PyModule
    {
        internal sealed class PyRandom : IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue
        {
            private readonly PyRandomState _state;

            public PyRandom(PyRandomState state)
            {
                _state = state;
            }

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "seed" => new BoundCallable((arguments, span, context) => Seed(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSeed),
                    "random" => new BoundCallable((arguments, span, context) => Random(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandom),
                    "getstate" => new BoundCallable((arguments, span, context) => GetState(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGetState),
                    "setstate" => new BoundCallable((arguments, span, context) => SetState(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSetState),
                    "randrange" => new BoundCallable((arguments, span, context) => RandRange(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandRange),
                    "randint" => new BoundCallable((arguments, span, context) => RandInt(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandInt),
                    "choice" => new BoundCallable((arguments, span, context) => Choice(_state, arguments, span, context), LythonKnownCallableSignatures.RandomChoice),
                    "choices" => new BoundCallable((arguments, span, context) => Choices(_state, arguments, span, context), LythonKnownCallableSignatures.RandomChoices),
                    "shuffle" => new BoundCallable((arguments, span, context) => Shuffle(_state, arguments, span, context), LythonKnownCallableSignatures.RandomShuffle),
                    "sample" => new BoundCallable((arguments, span, context) => Sample(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSample),
                    "getrandbits" => new BoundCallable((arguments, span, context) => GetRandBits(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGetRandBits),
                    "randbytes" => new BoundCallable((arguments, span, context) => RandBytes(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandBytes),
                    "uniform" => new BoundCallable((arguments, span, context) => Uniform(_state, arguments, span, context), LythonKnownCallableSignatures.RandomUniform),
                    "triangular" => new BoundCallable((arguments, span, context) => Triangular(_state, arguments, span, context), LythonKnownCallableSignatures.RandomTriangular),
                    "betavariate" => new BoundCallable((arguments, span, context) => BetaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomBetaVariate),
                    "expovariate" => new BoundCallable((arguments, span, context) => ExpVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomExpVariate),
                    "gammavariate" => new BoundCallable((arguments, span, context) => GammaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGammaVariate),
                    "gauss" => new BoundCallable((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.gauss"), LythonKnownCallableSignatures.RandomGauss),
                    "normalvariate" => new BoundCallable((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.normalvariate"), LythonKnownCallableSignatures.RandomNormalVariate),
                    "lognormvariate" => new BoundCallable((arguments, span, context) => LogNormVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomLogNormVariate),
                    "paretovariate" => new BoundCallable((arguments, span, context) => ParetoVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomParetoVariate),
                    "vonmisesvariate" => new BoundCallable((arguments, span, context) => VonMisesVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomVonMisesVariate),
                    "weibullvariate" => new BoundCallable((arguments, span, context) => WeibullVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomWeibullVariate),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<random.Random object>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
    }
}

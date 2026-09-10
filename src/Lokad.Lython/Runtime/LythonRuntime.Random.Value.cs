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
                    "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("random"),
                    "seed" => BoundCallable.Create((arguments, span, context) => Seed(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSeed),
                    "random" => BoundCallable.Create((arguments, span, context) => Random(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandom),
                    "getstate" => BoundCallable.Create((arguments, span, context) => GetState(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGetState),
                    "setstate" => BoundCallable.Create((arguments, span, context) => SetState(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSetState),
                    "randrange" => BoundCallable.Create((arguments, span, context) => RandRange(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandRange),
                    "randint" => BoundCallable.Create((arguments, span, context) => RandInt(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandInt),
                    "choice" => BoundCallable.Create((arguments, span, context) => Choice(_state, arguments, span, context), LythonKnownCallableSignatures.RandomChoice),
                    "choices" => BoundCallable.Create((arguments, span, context) => Choices(_state, arguments, span, context), LythonKnownCallableSignatures.RandomChoices),
                    "shuffle" => BoundCallable.Create((arguments, span, context) => Shuffle(_state, arguments, span, context), LythonKnownCallableSignatures.RandomShuffle),
                    "sample" => BoundCallable.Create((arguments, span, context) => Sample(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSample),
                    "getrandbits" => BoundCallable.Create((arguments, span, context) => GetRandBits(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGetRandBits),
                    "randbytes" => BoundCallable.Create((arguments, span, context) => RandBytes(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandBytes),
                    "uniform" => BoundCallable.Create((arguments, span, context) => Uniform(_state, arguments, span, context), LythonKnownCallableSignatures.RandomUniform),
                    "triangular" => BoundCallable.Create((arguments, span, context) => Triangular(_state, arguments, span, context), LythonKnownCallableSignatures.RandomTriangular),
                    "betavariate" => BoundCallable.Create((arguments, span, context) => BetaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomBetaVariate),
                    "expovariate" => BoundCallable.Create((arguments, span, context) => ExpVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomExpVariate),
                    "gammavariate" => BoundCallable.Create((arguments, span, context) => GammaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGammaVariate),
                    "gauss" => BoundCallable.Create((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.gauss"), LythonKnownCallableSignatures.RandomGauss),
                    "normalvariate" => BoundCallable.Create((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.normalvariate"), LythonKnownCallableSignatures.RandomNormalVariate),
                    "lognormvariate" => BoundCallable.Create((arguments, span, context) => LogNormVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomLogNormVariate),
                    "paretovariate" => BoundCallable.Create((arguments, span, context) => ParetoVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomParetoVariate),
                    "vonmisesvariate" => BoundCallable.Create((arguments, span, context) => VonMisesVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomVonMisesVariate),
                    "weibullvariate" => BoundCallable.Create((arguments, span, context) => WeibullVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomWeibullVariate),
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

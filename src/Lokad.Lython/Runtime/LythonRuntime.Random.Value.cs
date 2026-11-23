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
                    "seed" => BoundCallable.Create((arguments, span, context) => Seed(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Seed),
                    "random" => BoundCallable.Create((arguments, span, context) => Random(_state, arguments, span, context), LythonKnownCallableSignatures.Random.RandomRandom),
                    "getstate" => BoundCallable.Create((arguments, span, context) => GetState(_state, arguments, span, context), LythonKnownCallableSignatures.Random.GetState),
                    "setstate" => BoundCallable.Create((arguments, span, context) => SetState(_state, arguments, span, context), LythonKnownCallableSignatures.Random.SetState),
                    "randrange" => BoundCallable.Create((arguments, span, context) => RandRange(_state, arguments, span, context), LythonKnownCallableSignatures.Random.RandRange, (arguments, span, context) => RandRangeAsync(_state, arguments, span, context)),
                    "randint" => BoundCallable.Create((arguments, span, context) => RandInt(_state, arguments, span, context), LythonKnownCallableSignatures.Random.RandInt),
                    "choice" => BoundCallable.Create((arguments, span, context) => Choice(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Choice),
                    "choices" => BoundCallable.Create((arguments, span, context) => Choices(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Choices),
                    "shuffle" => BoundCallable.Create((arguments, span, context) => Shuffle(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Shuffle),
                    "sample" => BoundCallable.Create((arguments, span, context) => Sample(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Sample),
                    "getrandbits" => BoundCallable.Create((arguments, span, context) => GetRandBits(_state, arguments, span, context), LythonKnownCallableSignatures.Random.GetRandBits, (arguments, span, context) => GetRandBitsAsync(_state, arguments, span, context)),
                    "randbytes" => BoundCallable.Create((arguments, span, context) => RandBytes(_state, arguments, span, context), LythonKnownCallableSignatures.Random.RandBytes, (arguments, span, context) => RandBytesAsync(_state, arguments, span, context)),
                    "uniform" => BoundCallable.Create((arguments, span, context) => Uniform(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Uniform),
                    "triangular" => BoundCallable.Create((arguments, span, context) => Triangular(_state, arguments, span, context), LythonKnownCallableSignatures.Random.Triangular),
                    "betavariate" => BoundCallable.Create((arguments, span, context) => BetaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.BetaVariate),
                    "expovariate" => BoundCallable.Create((arguments, span, context) => ExpVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.ExpVariate),
                    "gammavariate" => BoundCallable.Create((arguments, span, context) => GammaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.GammaVariate),
                    "gauss" => BoundCallable.Create((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.gauss"), LythonKnownCallableSignatures.Random.Gauss),
                    "normalvariate" => BoundCallable.Create((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.normalvariate"), LythonKnownCallableSignatures.Random.NormalVariate),
                    "lognormvariate" => BoundCallable.Create((arguments, span, context) => LogNormVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.LogNormVariate),
                    "paretovariate" => BoundCallable.Create((arguments, span, context) => ParetoVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.ParetoVariate),
                    "vonmisesvariate" => BoundCallable.Create((arguments, span, context) => VonMisesVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.VonMisesVariate),
                    "weibullvariate" => BoundCallable.Create((arguments, span, context) => WeibullVariate(_state, arguments, span, context), LythonKnownCallableSignatures.Random.WeibullVariate),
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

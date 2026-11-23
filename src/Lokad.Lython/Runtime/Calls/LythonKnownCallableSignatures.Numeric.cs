namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    internal static class Statistics
    {
        public static readonly LythonCallableSignature Mean = LythonCallableSignature.Create("statistics.mean", ["data"]);
        public static readonly LythonCallableSignature FMean = LythonCallableSignature.Create("statistics.fmean", ["data", "weights"], requiredCount: 1);
        public static readonly LythonCallableSignature GeometricMean = LythonCallableSignature.Create("statistics.geometric_mean", ["data"]);
        public static readonly LythonCallableSignature HarmonicMean = LythonCallableSignature.Create("statistics.harmonic_mean", ["data", "weights"], requiredCount: 1);
        public static readonly LythonCallableSignature Median = LythonCallableSignature.Create("statistics.median", ["data"]);
        public static readonly LythonCallableSignature MedianLow = LythonCallableSignature.Create("statistics.median_low", ["data"]);
        public static readonly LythonCallableSignature MedianHigh = LythonCallableSignature.Create("statistics.median_high", ["data"]);
        public static readonly LythonCallableSignature MedianGrouped = LythonCallableSignature.Create("statistics.median_grouped", ["data", "interval"], requiredCount: 1);
        public static readonly LythonCallableSignature Mode = LythonCallableSignature.Create("statistics.mode", ["data"]);
        public static readonly LythonCallableSignature MultiMode = LythonCallableSignature.Create("statistics.multimode", ["data"]);
        public static readonly LythonCallableSignature PStdev = LythonCallableSignature.Create("statistics.pstdev", ["data", "mu"], requiredCount: 1);
        public static readonly LythonCallableSignature Stdev = LythonCallableSignature.Create("statistics.stdev", ["data", "xbar"], requiredCount: 1);
        public static readonly LythonCallableSignature PVariance = LythonCallableSignature.Create("statistics.pvariance", ["data", "mu"], requiredCount: 1);
        public static readonly LythonCallableSignature Variance = LythonCallableSignature.Create("statistics.variance", ["data", "xbar"], requiredCount: 1);
        public static readonly LythonCallableSignature Quantiles = LythonCallableSignature.Create("statistics.quantiles", ["data", "n", "method"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Covariance = LythonCallableSignature.Create("statistics.covariance", ["x", "y"]);
        public static readonly LythonCallableSignature Correlation = LythonCallableSignature.Create("statistics.correlation", ["x", "y"]);
        public static readonly LythonCallableSignature LinearRegression = LythonCallableSignature.Create("statistics.linear_regression", ["x", "y", "proportional"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature LinearRegressionResult = LythonCallableSignature.Create("statistics.LinearRegression", ["slope", "intercept"]);
        public static readonly LythonCallableSignature NormalDist = LythonCallableSignature.Create("statistics.NormalDist", ["mu", "sigma"], requiredCount: 0);
        public static readonly LythonCallableSignature NormalDistFromSamples = LythonCallableSignature.Create("statistics.NormalDist.from_samples", ["data"]);
        public static readonly LythonCallableSignature Kde = LythonCallableSignature.Create("statistics.kde", requiredCount: 0);
        public static readonly LythonCallableSignature KdeRandom = LythonCallableSignature.Create("statistics.kde_random", requiredCount: 0);
    }

    internal static class Random
    {
        public static readonly LythonCallableSignature Class = LythonCallableSignature.Create("random.Random", ["a"], requiredCount: 0);
        public static readonly LythonCallableSignature SystemRandom = LythonCallableSignature.Create("random.SystemRandom", ["a"], requiredCount: 0);
        public static readonly LythonCallableSignature Seed = LythonCallableSignature.Create("random.seed", ["a", "version"], requiredCount: 0);
        public static readonly LythonCallableSignature RandomRandom = LythonCallableSignature.Create("random.random", []);
        public static readonly LythonCallableSignature GetState = LythonCallableSignature.Create("random.getstate", []);
        public static readonly LythonCallableSignature SetState = LythonCallableSignature.Create("random.setstate", ["state"]);
        public static readonly LythonCallableSignature RandRange = LythonCallableSignature.Create("random.randrange", ["start", "stop", "step"], requiredCount: 1);
        public static readonly LythonCallableSignature RandInt = LythonCallableSignature.Create("random.randint", ["a", "b"]);
        public static readonly LythonCallableSignature Choice = LythonCallableSignature.Create("random.choice", ["seq"]);
        public static readonly LythonCallableSignature Choices = LythonCallableSignature.Create("random.choices", ["population", "weights", "cum_weights", "k"], requiredCount: 1);
        public static readonly LythonCallableSignature Shuffle = LythonCallableSignature.Create("random.shuffle", ["x"]);
        public static readonly LythonCallableSignature Sample = LythonCallableSignature.Create("random.sample", ["population", "k", "counts"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature GetRandBits = LythonCallableSignature.Create("random.getrandbits", ["k"]);
        public static readonly LythonCallableSignature RandBytes = LythonCallableSignature.Create("random.randbytes", ["n"]);
        public static readonly LythonCallableSignature Uniform = LythonCallableSignature.Create("random.uniform", ["a", "b"]);
        public static readonly LythonCallableSignature Triangular = LythonCallableSignature.Create("random.triangular", ["low", "high", "mode"], requiredCount: 0);
        public static readonly LythonCallableSignature BetaVariate = LythonCallableSignature.Create("random.betavariate", ["alpha", "beta"]);
        public static readonly LythonCallableSignature ExpVariate = LythonCallableSignature.Create("random.expovariate", ["lambd"], requiredCount: 0);
        public static readonly LythonCallableSignature GammaVariate = LythonCallableSignature.Create("random.gammavariate", ["alpha", "beta"]);
        public static readonly LythonCallableSignature Gauss = LythonCallableSignature.Create("random.gauss", ["mu", "sigma"], requiredCount: 0);
        public static readonly LythonCallableSignature NormalVariate = LythonCallableSignature.Create("random.normalvariate", ["mu", "sigma"], requiredCount: 0);
        public static readonly LythonCallableSignature LogNormVariate = LythonCallableSignature.Create("random.lognormvariate", ["mu", "sigma"]);
        public static readonly LythonCallableSignature ParetoVariate = LythonCallableSignature.Create("random.paretovariate", ["alpha"]);
        public static readonly LythonCallableSignature VonMisesVariate = LythonCallableSignature.Create("random.vonmisesvariate", ["mu", "kappa"]);
        public static readonly LythonCallableSignature WeibullVariate = LythonCallableSignature.Create("random.weibullvariate", ["alpha", "beta"]);
    }

    internal static class Math
    {
        public static readonly LythonCallableSignature Sqrt = LythonCallableSignature.Create("math.sqrt", ["x"]);
        public static readonly LythonCallableSignature Exp = LythonCallableSignature.Create("math.exp", ["x"]);
        public static readonly LythonCallableSignature Log = LythonCallableSignature.Create("math.log", ["x", "base"], requiredCount: 1);
        public static readonly LythonCallableSignature Log10 = LythonCallableSignature.Create("math.log10", ["x"]);
        public static readonly LythonCallableSignature Log2 = LythonCallableSignature.Create("math.log2", ["x"]);
        public static readonly LythonCallableSignature Sin = LythonCallableSignature.Create("math.sin", ["x"]);
        public static readonly LythonCallableSignature Cos = LythonCallableSignature.Create("math.cos", ["x"]);
        public static readonly LythonCallableSignature Tan = LythonCallableSignature.Create("math.tan", ["x"]);
        public static readonly LythonCallableSignature Asin = LythonCallableSignature.Create("math.asin", ["x"]);
        public static readonly LythonCallableSignature Acos = LythonCallableSignature.Create("math.acos", ["x"]);
        public static readonly LythonCallableSignature Atan = LythonCallableSignature.Create("math.atan", ["x"]);
        public static readonly LythonCallableSignature Atan2 = LythonCallableSignature.Create("math.atan2", ["y", "x"]);
        public static readonly LythonCallableSignature Sinh = LythonCallableSignature.Create("math.sinh", ["x"]);
        public static readonly LythonCallableSignature Cosh = LythonCallableSignature.Create("math.cosh", ["x"]);
        public static readonly LythonCallableSignature Tanh = LythonCallableSignature.Create("math.tanh", ["x"]);
        public static readonly LythonCallableSignature Floor = LythonCallableSignature.Create("math.floor", ["x"]);
        public static readonly LythonCallableSignature Ceil = LythonCallableSignature.Create("math.ceil", ["x"]);
        public static readonly LythonCallableSignature Fabs = LythonCallableSignature.Create("math.fabs", ["x"]);
        public static readonly LythonCallableSignature Trunc = LythonCallableSignature.Create("math.trunc", ["x"]);
        public static readonly LythonCallableSignature Degrees = LythonCallableSignature.Create("math.degrees", ["x"]);
        public static readonly LythonCallableSignature Radians = LythonCallableSignature.Create("math.radians", ["x"]);
        public static readonly LythonCallableSignature IsFinite = LythonCallableSignature.Create("math.isfinite", ["x"]);
        public static readonly LythonCallableSignature IsInf = LythonCallableSignature.Create("math.isinf", ["x"]);
        public static readonly LythonCallableSignature IsNaN = LythonCallableSignature.Create("math.isnan", ["x"]);
        public static readonly LythonCallableSignature Pow = LythonCallableSignature.Create("math.pow", ["x", "y"]);
        public static readonly LythonCallableSignature Hypot = LythonCallableSignature.Create("math.hypot", requiredCount: 0);
        public static readonly LythonCallableSignature Fmod = LythonCallableSignature.Create("math.fmod", ["x", "y"]);
        public static readonly LythonCallableSignature CopySign = LythonCallableSignature.Create("math.copysign", ["x", "y"]);
        public static readonly LythonCallableSignature IsClose = LythonCallableSignature.Create("math.isclose", ["a", "b", "rel_tol", "abs_tol"], requiredCount: 2);
        public static readonly LythonCallableSignature Prod = LythonCallableSignature.Create("math.prod", ["iterable", "start"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Fsum = LythonCallableSignature.Create("math.fsum", ["iterable"]);
        public static readonly LythonCallableSignature Factorial = LythonCallableSignature.Create("math.factorial", ["n"]);
        public static readonly LythonCallableSignature Gcd = LythonCallableSignature.Create("math.gcd", requiredCount: 0);
        public static readonly LythonCallableSignature Lcm = LythonCallableSignature.Create("math.lcm", requiredCount: 0);
        public static readonly LythonCallableSignature Comb = LythonCallableSignature.Create("math.comb", ["n", "k"]);
        public static readonly LythonCallableSignature Perm = LythonCallableSignature.Create("math.perm", ["n", "k"], requiredCount: 1);
        public static readonly LythonCallableSignature Isqrt = LythonCallableSignature.Create("math.isqrt", ["n"]);
        public static readonly LythonCallableSignature Dist = LythonCallableSignature.Create("math.dist", ["p", "q"]);
        public static readonly LythonCallableSignature Frexp = LythonCallableSignature.Create("math.frexp", ["x"]);
        public static readonly LythonCallableSignature Ldexp = LythonCallableSignature.Create("math.ldexp", ["x", "i"]);
        public static readonly LythonCallableSignature Modf = LythonCallableSignature.Create("math.modf", ["x"]);
        public static readonly LythonCallableSignature Remainder = LythonCallableSignature.Create("math.remainder", ["x", "y"]);
        public static readonly LythonCallableSignature NextAfter = LythonCallableSignature.Create("math.nextafter", ["x", "y", "steps"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Ulp = LythonCallableSignature.Create("math.ulp", ["x"]);
        public static readonly LythonCallableSignature Exp2 = LythonCallableSignature.Create("math.exp2", ["x"]);
        public static readonly LythonCallableSignature Expm1 = LythonCallableSignature.Create("math.expm1", ["x"]);
        public static readonly LythonCallableSignature Log1p = LythonCallableSignature.Create("math.log1p", ["x"]);
        public static readonly LythonCallableSignature Cbrt = LythonCallableSignature.Create("math.cbrt", ["x"]);
        public static readonly LythonCallableSignature Erf = LythonCallableSignature.Create("math.erf", ["x"]);
        public static readonly LythonCallableSignature Erfc = LythonCallableSignature.Create("math.erfc", ["x"]);
        public static readonly LythonCallableSignature Gamma = LythonCallableSignature.Create("math.gamma", ["x"]);
        public static readonly LythonCallableSignature Lgamma = LythonCallableSignature.Create("math.lgamma", ["x"]);
        public static readonly LythonCallableSignature Fma = LythonCallableSignature.Create("math.fma", ["x", "y", "z"], requiredCount: 3, maximumPositionalArgumentCount: 3, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 0);
        public static readonly LythonCallableSignature SumProd = LythonCallableSignature.Create("math.sumprod", ["p", "q"], requiredCount: 2, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 0);
    }
}

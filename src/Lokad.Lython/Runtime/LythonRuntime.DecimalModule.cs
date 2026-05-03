namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class DecimalModule : PyModule
    {
        public static readonly DecimalModule Instance = new();

        private DecimalModule() : base("decimal")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "Decimal" => new BuiltinCallable("decimal.Decimal", DecimalCtor, ["value"], requiredCount: 1),
                "InvalidOperation" => new ExceptionTypeValue("InvalidOperation"),
                "DivisionByZero" => new ExceptionTypeValue("DivisionByZero"),
                "ROUND_HALF_EVEN" => Runtime.Text.PyString.FromString("ROUND_HALF_EVEN"),
                "ROUND_DOWN" => Runtime.Text.PyString.FromString("ROUND_DOWN"),
                "ROUND_UP" => Runtime.Text.PyString.FromString("ROUND_UP"),
                _ => null!,
            };

            return value is not null;
        }
    }

    private static object DecimalCtor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "decimal.Decimal(value) expects one argument.", span);
        }

        return PyDecimalOps.Parse(arguments[0], span);
    }
}

using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class JsonModule : PyModule
    {
        public static readonly JsonModule Instance = new();

        private JsonModule() : base("json")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "load" => BuiltinCallable.Create(LythonKnownCallableSignatures.JsonLoad, Load),
                "loads" => BuiltinCallable.Create(LythonKnownCallableSignatures.JsonLoads, Loads),
                "dump" => BuiltinCallable.Create(LythonKnownCallableSignatures.JsonDump, Dump),
                "dumps" => BuiltinCallable.Create(LythonKnownCallableSignatures.JsonDumps, Dumps),
                "JSONDecodeError" => new ExceptionTypeValue("JSONDecodeError"),
                "JSONEncoder" => new UnsupportedJsonClassFactory("json.JSONEncoder"),
                "JSONDecoder" => new UnsupportedJsonClassFactory("json.JSONDecoder"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Load(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1 || arguments[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.load(fp, *, ...) expects a readable text file handle.", span);
            }

            var loadOptions = ParseJsonLoadOptions(arguments, span);
            return ParseJsonText(file.Read(), loadOptions, context, span);
        }

        private object Loads(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "json.loads(s, *, ...) expects a string argument.", span);
            }

            var loadOptions = ParseJsonLoadOptions(arguments, span);
            return ParseJsonText(text, loadOptions, context, span);
        }

        private object Dump(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 2 || arguments[1] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.dump(obj, fp, *, ...) expects an object and writable text file handle.", span);
            }

            var text = SerializeJsonText(arguments[0], ParseJsonDumpOptions(arguments, JsonDumpCallForm.Dump, span), context, span);
            _ = file.Write(text);
            return PyNone.Instance;
        }

        private object Dumps(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);
            }

            return SerializeJsonText(arguments[0], ParseJsonDumpOptions(arguments, JsonDumpCallForm.Dumps, span), context, span);
        }
    }
}

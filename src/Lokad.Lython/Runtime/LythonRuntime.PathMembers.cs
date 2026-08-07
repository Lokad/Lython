using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class PathStatMembers
    {
        public static bool TryGetMember(LythonPathStat stat, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "exists" => stat.Exists,
                "is_file" => stat.IsFile,
                "is_dir" => stat.IsDir,
                "size" => stat.Size,
                "modified_at" => stat.ModifiedAt,
                "st_size" => stat.Size,
                "st_mtime" => PathModifiedAtSeconds(stat.ModifiedAtTimestamp, null),
                "st_ctime" => PathModifiedAtSeconds(stat.ModifiedAtTimestamp, null),
                "st_atime" => PathModifiedAtSeconds(stat.ModifiedAtTimestamp, null),
                "st_mode" or "st_ino" or "st_dev" or "st_nlink" or "st_uid" or "st_gid"
                    => throw new LythonRuntimeException("NotImplementedError", "Rich stat_result metadata is not supported by Lython because the host path model only exposes existence, kind, size, and modified time.", null),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class ExceptionInstanceMembers
    {
        public static bool TryGetMember(PyException exception, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "type" => PyString.FromString(exception.TypeName),
                "message" => PyString.FromString(exception.Message),
                "args" => CreateExceptionArgs(exception),
                "code" when string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal) => exception.Value,
                _ => MissingMemberValue.Instance,
            };

            if (ReferenceEquals(value, MissingMemberValue.Instance) &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString(name), out var payloadValue))
            {
                value = payloadValue;
            }

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PyTuple CreateExceptionArgs(PyException exception)
        {
            if (exception.ExplicitArgs is not null)
            {
                return exception.ExplicitArgs;
            }

            if (string.Equals(exception.TypeName, "SystemExit", StringComparison.Ordinal))
            {
                return ReferenceEquals(exception.Value, PyNone.Instance)
                    ? PyTuple.Empty
                    : new PyTuple([exception.Value]);
            }

            if (string.Equals(exception.TypeName, "JSONDecodeError", StringComparison.Ordinal) &&
                exception.Value is PyDict payload &&
                payload.TryGetValue(PyString.FromString("msg"), out var msg) &&
                payload.TryGetValue(PyString.FromString("doc"), out var doc) &&
                payload.TryGetValue(PyString.FromString("pos"), out var pos))
            {
                return new PyTuple([msg, doc, pos]);
            }

            if (string.Equals(exception.TypeName, "CalledProcessError", StringComparison.Ordinal) &&
                exception.Value is PyDict subprocessPayload &&
                subprocessPayload.TryGetValue(PyString.FromString("returncode"), out var returnCode) &&
                subprocessPayload.TryGetValue(PyString.FromString("cmd"), out var command))
            {
                return new PyTuple([returnCode, command]);
            }

            if (string.Equals(exception.TypeName, "TimeoutExpired", StringComparison.Ordinal) &&
                exception.Value is PyDict timeoutPayload &&
                timeoutPayload.TryGetValue(PyString.FromString("cmd"), out var timeoutCommand) &&
                timeoutPayload.TryGetValue(PyString.FromString("timeout"), out var timeout))
            {
                return new PyTuple([timeoutCommand, timeout]);
            }

            return exception.Value switch
            {
                PyNone => PyTuple.Empty,
                _ => new PyTuple([exception.Value])
            };
        }
    }

    internal static class DecimalMembers
    {
        public static bool TryGetMember(PyDecimal decimalValue, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "quantize" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length is < 1 or > 3 || arguments[0] is not PyDecimal exponent)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.quantize(exp[, rounding][, context]) expects a Decimal exponent plus optional rounding/context.", span);
                    }

                    var rounding = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
                    var decimalContext = arguments.Length >= 3 && arguments[2] is not PyNone
                        ? arguments[2] as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal.quantize(..., context=...) expects a Context or None.", span)
                        : context.DecimalContext;
                    return PyDecimalOps.Quantize(decimalValue, exponent, rounding, decimalContext, span);
                }, new LythonCallableSignature("Decimal.quantize", ["exp", "rounding", "context"], RequiredCount: 1)),
                "normalize" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("normalize", decimalValue, arguments, span), new LythonCallableSignature("Decimal.normalize", ["context"], RequiredCount: 0)),
                "sqrt" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("sqrt", decimalValue, arguments, span), new LythonCallableSignature("Decimal.sqrt", ["context"], RequiredCount: 0)),
                "exp" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("exp", decimalValue, arguments, span), new LythonCallableSignature("Decimal.exp", ["context"], RequiredCount: 0)),
                "ln" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("ln", decimalValue, arguments, span), new LythonCallableSignature("Decimal.ln", ["context"], RequiredCount: 0)),
                "log10" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("log10", decimalValue, arguments, span), new LythonCallableSignature("Decimal.log10", ["context"], RequiredCount: 0)),
                "copy_abs" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_abs", decimalValue, arguments, span)),
                "copy_negate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Unary("copy_negate", decimalValue, arguments, span)),
                "copy_sign" => new BoundCallable((arguments, span, _) => PyDecimalOps.CopySign(decimalValue, arguments, span), "Decimal.copy_sign", ["other"]),
                "to_integral_value" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral_value", ["rounding", "context"], RequiredCount: 0)),
                "to_integral_exact" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral_exact", ["rounding", "context"], RequiredCount: 0)),
                "to_integral" => new BoundCallable((arguments, span, context) => PyDecimalOps.ToIntegral(decimalValue, arguments, context.DecimalContext, span), new LythonCallableSignature("Decimal.to_integral", ["rounding", "context"], RequiredCount: 0)),
                "as_tuple" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.as_tuple() expects no arguments.", span);
                    }

                    return PyDecimalOps.AsTuple(decimalValue);
                }, "Decimal.as_tuple", []),
                "adjusted" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.adjusted() expects no arguments.", span);
                    }

                    return PyDecimalOps.Adjusted(decimalValue);
                }, "Decimal.adjusted", []),
                "compare" => new BoundCallable((arguments, span, _) => PyDecimalOps.CompareValue(decimalValue, arguments, span), new LythonCallableSignature("Decimal.compare", ["other", "context"], RequiredCount: 1)),
                "compare_total" => new BoundCallable((arguments, span, _) => PyDecimalOps.CompareTotal(decimalValue, arguments, span), "Decimal.compare_total", ["other"]),
                "is_nan" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_nan", arguments, span, false), "Decimal.is_nan", []),
                "is_infinite" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_infinite", arguments, span, false), "Decimal.is_infinite", []),
                "is_finite" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_finite", arguments, span, true), "Decimal.is_finite", []),
                "is_zero" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_zero", arguments, span, decimalValue.Value == 0m), "Decimal.is_zero", []),
                "is_signed" => new BoundCallable((arguments, span, _) => ExpectDecimalNoArguments("is_signed", arguments, span, decimalValue.IsSigned), "Decimal.is_signed", []),
                "to_eng_string" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Decimal.to_eng_string() expects no arguments.", span);
                    }

                    return PyDecimalOps.ToEngineeringString(decimalValue);
                }, "Decimal.to_eng_string", []),
                "scaleb" => new BoundCallable((arguments, span, _) => PyDecimalOps.ScaleB(decimalValue, arguments, span), new LythonCallableSignature("Decimal.scaleb", ["other", "context"], RequiredCount: 1)),
                "shift" => new BoundCallable((arguments, span, _) => PyDecimalOps.Shift(decimalValue, arguments, span), "Decimal.shift", ["other"]),
                "rotate" => new BoundCallable((arguments, span, _) => PyDecimalOps.Rotate(decimalValue, arguments, span), "Decimal.rotate", ["other"]),
                "same_quantum" => new BoundCallable((arguments, span, _) => PyDecimalOps.SameQuantum(decimalValue, arguments, span), "Decimal.same_quantum", ["other"]),
                "remainder_near" => new BoundCallable((arguments, span, _) => PyDecimalOps.RemainderNear(decimalValue, arguments, span), new LythonCallableSignature("Decimal.remainder_near", ["other", "context"], RequiredCount: 1)),
                "min" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "min", span), new LythonCallableSignature("Decimal.min", ["other", "context"], RequiredCount: 1)),
                "max" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "max", span), new LythonCallableSignature("Decimal.max", ["other", "context"], RequiredCount: 1)),
                "min_mag" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "min_mag", span), new LythonCallableSignature("Decimal.min_mag", ["other", "context"], RequiredCount: 1)),
                "max_mag" => new BoundCallable((arguments, span, _) => PyDecimalOps.MinMax(decimalValue, arguments, "max_mag", span), new LythonCallableSignature("Decimal.max_mag", ["other", "context"], RequiredCount: 1)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static bool ExpectDecimalNoArguments(string name, object[] arguments, LythonSourceSpan span, bool result)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"Decimal.{name}() expects no arguments.", span);
            }

            return result;
        }
    }

    internal static class PathMembers
    {
        public static bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(PathOps.BaseName(path.Value.AsString())),
                "suffix" => PyString.FromString(PathOps.Suffix(path.Value.AsString())),
                "suffixes" => PathSuffixes(path.Value),
                "stem" => PyString.FromString(PathOps.Stem(path.Value.AsString())),
                "parent" => new PyPath(PathOps.Parent(path.Value)),
                "parents" => PathOps.Parents(path.Value),
                "parts" => PathOps.Parts(path.Value),
                "drive" => PyString.Empty,
                "root" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                "anchor" => PathOps.IsAbsolute(path.Value.AsString()) ? PyStringOps.SlashLiteral : PyString.Empty,
                "__fspath__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.__fspath__() expects no arguments.", span);
                    }

                    return path.Value;
                }, "Path.__fspath__", []),
                "is_absolute" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_absolute() expects no arguments.", span);
                    }

                    return PathOps.IsAbsolute(path.Value.AsString());
                }),
                "is_mount" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_mount() expects no arguments.", span);
                    }

                    return string.Equals(PathOps.Normalize(path.Value.AsString()), "/", StringComparison.Ordinal);
                }, "Path.is_mount", []),
                "is_reserved" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_reserved() expects no arguments.", span);
                    }

                    return false;
                }, "Path.is_reserved", []),
                "joinpath" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length == 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects at least one string argument.", span);
                    }

                    var current = path.Value;
                    foreach (var argument in arguments)
                    {
                        var part = argument switch
                        {
                            PyPath pathArgument => pathArgument.Value,
                            _ when PyStringOps.TryAsString(argument, out var text) => text,
                            _ => throw new LythonRuntimeException("TypeError", "Path.joinpath(*other) expects Path or string arguments.", span)
                        };

                        if (part.Length == 0)
                        {
                            continue;
                        }

                        current = PathOps.Join(current, part);
                    }

                    return new PyPath(current);
                }),
                "expanduser" => UnsupportedPathMember("Path.expanduser", "Path.expanduser() is not supported by Lython; the host does not expose an ambient user home directory."),
                "match" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.match(pattern) expects one string argument.", span);
                    }

                    return PathOps.Match(path.Value.AsString(), pattern.AsString());
                }, "Path.match", ["pattern"]),
                "is_relative_to" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_relative_to(other) expects one argument.", span);
                    }

                    var other = RequirePath(arguments[0], "Path.is_relative_to(other)", span);
                    try
                    {
                        PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString());
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                }, "Path.is_relative_to", ["other"]),
                "as_posix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.as_posix() expects no arguments.", span);
                    }

                    return path.Value;
                }),
                "resolve" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.resolve() expects no arguments.", span);
                    }

                    return new PyPath(PathOps.Normalize(path.Value, PyString.FromString(context.Host.Cwd)));
                }),
                "absolute" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.absolute() expects no arguments.", span);
                    }

                    return new PyPath(PathOps.MakeAbsoluteLexical(path.Value, PyString.FromString(context.Host.Cwd)));
                }),
                "relative_to" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.relative_to(other) expects one argument.", span);
                    }

                    var other = RequirePath(arguments[0], "Path.relative_to(other)", span);
                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.RelativeTo(path.Value.AsString(), other.Value.AsString())));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.relative_to", ["other"]),
                "with_suffix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.with_suffix(suffix) expects one string argument.", span);
                    }

                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.WithSuffix(path.Value.AsString(), suffix.AsString())));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.with_suffix", ["suffix"]),
                "with_name" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var name))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.with_name(name) expects one string argument.", span);
                    }

                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.WithName(path.Value.AsString(), name.AsString())));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.with_name", ["name"]),
                "with_stem" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var stem))
                    {
                        throw new LythonRuntimeException("TypeError", "Path.with_stem(stem) expects one string argument.", span);
                    }

                    try
                    {
                        return new PyPath(PyString.FromString(PathOps.WithName(path.Value.AsString(), stem.AsString() + PathOps.Suffix(path.Value.AsString()))));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "Path.with_stem", ["stem"]),
                "stat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.stat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }),
                "lstat" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.lstat() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }, "Path.lstat", []),
                "exists" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostExists(path.Value.AsString(), span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.exists() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return await context.HostExistsAsync(path.Value.AsString(), span).ConfigureAwait(false);
                }),
                "is_file" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span).IsFile;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_file() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsFile;
                }),
                "is_dir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return context.HostStat(path.Value.AsString(), span).IsDir;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_dir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    return (await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).IsDir;
                }),
                "is_symlink" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.is_symlink() expects no arguments.", span);
                    }

                    return false;
                }),
                "unlink" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                    }

                    var missingOk = ParseOptionalBool(arguments, 0, false, "Path.unlink([missing_ok])", "missing_ok", span);
                    if (missingOk)
                    {
                        context.RegisterHostCall(span);
                        if (!context.HostStat(path.Value.AsString(), span).Exists)
                        {
                            return PyNone.Instance;
                        }
                    }

                    context.RegisterHostCall(span);
                    context.HostRemove(path.Value.AsString(), span);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.unlink([missing_ok]) expects zero or one argument.", span);
                    }

                    var missingOk = ParseOptionalBool(arguments, 0, false, "Path.unlink([missing_ok])", "missing_ok", span);
                    if (missingOk)
                    {
                        context.RegisterHostCall(span);
                        if (!(await context.HostStatAsync(path.Value.AsString(), span).ConfigureAwait(false)).Exists)
                        {
                            return PyNone.Instance;
                        }
                    }

                    context.RegisterHostCall(span);
                    await context.HostRemoveAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    return PyNone.Instance;
                }, "Path.unlink", ["missing_ok"], 0),
                "rmdir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    context.HostRemove(path.Value.AsString(), span);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rmdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    await context.HostRemoveAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "rename" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.rename(target)", span);
                    context.RegisterHostCall(span);
                    context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                    return target;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.rename(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.rename(target)", span);
                    context.RegisterHostCall(span);
                    await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                    return target;
                }, "Path.rename", ["target"]),
                "replace" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.replace(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.replace(target)", span);
                    context.RegisterHostCall(span);
                    if (context.HostStat(target.Value.AsString(), span).Exists)
                    {
                        context.RegisterHostCall(span);
                        context.HostRemove(target.Value.AsString(), span);
                    }

                    context.RegisterHostCall(span);
                    context.HostMove(path.Value.AsString(), target.Value.AsString(), span);
                    return target;
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.replace(target) expects one argument.", span);
                    }

                    var target = RequirePath(arguments[0], "Path.replace(target)", span);
                    context.RegisterHostCall(span);
                    if ((await context.HostStatAsync(target.Value.AsString(), span).ConfigureAwait(false)).Exists)
                    {
                        context.RegisterHostCall(span);
                        await context.HostRemoveAsync(target.Value.AsString(), span).ConfigureAwait(false);
                    }

                    context.RegisterHostCall(span);
                    await context.HostMoveAsync(path.Value.AsString(), target.Value.AsString(), span).ConfigureAwait(false);
                    return target;
                }, "Path.replace", ["target"]),
                "mkdir" => new BoundCallable((arguments, span, context) =>
                {
                    PathMkDir(path.Value.AsString(), arguments, span, context);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    await PathMkDirAsync(path.Value.AsString(), arguments, span, context).ConfigureAwait(false);
                    return PyNone.Instance;
                }, "Path.mkdir", ["mode", "parents", "exist_ok"], 0),
                "touch" => new BoundCallable((arguments, span, context) =>
                {
                    PathTouch(path.Value.AsString(), arguments, span, context);
                    return PyNone.Instance;
                },
                async (arguments, span, context) =>
                {
                    await PathTouchAsync(path.Value.AsString(), arguments, span, context).ConfigureAwait(false);
                    return PyNone.Instance;
                }, "Path.touch", ["mode", "exist_ok"], 0),
                "read_bytes" => UnsupportedPathMember("Path.read_bytes", "Path.read_bytes() is not supported by Lython under the text-only host boundary."),
                "write_bytes" => UnsupportedPathMember("Path.write_bytes", "Path.write_bytes(data) is not supported by Lython under the text-only host boundary."),
                "readlink" => UnsupportedPathMember("Path.readlink", "Path.readlink() is not supported by Lython because symlink targets are not exposed by the host path model."),
                "symlink_to" => UnsupportedPathMember("Path.symlink_to", "Path.symlink_to(target, target_is_directory=False) is not supported by Lython because symlink mutation is outside the host path model."),
                "hardlink_to" => UnsupportedPathMember("Path.hardlink_to", "Path.hardlink_to(target) is not supported by Lython because hardlink mutation is outside the host path model."),
                "chmod" => UnsupportedPathMember("Path.chmod", "Path.chmod(mode) is not supported by Lython because permissions are not exposed by the host path model."),
                "owner" => UnsupportedPathMember("Path.owner", "Path.owner() is not supported by Lython because user ownership is not exposed by the host path model."),
                "group" => UnsupportedPathMember("Path.group", "Path.group() is not supported by Lython because group ownership is not exposed by the host path model."),
                "open" => new PathOpenCallable(path.Value.AsString()),
                "glob" => new BoundCallable((arguments, span, context) =>
                {
                    var pattern = ParsePathGlobArguments(arguments, "Path.glob", span);

                    var results = new PyList([], context.MemoryGovernor, span);
                    context.RegisterHostCall(span);
                    foreach (var name in context.HostListDir(path.Value.AsString(), span))
                    {
                        context.CheckExecutionBudget(span);
                        if (LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern))
                        {
                            results.Add(new PyPath(PyString.FromString(
                                PathOps.Join(path.Value.AsString(), name),
                                context.MemoryGovernor,
                                span)));
                            context.ObserveCollectionCount(results.Count, span);
                        }
                    }

                    return results;
                },
                async (arguments, span, context) =>
                {
                    var pattern = ParsePathGlobArguments(arguments, "Path.glob", span);

                    var results = new PyList([], context.MemoryGovernor, span);
                    context.RegisterHostCall(span);
                    var names = await context.HostListDirAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    foreach (var name in names)
                    {
                        context.CheckExecutionBudget(span);
                        if (LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern))
                        {
                            results.Add(new PyPath(PyString.FromString(
                                PathOps.Join(path.Value.AsString(), name),
                                context.MemoryGovernor,
                                span)));
                            context.ObserveCollectionCount(results.Count, span);
                        }
                    }

                    return results;
                }, "Path.glob", ["pattern", "case_sensitive", "recurse_symlinks"], 1),
                "iterdir" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.iterdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    var entries = context.HostListDir(path.Value.AsString(), span)
                        .Select<string, object>(name => new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                    return new PyList(entries, context.MemoryGovernor, span);
                },
                async (arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.iterdir() expects no arguments.", span);
                    }

                    context.RegisterHostCall(span);
                    var names = await context.HostListDirAsync(path.Value.AsString(), span).ConfigureAwait(false);
                    var entries = names.Select<string, object>(name => new PyPath(PathOps.Join(path.Value, PyString.FromString(name))));
                    return new PyList(entries, context.MemoryGovernor, span);
                }),
                "read_text" => new BoundCallable((arguments, span, context) =>
                {
                    var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                    return ReadPathText(path.Value.AsString(), encodingMode, errors, newline, context, span);
                },
                async (arguments, span, context) =>
                {
                    var (encodingMode, errors, newline) = ParsePathReadTextArguments(arguments, span);
                    return await ReadPathTextAsync(path.Value.AsString(), encodingMode, errors, newline, context, span).ConfigureAwait(false);
                }, "Path.read_text", ["encoding", "errors", "newline"], 0),
                "write_text" => new BoundCallable((arguments, span, context) =>
                {
                    var (text, encodingMode, errors, newline) = ParsePathWriteTextArguments(arguments, span);
                    context.ObserveString(text, span);
                    var payload = EncodePathText(text, encodingMode, errors, newline, context, span);
                    context.RegisterHostCall(span);
                    WriteEncodedHostText(path.Value.AsString(), payload, encodingMode, context, span);
                    return new BigInteger(text.Length);
                },
                async (arguments, span, context) =>
                {
                    var (text, encodingMode, errors, newline) = ParsePathWriteTextArguments(arguments, span);
                    context.ObserveString(text, span);
                    var payload = EncodePathText(text, encodingMode, errors, newline, context, span);
                    context.RegisterHostCall(span);
                    await WriteEncodedHostTextAsync(path.Value.AsString(), payload, encodingMode, context, span).ConfigureAwait(false);
                    return new BigInteger(text.Length);
                }, "Path.write_text", ["text", "encoding", "errors", "newline"], 1),
                "rglob" => new BoundCallable((arguments, span, context) =>
                {
                    var pattern = ParsePathGlobArguments(arguments, "Path.rglob", span);

                    var results = new PyList([], context.MemoryGovernor, span);
                    foreach (var item in EnumerateRecursive(path.Value, pattern, context, span))
                    {
                        results.Add(item);
                        context.ObserveCollectionCount(results.Count, span);
                    }

                    return results;
                },
                async (arguments, span, context) =>
                {
                    var pattern = ParsePathGlobArguments(arguments, "Path.rglob", span);

                    var results = new PyList([], context.MemoryGovernor, span);
                    await EnumerateRecursiveAsync(path.Value, pattern, context, span, results).ConfigureAwait(false);
                    return results;
                }, "Path.rglob", ["pattern", "case_sensitive", "recurse_symlinks"], 1),
                "samefile" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "Path.samefile(other_path) expects one argument.", span);
                    }

                    var other = RequirePath(arguments[0], "Path.samefile(other_path)", span);
                    var left = PathOps.Normalize(path.Value.AsString(), context.Host.Cwd);
                    var right = PathOps.Normalize(other.Value.AsString(), context.Host.Cwd);
                    context.RegisterHostCall(span);
                    var leftStat = context.HostStat(left, span);
                    context.RegisterHostCall(span);
                    var rightStat = context.HostStat(right, span);
                    if (!leftStat.Exists || !rightStat.Exists)
                    {
                        throw new LythonRuntimeException("RuntimeError", "Path.samefile() expects both paths to exist.", span);
                    }

                    return string.Equals(left, right, StringComparison.Ordinal);
                }, "Path.samefile", ["other_path"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryGetMember(PyPath path, string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "parents" => PathOps.Parents(path.Value, context.MemoryGovernor, span),
                "parts" => PathOps.Parts(path.Value, context.MemoryGovernor, span),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static BoundCallable UnsupportedPathMember(string name, string message)
            => new((object[] arguments, LythonSourceSpan span, ExecutionContext context) =>
            {
                _ = arguments;
                _ = context;
                throw new LythonRuntimeException("NotImplementedError", message, span);
            }, name: name);

        private static PyString ParsePathGlobArguments(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(pattern[, case_sensitive][, recurse_symlinks]) expects a string pattern.", span);
            }

            if (arguments.Length >= 2 &&
                arguments[1] is not PyNone &&
                arguments[1] is not true)
            {
                throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., case_sensitive=False) is not supported by Lython's normalized path matcher.", span);
            }

            if (arguments.Length >= 3 &&
                arguments[2] is not PyNone and not false)
            {
                throw new LythonRuntimeException("NotImplementedError", $"{owner}(..., recurse_symlinks=True) is not supported by Lython because symlink traversal is outside the host path model.", span);
            }

            return pattern;
        }

        private static PyPath RequirePath(object value, string signature, LythonSourceSpan span)
        {
            return value switch
            {
                PyPath path => path,
                _ when PyStringOps.TryAsString(value, out var text) => new PyPath(PathOps.NormalizeLexical(text)),
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects a Path or string argument.", span)
            };
        }

        private static PyList PathSuffixes(PyString path)
        {
            var name = PathOps.BaseName(path.AsString());
            var suffixes = new List<object>();
            var dot = name.IndexOf('.', name.StartsWith(".", StringComparison.Ordinal) ? 1 : 0);
            while (dot >= 0 && dot < name.Length - 1)
            {
                var next = name.IndexOf('.', dot + 1);
                suffixes.Add(PyString.FromString(next < 0 ? name[dot..] : name[dot..next]));
                dot = next;
            }

            return new PyList(suffixes);
        }

        private static bool ParseOptionalBool(object[] arguments, int index, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is null or PyNone)
            {
                return defaultValue;
            }

            return arguments[index] is bool value
                ? value
                : throw new LythonRuntimeException("TypeError", $"{owner} expects {parameterName} to be a bool.", span);
        }

        private static void ValidateIgnoredPathMode(object[] arguments, int index, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is null or PyNone)
            {
                return;
            }

            if (!Numbers.PyNumberOps.TryAsInteger(arguments[index], out _))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects mode to be an integer.", span);
            }
        }

        private static void PathMkDir(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.mkdir([mode][, parents][, exist_ok])", span);
            var parents = ParseOptionalBool(arguments, 1, false, "Path.mkdir([mode][, parents][, exist_ok])", "parents", span);
            var existOk = ParseOptionalBool(arguments, 2, false, "Path.mkdir([mode][, parents][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);

            if (parents)
            {
                PathMkDirs(normalized, existOk, span, context);
                return;
            }

            if (existOk)
            {
                context.RegisterHostCall(span);
                var stat = context.HostStat(normalized, span);
                if (stat.Exists && stat.IsDir)
                {
                    return;
                }

                if (stat.Exists)
                {
                    throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
                }
            }

            context.RegisterHostCall(span);
            context.HostMkDir(normalized, span);
        }

        private static async ValueTask PathMkDirAsync(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.mkdir([mode][, parents][, exist_ok]) expects zero to three arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.mkdir([mode][, parents][, exist_ok])", span);
            var parents = ParseOptionalBool(arguments, 1, false, "Path.mkdir([mode][, parents][, exist_ok])", "parents", span);
            var existOk = ParseOptionalBool(arguments, 2, false, "Path.mkdir([mode][, parents][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);

            if (parents)
            {
                await PathMkDirsAsync(normalized, existOk, span, context).ConfigureAwait(false);
                return;
            }

            if (existOk)
            {
                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
                if (stat.Exists && stat.IsDir)
                {
                    return;
                }

                if (stat.Exists)
                {
                    throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
                }
            }

            context.RegisterHostCall(span);
            await context.HostMkDirAsync(normalized, span).ConfigureAwait(false);
        }

        private static void PathMkDirs(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.RegisterHostCall(span);
            var stat = context.HostStat(normalized, span);
            if (stat.Exists)
            {
                if (stat.IsDir && existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
            }

            foreach (var current in EnumerateMissingDirectories(normalized))
            {
                context.RegisterHostCall(span);
                var currentStat = context.HostStat(current, span);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                context.HostMkDir(current, span);
            }
        }

        private static async ValueTask PathMkDirsAsync(string normalized, bool existOk, LythonSourceSpan span, ExecutionContext context)
        {
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
            if (stat.Exists)
            {
                if (stat.IsDir && existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() target already exists: {normalized}", span);
            }

            foreach (var current in EnumerateMissingDirectories(normalized))
            {
                context.RegisterHostCall(span);
                var currentStat = await context.HostStatAsync(current, span).ConfigureAwait(false);
                if (currentStat.Exists)
                {
                    if (!currentStat.IsDir)
                    {
                        throw new LythonRuntimeException("RuntimeError", $"Path.mkdir() path component is not a directory: {current}", span);
                    }

                    continue;
                }

                context.RegisterHostCall(span);
                await context.HostMkDirAsync(current, span).ConfigureAwait(false);
            }
        }

        private static void PathTouch(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.touch([mode][, exist_ok])", span);
            var existOk = ParseOptionalBool(arguments, 1, true, "Path.touch([mode][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);
            context.RegisterHostCall(span);
            var stat = context.HostStat(normalized, span);
            if (stat.Exists)
            {
                if (existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.touch() target already exists: {normalized}", span);
            }

            context.RegisterHostCall(span);
            context.WriteTextUtf8(normalized, Array.Empty<byte>(), span);
        }

        private static async ValueTask PathTouchAsync(string path, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "Path.touch([mode][, exist_ok]) expects zero to two arguments.", span);
            }

            ValidateIgnoredPathMode(arguments, 0, "Path.touch([mode][, exist_ok])", span);
            var existOk = ParseOptionalBool(arguments, 1, true, "Path.touch([mode][, exist_ok])", "exist_ok", span);
            var normalized = PathOps.Normalize(path, context.Host.Cwd);
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(normalized, span).ConfigureAwait(false);
            if (stat.Exists)
            {
                if (existOk)
                {
                    return;
                }

                throw new LythonRuntimeException("RuntimeError", $"Path.touch() target already exists: {normalized}", span);
            }

            context.RegisterHostCall(span);
            await context.WriteTextUtf8Async(normalized, Array.Empty<byte>(), span).ConfigureAwait(false);
        }

        private sealed class PathOpenCallable(string path) : ICallable
        {
            private static readonly LythonCallableSignature CallSignature = new(
                "Path.open",
                ["mode", "buffering", "encoding", "errors", "newline"],
                RequiredCount: 0);

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var (mode, encodingMode, errors, newline) = ParsePathOpenArguments(BindArguments(arguments, span), span);
                return OpenTextFile(path, mode, encodingMode, errors, newline, span, context);
            }

            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var (mode, encodingMode, errors, newline) = ParsePathOpenArguments(BindArguments(arguments, span), span);
                return mode switch
                {
                    "r" => await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    "a" => await LythonRuntime.ExecutionContext.TextFileHandle.ForAppendAsync(path, context, encodingMode, errors, newline).ConfigureAwait(false),
                    _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                };
            }

            private static BoundOpenArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
                => BoundOpenArguments.From(CallBinder.BindNamedArgumentsWithPresence(arguments, span, CallSignature, PythonCallableKind.Method));

            private static object OpenTextFile(
                string path,
                string mode,
                TextEncodingMode encodingMode,
                TextErrorMode errors,
                TextNewlineMode newline,
                LythonSourceSpan span,
                ExecutionContext context)
            {
                return mode switch
                {
                    "r" => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(path, context, encodingMode, errors, newline),
                    "w" => LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(path, context, encodingMode, errors, newline),
                    "a" => LythonRuntime.ExecutionContext.TextFileHandle.ForAppend(path, context, encodingMode, errors, newline),
                    _ => throw new LythonRuntimeException("ValueError", "Path.open() only supports modes 'r', 'w', and 'a'.", span)
                };
            }
        }

        private static (string Mode, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathOpenArguments(BoundOpenArguments boundArguments, LythonSourceSpan span)
        {
            var arguments = boundArguments.Values;
            if (boundArguments.Count > 5)
            {
                throw new LythonRuntimeException("TypeError", "Path.open([mode][, buffering][, encoding][, errors][, newline]) expects supported text-mode options.", span);
            }

            var mode = boundArguments.Assigned[0]
                ? arguments[0] switch
                {
                    PyString text => text,
                    _ => throw new LythonRuntimeException("TypeError", "Path.open(mode) expects mode to be a string.", span)
                }
                : PyString.FromString("r");

            if (boundArguments.Count >= 2)
            {
                ValidateTextBuffering(arguments[1], "Path.open()", span);
            }

            var encodingMode = boundArguments.Count >= 3
                ? ParseTextEncoding(arguments[2], "Path.open()", span)
                : TextEncodingMode.Utf8;
            var errors = boundArguments.Count >= 4
                ? ParseTextErrors(arguments[3], "Path.open()", span)
                : TextErrorMode.Strict;
            var newline = boundArguments.Count >= 5
                ? ParseTextNewline(arguments[4], "Path.open()", span)
                : TextNewlineMode.TranslateUniversal;

            return (ParseTextOpenMode(mode, "Path.open()", span), encodingMode, errors, newline);
        }

        private static (TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathReadTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "Path.read_text([encoding][, errors][, newline]) expects zero to three arguments.", span);
            }

            var encodingMode = arguments.Length >= 1
                ? ParseTextEncoding(arguments[0], "Path.read_text()", span)
                : TextEncodingMode.Utf8;
            var errors = arguments.Length >= 2
                ? ParseTextErrors(arguments[1], "Path.read_text()", span)
                : TextErrorMode.Strict;
            var newline = arguments.Length >= 3
                ? ParseTextNewline(arguments[2], "Path.read_text()", span)
                : TextNewlineMode.TranslateUniversal;

            return (encodingMode, errors, newline);
        }

        private static (PyString Text, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParsePathWriteTextArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Path.write_text(text[, encoding][, errors][, newline]) expects a string plus optional keyword-compatible arguments.", span);
            }

            var encodingMode = arguments.Length >= 2
                ? ParseTextEncoding(arguments[1], "Path.write_text()", span)
                : TextEncodingMode.Utf8;
            var errors = arguments.Length >= 3
                ? ParseTextErrors(arguments[2], "Path.write_text()", span)
                : TextErrorMode.Strict;
            var newline = arguments.Length == 4
                ? ParseTextNewline(arguments[3], "Path.write_text()", span)
                : TextNewlineMode.TranslateUniversal;

            return (text, encodingMode, errors, newline);
        }

        private static PyString ReadPathText(
            string path,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            PyString text;
            if (encodingMode == TextEncodingMode.Latin1)
            {
                using var payload = ReadGovernedHostBytes(path, context, span);
                text = DecodeText(payload.Memory, encodingMode, context, span, errors, newline);
            }
            else
            {
                text = StripUtf8Bom(ReadGovernedHostText(path, context, span, errors, newline), encodingMode);
            }

            context.ObserveString(text, span);
            return text;
        }

        private static async ValueTask<PyString> ReadPathTextAsync(
            string path,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            PyString text;
            if (encodingMode == TextEncodingMode.Latin1)
            {
                using var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
                text = DecodeText(payload.Memory, encodingMode, context, span, errors, newline);
            }
            else
            {
                text = StripUtf8Bom(
                    await ReadGovernedHostTextAsync(path, context, span, errors, newline).ConfigureAwait(false),
                    encodingMode);
            }

            context.ObserveString(text, span);
            return text;
        }

        private static byte[] EncodePathText(
            PyString text,
            TextEncodingMode encodingMode,
            TextErrorMode errors,
            TextNewlineMode newline,
            ExecutionContext context,
            LythonSourceSpan span)
            => EncodeText(text, encodingMode, errors, newline, context, span);

        private static void WriteEncodedHostText(
            string path,
            ReadOnlyMemory<byte> payload,
            TextEncodingMode encoding,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (encoding == TextEncodingMode.Latin1)
            {
                context.WriteHostBytes(path, payload, span);
            }
            else
            {
                context.WriteTextUtf8(path, payload, span);
            }
        }

        private static ValueTask WriteEncodedHostTextAsync(
            string path,
            ReadOnlyMemory<byte> payload,
            TextEncodingMode encoding,
            ExecutionContext context,
            LythonSourceSpan span)
            => encoding == TextEncodingMode.Latin1
                ? context.WriteHostBytesAsync(path, payload, span)
                : context.WriteTextUtf8Async(path, payload, span);

        private static IEnumerable<object> EnumerateRecursive(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span)
        {
            context.RegisterHostCall(span);
            foreach (var name in context.HostListDir(root.AsString(), span))
            {
                context.CheckExecutionBudget(span);
                var child = new PyPath(PathOps.Join(root, PyString.FromString(name)));
                context.RegisterHostCall(span);
                var stat = context.HostStat(child.Value.AsString(), span);
                if (stat.IsDir)
                {
                    foreach (var nested in EnumerateRecursive(child.Value, pattern, context, span))
                    {
                        yield return nested;
                    }

                    continue;
                }

                if (stat.IsFile && MatchRglobPattern(name, pattern))
                {
                    yield return child;
                }
            }
        }

        private static async ValueTask EnumerateRecursiveAsync(PyString root, PyString pattern, ExecutionContext context, LythonSourceSpan span, PyList results)
        {
            context.RegisterHostCall(span);
            var names = await context.HostListDirAsync(root.AsString(), span).ConfigureAwait(false);
            foreach (var name in names)
            {
                context.CheckExecutionBudget(span);
                var child = new PyPath(PathOps.Join(root, PyString.FromString(name)));
                context.RegisterHostCall(span);
                var stat = await context.HostStatAsync(child.Value.AsString(), span).ConfigureAwait(false);
                if (stat.IsDir)
                {
                    await EnumerateRecursiveAsync(child.Value, pattern, context, span, results).ConfigureAwait(false);
                    continue;
                }

                if (stat.IsFile && MatchRglobPattern(name, pattern))
                {
                    results.Add(child);
                    context.ObserveCollectionCount(results.Count, span);
                }
            }
        }

        private static bool MatchRglobPattern(string name, PyString pattern)
        {
            return LythonRuntime.FnMatchModule.MatchSimple(PyString.FromString(name), pattern);
        }
    }

}

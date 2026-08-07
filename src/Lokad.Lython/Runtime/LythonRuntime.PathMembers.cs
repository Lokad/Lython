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

    internal static partial class PathMembers
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
    }
}

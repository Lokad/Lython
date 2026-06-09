using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static class StaticContractEngine
{
    private delegate bool StaticCallMatcher(CallExpressionSyntax call);

    private delegate void StaticCallAnalyzer(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings);

    private sealed record StaticCallContract(StaticCallMatcher Matches, StaticCallAnalyzer Analyze);

    private static readonly StaticCallContract[] RegisteredCallContracts =
    [
        new(IsInputCall, AnalyzeInputCall),
        new(IsReadTextCall, AnalyzeReadTextCall),
        new(IsWriteAppendTextCall, AnalyzeWriteAppendTextCall),
        new(IsOnePathStringBuiltinCall, AnalyzeOnePathStringBuiltinCall),
        new(IsTwoPathStringBuiltinCall, AnalyzeTwoPathStringBuiltinCall),
        new(IsJoinPathCall, AnalyzeJoinPathCall),
        new(IsDefaultDictCall, AnalyzeDefaultDictCall),
        new(IsArgumentParserConstructorCall, AnalyzeArgumentParserConstructorCall),
        new(IsFunctoolsPartialCall, AnalyzeFunctoolsPartialCall),
        new(IsPropertyCall, AnalyzePropertyCall),
        new(IsGlobCollectorCall, AnalyzeGlobCollectorCall),
        new(IsGlobEscapeCall, AnalyzeGlobEscapeCall),
        new(IsSysStreamWriteCall, AnalyzeSysStreamWriteCall),
    ];

    public static bool TryAnalyzeCall(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (TryAnalyzeRegisteredCall(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticTextIoContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticCollectionContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticStringContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticFilesystemContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticProcessContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticArgparseContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticDataclassContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (StaticDataModuleContractFamily.TryAnalyze(call, arguments, diagnostics, bindings))
        {
            return true;
        }

        return false;
    }

    private static bool TryAnalyzeRegisteredCall(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var contract in RegisteredCallContracts)
        {
            if (contract.Matches(call))
            {
                contract.Analyze(call, arguments, diagnostics, bindings);
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveMemberValue(MemberExpressionSyntax member, AbstractState bindings, out AbstractValue value)
    {
        if (StaticAbstractValueResolver.TryResolve(member.Target, bindings, out var receiverValue) &&
            (TryResolveModuleMemberValue(receiverValue, member.MemberName, member.Span, out value) ||
             StaticContracts.TryGetMemberValue(receiverValue, member.MemberName, member.Span, out value)))
        {
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryResolveCallReturn(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (TryResolveKnownCallReturn(call, bindings, out value))
        {
            return true;
        }

        if (StaticRegexContractFamily.TryResolvePatternMemberCallReturn(call, bindings, out value))
        {
            return true;
        }

        if (StaticRegexContractFamily.TryResolveMatchMemberCallReturn(call, bindings, out value))
        {
            return true;
        }

        if (TryResolveDictionaryMemberCallReturn(call, bindings, out value))
        {
            return true;
        }

        if (call.Target is MemberExpressionSyntax { Target: var memberReceiver, MemberName: var memberName } &&
            StaticAbstractValueResolver.TryResolve(memberReceiver, bindings, out var receiverValue) &&
            StaticContracts.TryGetMemberReturn(receiverValue, memberName, call.Span, out value))
        {
            if (StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) &&
                StaticContracts.TryGetCallableContract(receiverValue, memberName, out var contract) &&
                !contract.AcceptsArgumentShape(arguments))
            {
                value = default;
                return false;
            }

            return true;
        }

        value = default;
        return false;
    }

    public static bool AnalyzeKnownCallContract(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryResolveKnownCallableTarget(call.Target, bindings, out var targetName) ||
            !StaticContracts.TryGetKnownCallContract(targetName, out var contract))
        {
            return false;
        }

        if (!contract.TryGetArgumentShapeFailure(arguments, out var reason, out var offendingExpression))
        {
            _ = AnalyzeKnownCallSemanticContract(targetName, call, arguments, diagnostics, bindings);
            return false;
        }

        StaticDiagnosticSink.AddError(
            diagnostics,
            contract.DiagnosticCode,
            contract.Message,
            offendingExpression?.Span ?? call.Span,
            new StaticDiagnosticProof("contract", targetName, reason));
        return true;
    }

    public static bool AnalyzeCallableContract(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is not MemberExpressionSyntax member ||
            !StaticAbstractValueResolver.TryResolve(member.Target, bindings, out var receiver) ||
            !StaticContracts.TryGetCallableContract(receiver, member.MemberName, out var contract))
        {
            return false;
        }

        if (!contract.TryGetArgumentShapeFailure(arguments, out var reason, out var offendingExpression))
        {
            return AnalyzeCallableSemanticContract(call, arguments, diagnostics, bindings, receiver, member.MemberName);
        }

        StaticDiagnosticSink.AddError(
            diagnostics,
            contract.DiagnosticCode,
            contract.Message,
            offendingExpression?.Span ?? call.Span,
            new StaticDiagnosticProof("contract", member.MemberName, reason, receiver));
        return true;
    }

    private static bool AnalyzeCallableSemanticContract(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        AbstractValue receiver,
        string memberName)
        => StaticRegexContractFamily.AnalyzeCallableSemanticContract(call, arguments, diagnostics, bindings, receiver, memberName);

    private static bool TryResolveModuleMemberValue(AbstractValue receiverValue, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        if (receiverValue.Kind == AbstractValueKind.Module)
        {
            return StaticContracts.TryGetModuleMemberValue((string)receiverValue.Value, memberName, span, out value);
        }

        value = default;
        return false;
    }

    private static bool TryResolveKnownCallReturn(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (!TryResolveKnownCallableTarget(call.Target, bindings, out var targetName) ||
            !StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !StaticContracts.TryGetKnownCallContract(targetName, out var contract) ||
            !contract.AcceptsArgumentShape(arguments))
        {
            value = default;
            return false;
        }

        if (StaticDataclassContractFamily.TryResolveKnownCallReturn(targetName, arguments, bindings, call.Span, out value))
        {
            return true;
        }

        if (StaticRegexContractFamily.TryResolveKnownCallReturn(targetName, arguments, bindings, call.Span, out value))
        {
            return true;
        }

        if (StaticTypingContractFamily.TryResolveKnownCallReturn(targetName, arguments, bindings, call.Span, out value))
        {
            return true;
        }

        return StaticContracts.TryGetKnownCallReturn(targetName, call.Span, out value);
    }

    private static bool TryResolveKnownCallableTarget(ExpressionSyntax target, AbstractState bindings, out string targetName)
    {
        if (StaticAbstractValueResolver.TryResolve(target, bindings, out var value) &&
            value.Kind == AbstractValueKind.KnownCallable)
        {
            targetName = (string)value.Value;
            return true;
        }

        targetName = string.Empty;
        return false;
    }

    private static bool AnalyzeKnownCallSemanticContract(
        string targetName,
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        emitted |= StaticDataclassContractFamily.AnalyzeKnownCallSemanticContract(targetName, call, arguments, diagnostics, bindings);
        emitted |= AnalyzeKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        emitted |= StaticDataModuleContractFamily.AnalyzeKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        emitted |= StaticRegexContractFamily.AnalyzeKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        emitted |= StaticArgparseContractFamily.AnalyzeKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        emitted |= StaticProcessContractFamily.AnalyzeKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        emitted |= StaticFilesystemContractFamily.AnalyzeKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings);
        return emitted;
    }

    public static bool TryAddUnsatisfiedHostRequirement(StaticAnalysisContext context, StaticHostRequirement requirement, ILythonHost host)
    {
        if (StaticContracts.IsHostRequirementSatisfied(requirement, host))
        {
            return false;
        }

        StaticDiagnosticSink.AddError(
            context,
            requirement.DiagnosticCode,
            requirement.Message,
            requirement.Span,
            new StaticDiagnosticProof("host", requirement.Capability.ToString(), "host capability is not available"));
        return true;
    }

    private static bool TryResolveDictionaryMemberCallReturn(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (call.Target is not MemberExpressionSyntax { Target: var receiverExpression, MemberName: var memberName } ||
            memberName is not ("get" or "pop" or "setdefault") ||
            !StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver) ||
            receiver.Kind != AbstractValueKind.Dict ||
            !StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !StaticContracts.TryGetCallableContract(receiver, memberName, out var contract) ||
            !contract.AcceptsArgumentShape(arguments) ||
            !arguments.TryGetValue(0, "key", out var keyExpression))
        {
            value = default;
            return false;
        }

        var pairs = (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)receiver.Value;
        var fallback = TryGetDictionaryFallbackValue(memberName, arguments, bindings, call.Span, out var fallbackValue)
            ? fallbackValue
            : AbstractValue.Unknown(call.Span);

        if (arguments.TryResolveValue(0, "key", bindings, out var key))
        {
            foreach (var pair in pairs)
            {
                if (AbstractValuesEqual(pair.Key, key))
                {
                    value = pair.Value.WithSpan(call.Span);
                    return true;
                }
            }

            if (memberName is "get" or "setdefault")
            {
                value = fallback.WithSpan(call.Span);
                return true;
            }

            value = default;
            return false;
        }

        if (memberName == "pop")
        {
            value = pairs.Count == 0 ? default : JoinDictValues(pairs, call.Span);
            return pairs.Count != 0;
        }

        value = pairs.Count == 0
            ? fallback.WithSpan(call.Span)
            : AbstractValue.Join(JoinDictValues(pairs, call.Span), fallback, call.Span);
        return true;
    }

    private static bool TryGetDictionaryFallbackValue(
        string memberName,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue fallback)
    {
        if (memberName == "pop")
        {
            fallback = default;
            return false;
        }

        fallback = arguments.TryGetValue(1, "default", out _)
            ? arguments.TryResolveValue(1, "default", bindings, out var defaultValue) ? defaultValue : AbstractValue.Unknown(span)
            : AbstractValue.None(span);
        return true;
    }

    private static AbstractValue JoinDictValues(IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>> pairs, LythonSourceSpan span)
    {
        var result = AbstractValue.Never(span);
        foreach (var pair in pairs)
        {
            result = AbstractValue.Join(result, pair.Value, span);
        }

        return result.Kind == AbstractValueKind.Never ? AbstractValue.Unknown(span) : result;
    }

    private static bool AbstractValuesEqual(AbstractValue left, AbstractValue right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            AbstractValueKind.String or
            AbstractValueKind.Integer or
            AbstractValueKind.Float or
            AbstractValueKind.Boolean => Equals(left.Value, right.Value),
            AbstractValueKind.Bytes => ((byte[])left.Value).AsSpan().SequenceEqual((byte[])right.Value),
            AbstractValueKind.None => true,
            _ => false
        };
    }

    private static bool IsInputCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "input" };

    private static bool IsReadTextCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "read_text" };

    private static bool IsWriteAppendTextCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "write_text" or "append_text" };

    private static bool IsOnePathStringBuiltinCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "exists" or "listdir" or "mkdir" or "remove" or "dirname" or "basename" or "stat" };

    private static bool IsTwoPathStringBuiltinCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "copy" or "move" };

    private static bool IsJoinPathCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "join_path" };

    private static bool IsDefaultDictCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "defaultdict" } or
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "collections" }, MemberName: "defaultdict" };

    private static bool IsArgumentParserConstructorCall(CallExpressionSyntax call)
        => call.Target is MemberExpressionSyntax
        {
            Target: IdentifierExpressionSyntax { Name: "argparse" },
            MemberName: "ArgumentParser"
        };

    private static bool IsFunctoolsPartialCall(CallExpressionSyntax call)
        => call.Target is MemberExpressionSyntax
        {
            Target: IdentifierExpressionSyntax { Name: "functools" },
            MemberName: "partial"
        };

    private static bool IsPropertyCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "property" };

    private static bool IsGlobCollectorCall(CallExpressionSyntax call)
        => call.Target is MemberExpressionSyntax
        {
            Target: IdentifierExpressionSyntax { Name: "glob" },
            MemberName: "glob" or "iglob"
        };

    private static bool IsGlobEscapeCall(CallExpressionSyntax call)
        => call.Target is MemberExpressionSyntax
        {
            Target: IdentifierExpressionSyntax { Name: "glob" },
            MemberName: "escape"
        };

    private static bool IsSysStreamWriteCall(CallExpressionSyntax call)
        => call.Target is MemberExpressionSyntax
        {
            Target: MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "sys" },
                MemberName: "stdout" or "stderr"
            },
            MemberName: "write"
        };

    private static void AnalyzeInputCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "prompt", "LA3086", "input([prompt]) expects zero or one string argument.", diagnostics, bindings);
    }

    private static void AnalyzeReadTextCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "path", "LA3082", "read_text(path) expects one string argument.", diagnostics, bindings);
    }

    private static void AnalyzeWriteAppendTextCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var name = ((IdentifierExpressionSyntax)call.Target).Name;
        var message = name == "write_text"
            ? "write_text(path, text) expects two string arguments."
            : "append_text(path, text) expects two string arguments.";
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "path", "LA3081", message, diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownNonByteStringArgument(arguments, 1, "text", "LA3081", message, diagnostics, bindings);
        StaticContractChecks.AnalyzeTextBoundaryStringArgument(arguments, 1, "text", "LA3046", "Text-only host APIs do not accept bytes; Lython host boundaries are UTF-8 text-shaped only.", diagnostics, bindings);
    }

    private static void AnalyzeOnePathStringBuiltinCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var name = ((IdentifierExpressionSyntax)call.Target).Name;
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "path", "LA3083", $"{name}(path) expects one string argument.", diagnostics, bindings);
    }

    private static void AnalyzeTwoPathStringBuiltinCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var name = ((IdentifierExpressionSyntax)call.Target).Name;
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "source", "LA3084", $"{name}(source, destination) expects two string arguments.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 1, "destination", "LA3084", $"{name}(source, destination) expects two string arguments.", diagnostics, bindings);
    }

    private static void AnalyzeJoinPathCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        foreach (var expression in arguments.Positional)
        {
            if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out _) &&
                StaticAbstractValueResolver.IsDefinitelyKnownLiteral(expression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3087", "join_path(...) expects one or more string arguments.", expression.Span);
                return;
            }
        }
    }

    private static void AnalyzeDefaultDictCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 0, "default_factory", "LA3048", "defaultdict default_factory must be callable or None.", diagnostics, bindings);
    }

    private static void AnalyzeArgumentParserConstructorCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(arguments, 0, "prog", "LA3088", "argparse.ArgumentParser(..., prog=...) expects prog to be a string or None when it is statically known.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(arguments, 1, "usage", "LA3088", "argparse.ArgumentParser(..., usage=...) expects usage to be a string or None when it is statically known.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(arguments, 2, "description", "LA3088", "argparse.ArgumentParser(..., description=...) expects description to be a string or None when it is statically known.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(arguments, 3, "epilog", "LA3088", "argparse.ArgumentParser(..., epilog=...) expects epilog to be a string or None when it is statically known.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownBooleanArgument(arguments, 5, "add_help", "LA3088", "argparse.ArgumentParser(..., add_help=...) expects add_help to be a bool when it is statically known.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownBooleanArgument(arguments, 6, "allow_abbrev", "LA3088", "argparse.ArgumentParser(..., allow_abbrev=...) expects allow_abbrev to be a bool when it is statically known.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownBooleanArgument(arguments, 7, "exit_on_error", "LA3088", "argparse.ArgumentParser(..., exit_on_error=...) expects exit_on_error to be a bool when it is statically known.", diagnostics, bindings);
    }

    private static void AnalyzeFunctoolsPartialCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 0, "func", "LA3085", "functools.partial(func, ...) expects the first argument to be callable.", diagnostics, bindings);
    }

    private static void AnalyzePropertyCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 0, "fget", "LA3036", "property(fget=...) expects a callable or None.", diagnostics, bindings);
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 1, "fset", "LA3036", "property(fset=...) expects a callable or None.", diagnostics, bindings);
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 2, "fdel", "LA3036", "property(fdel=...) expects a callable or None.", diagnostics, bindings);
    }

    private static void AnalyzeGlobCollectorCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var memberName = ((MemberExpressionSyntax)call.Target).MemberName;
        if (arguments.TryGetValue(0, "pathname", out var pathnameExpression) &&
            !StaticAbstractValueResolver.TryResolveKnownString(pathnameExpression, bindings, out _) &&
            StaticAbstractValueResolver.IsDefinitelyKnownLiteral(pathnameExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3089", $"glob.{memberName}(pathname[, recursive]) expects a string pattern.", pathnameExpression.Span);
        }

        if (arguments.TryGetValue(1, "recursive", out var recursiveExpression) &&
            recursiveExpression is not NoneLiteralExpressionSyntax &&
            recursiveExpression is not BooleanLiteralExpressionSyntax &&
            StaticAbstractValueResolver.IsDefinitelyKnownLiteral(recursiveExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3090", $"glob.{memberName}(pathname, recursive) expects recursive to be a bool.", recursiveExpression.Span);
        }
    }

    private static void AnalyzeGlobEscapeCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "pathname", "LA3096", "glob.escape(pathname) expects one string argument.", diagnostics, bindings);
    }

    private static void AnalyzeSysStreamWriteCall(CallExpressionSyntax call, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeTextBoundaryStringArgument(arguments, 0, "text", "LA3046", "Text-only host APIs do not accept bytes; Lython host boundaries are UTF-8 text-shaped only.", diagnostics, bindings);
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}

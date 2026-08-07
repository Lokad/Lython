using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyFunction CreateLoweredFunction(
        LoweredFunctionDefinitionStatement functionDefinition,
        ExecutionContext context,
        Dictionary<string, object> defaults)
        => new(
            functionDefinition.Syntax.Name,
            functionDefinition.Parameters,
            functionDefinition.Body,
            context.FunctionClosureContext,
            defaults,
            ScopeDirectiveFactsCollector.ForFunction(functionDefinition.Syntax));

    private static PyType CreateLoweredClassType(
        LoweredClassDefinitionStatement classDefinition,
        IReadOnlyList<PyType> resolvedBases,
        ExecutionContext classContext,
        ExecutionContext definingContext)
    {
        StoreClassAnnotations(classDefinition.Syntax, classContext.Variables, classContext);

        PyType type;
        try
        {
            type = new PyType(
                classDefinition.Syntax.Name,
                resolvedBases,
                new Dictionary<string, object>(classContext.Variables, StringComparer.Ordinal));
        }
        catch (InvalidOperationException ex)
        {
            throw new LythonRuntimeException("TypeError", ex.Message, classDefinition.Span);
        }

        if (definingContext.TryGetBuiltinType("type", out var metaType))
        {
            type.SetMetaType(metaType);
        }

        PyDataclass.Apply(type, classDefinition.Syntax, classContext.Variables, classContext, classDefinition.Span);
        type.InitializeClassMembers(definingContext, classDefinition.Span);
        return type;
    }

    private static void StoreClassAnnotations(
        ClassDefinitionStatementSyntax syntax,
        Dictionary<string, object> members,
        ExecutionContext context)
    {
        PyDict? annotations = null;
        foreach (var statement in syntax.Body.OfType<AnnotatedAssignmentStatementSyntax>())
        {
            annotations ??= new PyDict(context.MemoryGovernor, syntax.Span);
            annotations.SetItem(PyString.FromString(statement.Name), PyDataclass.CreateAnnotationValue(statement.Annotation));
        }

        if (annotations is not null)
        {
            members["__annotations__"] = annotations;
        }
    }

    private static PyString ValidateLoweredString(PyString text, ExecutionContext context, LythonSourceSpan span)
    {
        context.ObserveString(text, span);
        return text;
    }

    private static T ValidateLoweredCollection<T>(T collection, ExecutionContext context, LythonSourceSpan span)
        where T : IReadOnlyCollection<object>
    {
        context.ObserveCollectionCount(collection.Count, span);
        return collection;
    }

    private static object ReadLoweredSubscript(
        object target,
        object index,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span), context, span);
        }

        return PyIndexing.ReadIndex(target, index, span);
    }

    private static object ResolveLoweredMemberValue(
        LoweredMemberExpression member,
        object target,
        ExecutionContext context)
    {
        if (TryResolveCachedRuntimeMember(member, target, context, out var value))
        {
            return value;
        }

        throw PyMemberAccess.CreateMissingMemberError(target, member.Member.MemberName, member.Span);
    }

    private static bool TryResolveCachedRuntimeMember(
        LoweredMemberExpression member,
        object target,
        ExecutionContext context,
        [MaybeNullWhen(false)] out object value)
    {
        if (context.State.TryReadRuntimeMemberCache(member, target, out value))
        {
            return true;
        }

        if (!TryResolveRuntimeMember(target, member.Member.MemberName, context, member.Span, out value))
        {
            return false;
        }

        if (CanCacheRuntimeMemberTarget(target))
        {
            context.State.WriteRuntimeMemberCache(member, target, value);
        }

        return true;
    }

    private static object EvaluateLoweredBinaryOperator(
        LoweredBinaryExpression binary,
        object left,
        object right,
        ExecutionContext context)
    {
        return binary.Binary.Operator switch
        {
            BinaryOperatorSyntax.Add => EvaluateAdd(left, right, context, binary.Span),
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, binary.Span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, binary.Span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, binary.Span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, binary.Span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, context, binary.Span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, binary.Span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, binary.Span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, binary.Span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, binary.Span),
            BinaryOperatorSyntax.Less => Compare(left, right, binary.Span) < 0,
            BinaryOperatorSyntax.LessEqual => Compare(left, right, binary.Span) <= 0,
            BinaryOperatorSyntax.Greater => Compare(left, right, binary.Span) > 0,
            BinaryOperatorSyntax.GreaterEqual => Compare(left, right, binary.Span) >= 0,
            BinaryOperatorSyntax.Is => AreIdentical(left, right),
            BinaryOperatorSyntax.IsNot => !AreIdentical(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, binary.Span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, binary.Span),
            BinaryOperatorSyntax.Equal => AreEqual(left, right),
            BinaryOperatorSyntax.NotEqual => !AreEqual(left, right),
            _ => throw new InvalidOperationException($"Unknown binary operator: {binary.Binary.Operator}")
        };
    }

    private static object EvaluateLoweredUnaryOperator(
        LoweredUnaryExpression unary,
        object operand,
        ExecutionContext context)
    {
        return unary.Unary.Operator switch
        {
            UnaryOperatorSyntax.Not => !IsTruthy(operand, context, unary.Span),
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, unary.Span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, unary.Span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, unary.Span),
            _ => throw new InvalidOperationException($"Unknown unary operator: {unary.Unary.Operator}")
        };
    }

    private static void ThrowLoweredRaisedValue(object raised, LythonSourceSpan span)
    {
        if (raised is not PyException instance)
        {
            throw RuntimeErrors.RaiseExpectsException(span);
        }

        throw new LythonRuntimeException(instance.TypeName, instance.Message, span, innerException: null, payload: instance.Value);
    }

    private static LoweredFunctionParameter[] LowerLambdaParameters(LoweredLambdaExpression lambda)
        => lambda.Lambda.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();

    private static object StoreLoweredAssignmentResult(
        LoweredAssignmentExpression assignment,
        object value,
        ExecutionContext context)
    {
        StoreName(assignment.Assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static void ValidateClassKeywordArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (string.Equals(argument.KeywordName, "metaclass", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "class(..., metaclass=...) is not supported by Lython.",
                    span);
            }
        }
    }
}

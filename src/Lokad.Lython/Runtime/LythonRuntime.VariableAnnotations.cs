using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void InitializeModuleAnnotations(IReadOnlyList<StatementSyntax> statements, ExecutionContext context)
    {
        if (context.Variables.ContainsKey("__annotations__") || !ContainsAnnotation(statements)) return;
        var span = statements[0].Span;
        var annotations = new PyDict(context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(annotations, annotations.CommittedStorageBytes, span);
        context.Variables["__annotations__"] = annotations;

        static bool ContainsAnnotation(IReadOnlyList<StatementSyntax> body)
            => body.Any(statement => statement is AnnotatedAssignmentStatementSyntax ||
                statement is not (FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax) &&
                StatementSyntaxTraversal.EnumerateChildBodies(statement).Any(ContainsAnnotation));
    }

    private static async ValueTask StoreModuleAnnotationAsync(LoweredAnnotatedAssignmentStatement statement,
        ExecutionContext context, bool asynchronous)
    {
        if (context.ParentContext is not null || context.IsClassBody) return;
        if (context.PostponedAnnotations && !statement.Assignment.IsSimple) return;
        var annotation = context.PostponedAnnotations ? CreatePostponedAnnotationValue(statement.Annotation.Syntax, context)
            : await EvaluateTypeExpressionAsync(statement.Annotation, context, asynchronous).ConfigureAwait(false);
        if (!statement.Assignment.IsSimple || statement.Assignment.Target is not NameAssignmentTargetSyntax name) return;
        var annotations = ResolveName("__annotations__", statement.Span, context);
        await StoreAnnotationEntryAsync(annotations, name.Name, annotation, context, statement.Span, asynchronous).ConfigureAwait(false);
    }

    private static PyString CreatePostponedAnnotationValue(ExpressionSyntax expression, ExecutionContext context)
    {
        var text = expression.PostponedAnnotationText ?? throw RuntimeErrors.Runtime("Postponed annotation metadata is unavailable.", expression.Span);
        var result = PyString.FromString(text, context.MemoryGovernor, expression.Span);
        context.Services.State.CallTemporaries.TrackFreshString(result, expression.Span);
        context.ObserveString(result, expression.Span);
        return result;
    }

    private static async ValueTask StoreAnnotationEntryAsync(object annotations, string name, object annotation,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var key = PyString.FromString(name, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshString(key, span);
        context.ObserveString(key, span);
        if (annotations is PyDict dictionary && !dictionary.ContainsKey(key))
            context.ObserveCollectionCount(dictionary.Count + 1, span);
        if (asynchronous)
            await SetHeaderSubscriptAsync(annotations, key, annotation, span, context).ConfigureAwait(false);
        else SetSubscriptValue(annotations, key, annotation, span, context);
        if (annotations is PyDict stored)
            context.Services.State.CallTemporaries.TrackGrowth(stored, stored.CommittedStorageBytes, span);
    }

    private static async ValueTask EvaluateAnnotationTargetReadsAsync(LoweredAnnotatedAssignmentStatement statement,
        ExecutionContext context, bool asynchronous)
    {
        foreach (var read in statement.Target.Reads.Values)
            await EvaluateTypeExpressionAsync(read, context, asynchronous).ConfigureAwait(false);
    }
}

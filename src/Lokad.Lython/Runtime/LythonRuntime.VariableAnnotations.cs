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
        StoreName("__annotations__", annotations, context, span);

        static bool ContainsAnnotation(IReadOnlyList<StatementSyntax> body)
            => body.Any(statement => statement is AnnotatedAssignmentStatementSyntax ||
                statement is not (FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax) &&
                StatementSyntaxTraversal.EnumerateChildBodies(statement).Any(ContainsAnnotation));
    }

    private static async ValueTask StoreModuleAnnotationAsync(LoweredAnnotatedAssignmentStatement statement,
        ExecutionContext context, bool asynchronous)
    {
        if (context.ParentContext is not null || context.IsClassBody || context.PostponedAnnotations) return;
        var annotation = await EvaluateTypeExpressionAsync(statement.Annotation, context, asynchronous).ConfigureAwait(false);
        if (!statement.Assignment.IsSimple || statement.Assignment.Target is not NameAssignmentTargetSyntax name) return;
        var annotations = ResolveName("__annotations__", statement.Span, context);
        var key = PyString.FromString(name.Name, context.MemoryGovernor, statement.Span);
        context.Services.State.CallTemporaries.TrackFreshString(key, statement.Span);
        if (annotations is PyDict dictionary && !dictionary.ContainsKey(key))
            context.ObserveCollectionCount(dictionary.Count + 1, statement.Span);
        if (asynchronous)
            await SetHeaderSubscriptAsync(annotations, key, annotation, statement.Span, context).ConfigureAwait(false);
        else SetSubscriptValue(annotations, key, annotation, statement.Span, context);
        if (annotations is PyDict stored)
            context.Services.State.CallTemporaries.TrackGrowth(stored, stored.CommittedStorageBytes, statement.Span);
    }

    private static async ValueTask EvaluateAnnotationTargetReadsAsync(LoweredAnnotatedAssignmentStatement statement,
        ExecutionContext context, bool asynchronous)
    {
        foreach (var read in statement.Target.Reads.Values)
            await EvaluateTypeExpressionAsync(read, context, asynchronous).ConfigureAwait(false);
    }
}

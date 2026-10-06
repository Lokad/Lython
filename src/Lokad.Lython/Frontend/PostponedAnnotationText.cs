using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Frontend;

internal sealed class AnnotationTextLimitException(LythonSourceSpan span) : Exception
{
    public LythonSourceSpan Span { get; } = span;
}

// Python 3.13 postponed annotations use the canonical expression spelling from
// Python/ast_unparse.c, which differs from both source text and ast.unparse().
// Source spans recover lexical names before class-private binding mangling.
internal static class PostponedAnnotationText
{
    internal const int MaximumCharacters = 8_000_000;

    public static IEnumerable<LythonDiagnostic> Validate(ScriptSyntax script)
    {
        var beginning = true;
        var postponed = false;
        var permitted = new HashSet<StatementSyntax>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < script.Statements.Count; i++)
        {
            var statement = script.Statements[i];
            if (i == 0 && statement is ExpressionStatementSyntax leading)
            {
                var expression = leading.Expression;
                while (expression is ParenthesizedExpressionSyntax grouped) expression = grouped.Inner;
                if (expression is StringLiteralExpressionSyntax) continue;
            }
            if (statement is ImportStatementSyntax { ModuleName: "__future__", ImportedMembers: not null } import)
            {
                if (beginning) permitted.Add(statement);
                postponed |= import.ImportedMembers.Any(member => member.Name == "annotations");
            }
            else beginning = false;
        }
        var statements = new Stack<StatementSyntax>(script.Statements);
        while (statements.TryPop(out var statement))
        {
            if (statement is ImportStatementSyntax { ModuleName: "__future__", ImportedMembers: not null } && !permitted.Contains(statement))
                yield return new LythonDiagnostic("LA1100", "Future imports must occur at the beginning of the module.", LythonDiagnosticSeverity.Error, statement.Span);
            if (postponed)
            {
                var annotations = statement switch
                {
                    FunctionDefinitionStatementSyntax definition => definition.Parameters.Where(p => p.Annotation is not null)
                        .Select(p => p.Annotation!).Concat(definition.ReturnAnnotation is null ? [] : new[] { definition.ReturnAnnotation }),
                    AnnotatedAssignmentStatementSyntax assignment => new[] { assignment.Annotation },
                    _ => Enumerable.Empty<ExpressionSyntax>(),
                };
                foreach (var annotation in annotations)
                {
                    var expressions = new Stack<ExpressionSyntax>();
                    expressions.Push(annotation);
                    while (expressions.TryPop(out var expression))
                    {
                        if (expression is AssignmentExpressionSyntax)
                            yield return new LythonDiagnostic("LA1100", "Assignment expressions cannot be used within postponed annotations.", LythonDiagnosticSeverity.Error, expression.Span);
                        // A lambda's body is its own scope; only its defaults belong
                        // to the containing annotation scope.
                        if (expression is LambdaExpressionSyntax lambda)
                        {
                            foreach (var parameter in lambda.Parameters)
                                if (parameter.DefaultValue is not null) expressions.Push(parameter.DefaultValue);
                        }
                        else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression)) expressions.Push(child);
                    }
                }
            }
            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                foreach (var child in body) statements.Push(child);
        }
    }

    public static void Attach(ScriptSyntax script, string source)
    {
        if (!script.Statements.OfType<ImportStatementSyntax>().Any(import => import.ModuleName == "__future__" &&
            import.ImportedMembers?.Any(member => member.Name == "annotations") == true)) return;
        var remaining = MaximumCharacters;
        var statements = new Stack<StatementSyntax>(script.Statements.Reverse());
        while (statements.TryPop(out var statement))
        {
            if (statement is FunctionDefinitionStatementSyntax definition)
            {
                foreach (var parameter in definition.Parameters)
                    if (parameter.Annotation is not null) Store(parameter.Annotation);
                if (definition.ReturnAnnotation is not null) Store(definition.ReturnAnnotation);
            }
            else if (statement is AnnotatedAssignmentStatementSyntax assignment) Store(assignment.Annotation);
            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                for (var i = body.Count - 1; i >= 0; i--) statements.Push(body[i]);
        }

        void Store(ExpressionSyntax expression)
        {
            if (expression.PostponedAnnotationText is not null) return;
            expression.PostponedAnnotationText = new Writer(source, remaining).Format(expression);
            remaining -= expression.PostponedAnnotationText.Length;
        }
    }

    // Use an explicit work stack: long flat operator chains must not consume
    // the CLR stack while their canonical annotation spelling is constructed.
    private sealed class Writer(string source, int maximum)
    {
        private readonly record struct Fragment(ExpressionSyntax? Expression, string? Text, int Minimum = 1);
        private readonly StringBuilder _text = new();
        private readonly Stack<Fragment> _pending = new();
        private LythonSourceSpan _span = new(0, 0, 0, 0);
        private static Fragment E(ExpressionSyntax expression, int minimum = 1) => new(expression, null, minimum);
        private static Fragment T(string text) => new(null, text);

        public string Format(ExpressionSyntax expression, int minimum = 1)
        {
            _span = expression.Span;
            _pending.Push(E(expression, minimum));
            while (_pending.TryPop(out var fragment))
            {
                if (fragment.Text is not null)
                {
                    if (fragment.Text.Length > maximum - _text.Length) throw new AnnotationTextLimitException(_span);
                    _text.Append(fragment.Text);
                }
                else Emit(fragment.Expression!, fragment.Minimum);
            }
            return _text.ToString();
        }

        private void Queue(IEnumerable<Fragment> parts)
        {
            foreach (var part in parts.Reverse()) _pending.Push(part);
        }
        private void Queue(params Fragment[] parts) => Queue((IEnumerable<Fragment>)parts);
        private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized) expression = parenthesized.Inner;
            return expression;
        }

        private string OriginalName(LythonSourceSpan span, bool last = false)
        {
            var start = span.Start;
            var end = span.Start + span.Length;
            if (last)
            {
                start = end;
                while (start > span.Start && IsIdentifierCharacter(source[start - 1])) start--;
            }
            return source[start..end].Normalize(NormalizationForm.FormKC);
        }
        private static bool IsIdentifierCharacter(char character)
            => character == '_' || char.IsLetterOrDigit(character) || char.GetUnicodeCategory(character) is
                UnicodeCategory.LetterNumber or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation;

        private void Emit(ExpressionSyntax expression, int minimum)
        {
            expression = Unwrap(expression);
            var priority = Priority(expression);
            var grouped = priority < minimum;
            if (grouped) _pending.Push(T(")"));
            switch (expression)
            {
                case IdentifierExpressionSyntax identifier: Queue(T(OriginalName(identifier.Span))); break;
                case StringLiteralExpressionSyntax text: Queue(T(Quote(text.Value))); break;
                case BytesLiteralExpressionSyntax bytes: Queue(T(Bytes(bytes.Value))); break;
                case IntegerLiteralExpressionSyntax integer: Queue(T(PyNumberOps.ParseInteger(integer.ValueText).ToString(CultureInfo.InvariantCulture))); break;
                case FloatLiteralExpressionSyntax number: Queue(T(Float(number.ValueText))); break;
                case ImaginaryLiteralExpressionSyntax imaginary: Queue(T(Imaginary(imaginary.ValueText))); break;
                case BooleanLiteralExpressionSyntax boolean: Queue(T(boolean.Value ? "True" : "False")); break;
                case NoneLiteralExpressionSyntax: Queue(T("None")); break;
                case EllipsisLiteralExpressionSyntax: Queue(T("...")); break;
                case UnpackedTypeExpressionSyntax unpacked: Queue(T("*"), E(unpacked.Value, 7)); break;
                case BinaryExpressionSyntax binary when binary.Operator is BinaryOperatorSyntax.And or BinaryOperatorSyntax.Or:
                    var values = new List<ExpressionSyntax>();
                    var booleanParts = new Stack<ExpressionSyntax>();
                    booleanParts.Push(binary);
                    while (booleanParts.TryPop(out var value))
                    {
                        if (value is BinaryExpressionSyntax nested && nested.Operator == binary.Operator)
                        { booleanParts.Push(nested.Right); booleanParts.Push(nested.Left); }
                        else values.Add(value);
                    }
                    Queue(Separated(values.Select(value => E(value, priority + 1)), Operator(binary.Operator)));
                    break;
                case BinaryExpressionSyntax binary:
                    var rightAssociative = binary.Operator == BinaryOperatorSyntax.Power;
                    var isComparison = priority == 6;
                    Queue(E(binary.Left, priority + (rightAssociative || isComparison ? 1 : 0)), T(Operator(binary.Operator)),
                        E(binary.Right, priority + (!rightAssociative || isComparison ? 1 : 0)));
                    break;
                case UnaryExpressionSyntax unary:
                    Queue(T(unary.Operator switch { UnaryOperatorSyntax.Not => "not ", UnaryOperatorSyntax.Plus => "+", UnaryOperatorSyntax.Minus => "-", _ => "~" }),
                        E(unary.Operand, priority));
                    break;
                case ChainedComparisonExpressionSyntax comparison:
                    var compared = new List<Fragment> { E(comparison.Operands[0], 7) };
                    for (var i = 0; i < comparison.Operators.Count; i++)
                    { compared.Add(T(Operator(comparison.Operators[i]))); compared.Add(E(comparison.Operands[i + 1], 7)); }
                    Queue(compared); break;
                case ConditionalExpressionSyntax conditional:
                    Queue(E(conditional.Consequent, 2), T(" if "), E(conditional.Condition, 2), T(" else "), E(conditional.Alternative)); break;
                case MemberExpressionSyntax member:
                    Queue(E(member.Target, 16), T(Unwrap(member.Target) is IntegerLiteralExpressionSyntax ? " ." : "."), T(OriginalName(member.Span, last: true))); break;
                case CallExpressionSyntax call:
                    var arguments = call.Arguments.Where(argument => argument.Kind is CallArgumentKind.Positional or CallArgumentKind.StarredList)
                        .Concat(call.Arguments.Where(argument => argument.Kind is CallArgumentKind.Keyword or CallArgumentKind.StarredDictionary)).ToArray();
                    if (arguments.Length == 1 && arguments[0].Kind == CallArgumentKind.Positional && Unwrap(arguments[0].Expression) is GeneratorExpressionSyntax generator)
                        Queue(E(call.Target, 16), E(generator));
                    else Queue(new[] { E(call.Target, 16), T("(") }.Concat(Separated(arguments.Select(CallArgument), ", ")).Append(T(")")));
                    break;
                case SubscriptExpressionSyntax subscript: Queue(E(subscript.Target, 16), T("["), E(subscript.Index, 0), T("]")); break;
                case SliceExpressionSyntax slice:
                    Queue(new[] { E(slice.Target, 16), T("[") }.Concat(Slice(slice.Start, slice.End, slice.Step)).Append(T("]"))); break;
                case SliceValueExpressionSyntax slice: Queue(Slice(slice.Start, slice.End, slice.Step)); break;
                case TupleLiteralExpressionSyntax tuple:
                    if (tuple.Items.Count == 0) Queue(T("()"));
                    else Queue(DisplayItems(tuple.Items).Concat(tuple.Items.Count == 1 ? new[] { T(",") } : []));
                    break;
                case ListLiteralExpressionSyntax list: Queue(new[] { T("[") }.Concat(DisplayItems(list.Items)).Append(T("]"))); break;
                case SetLiteralExpressionSyntax set: Queue(new[] { T("{") }.Concat(DisplayItems(set.Items)).Append(T("}"))); break;
                case DictLiteralExpressionSyntax dictionary:
                    Queue(new[] { T("{") }.Concat(Separated(dictionary.Items.Select(DictionaryItem), ", ")).Append(T("}"))); break;
                case ListComprehensionExpressionSyntax comprehension: Queue(Comprehension("[", "]", [E(comprehension.ItemExpression)], comprehension.Clauses)); break;
                case SetComprehensionExpressionSyntax comprehension: Queue(Comprehension("{", "}", [E(comprehension.ItemExpression)], comprehension.Clauses)); break;
                case DictComprehensionExpressionSyntax comprehension: Queue(Comprehension("{", "}", [E(comprehension.KeyExpression), T(": "), E(comprehension.ValueExpression)], comprehension.Clauses)); break;
                case GeneratorExpressionSyntax comprehension: Queue(Comprehension("(", ")", [E(comprehension.ItemExpression)], comprehension.Clauses)); break;
                case LambdaExpressionSyntax lambda:
                    Queue(new[] { T(lambda.Parameters.Any(parameter => parameter.Kind is FunctionParameterKind.Positional or FunctionParameterKind.PositionalOnly) ? "lambda " : "lambda") }
                        .Concat(LambdaParameters(lambda)).Concat([T(": "), E(lambda.Body)])); break;
                case AssignmentExpressionSyntax assignment:
                    var raw = source[assignment.Span.Start..(assignment.Span.Start + assignment.Span.Length)];
                    var nameLength = 0;
                    while (nameLength < raw.Length && IsIdentifierCharacter(raw[nameLength])) nameLength++;
                    Queue(T(raw[..nameLength].Normalize(NormalizationForm.FormKC)), T(" := "), E(assignment.Expression, 16)); break;
                case YieldExpressionSyntax yielded:
                    if (yielded.Value is null) Queue(T("(yield)"));
                    else Queue(T(yielded.Delegated ? "(yield from " : "(yield "), E(yielded.Value), T(")"));
                    break;
                case FormattedStringExpressionSyntax formatted: Queue(T("f" + Quote(FormattedBody(formatted.Parts)))); break;
                default: throw new InvalidOperationException($"Unknown postponed annotation syntax: {expression.GetType().Name}.");
            }
            if (grouped) _pending.Push(T("("));
        }

        private static int Priority(ExpressionSyntax expression) => expression switch
        {
            TupleLiteralExpressionSyntax { Items.Count: > 0 } => 0,
            AssignmentExpressionSyntax => 0,
            ConditionalExpressionSyntax or LambdaExpressionSyntax => 1,
            BinaryExpressionSyntax binary => binary.Operator switch
            {
                BinaryOperatorSyntax.Or => 2, BinaryOperatorSyntax.And => 3,
                BinaryOperatorSyntax.BitwiseOr => 7, BinaryOperatorSyntax.BitwiseXor => 8, BinaryOperatorSyntax.BitwiseAnd => 9,
                BinaryOperatorSyntax.LeftShift or BinaryOperatorSyntax.RightShift => 10,
                BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract => 11,
                BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.MatrixMultiply or BinaryOperatorSyntax.Divide or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo => 12,
                BinaryOperatorSyntax.Power => 14, _ => 6
            },
            UnaryExpressionSyntax { Operator: UnaryOperatorSyntax.Not } => 5,
            UnaryExpressionSyntax => 13,
            ChainedComparisonExpressionSyntax => 6,
            UnpackedTypeExpressionSyntax => 7,
            _ => 16
        };

        private static string Operator(BinaryOperatorSyntax op) => " " + (op switch
        {
            BinaryOperatorSyntax.Or => "or", BinaryOperatorSyntax.And => "and", BinaryOperatorSyntax.Add => "+", BinaryOperatorSyntax.Subtract => "-",
            BinaryOperatorSyntax.Multiply => "*", BinaryOperatorSyntax.MatrixMultiply => "@", BinaryOperatorSyntax.Divide => "/", BinaryOperatorSyntax.FloorDivide => "//",
            BinaryOperatorSyntax.Modulo => "%", BinaryOperatorSyntax.Power => "**", BinaryOperatorSyntax.BitwiseOr => "|", BinaryOperatorSyntax.BitwiseXor => "^",
            BinaryOperatorSyntax.BitwiseAnd => "&", BinaryOperatorSyntax.LeftShift => "<<", BinaryOperatorSyntax.RightShift => ">>", BinaryOperatorSyntax.Equal => "==",
            BinaryOperatorSyntax.NotEqual => "!=", BinaryOperatorSyntax.Less => "<", BinaryOperatorSyntax.LessEqual => "<=", BinaryOperatorSyntax.Greater => ">",
            BinaryOperatorSyntax.GreaterEqual => ">=", BinaryOperatorSyntax.Is => "is", BinaryOperatorSyntax.IsNot => "is not", BinaryOperatorSyntax.In => "in", BinaryOperatorSyntax.NotIn => "not in",
            _ => throw new InvalidOperationException("Unknown annotation operator.")
        }) + " ";

        private static IEnumerable<Fragment> Separated(IEnumerable<Fragment> items, string separator)
        {
            var first = true;
            foreach (var item in items)
            {
                if (!first) yield return T(separator);
                yield return item;
                first = false;
            }
        }
        private static IEnumerable<Fragment> Separated(IEnumerable<IEnumerable<Fragment>> items, string separator)
        {
            var first = true;
            foreach (var item in items)
            {
                if (!first) yield return T(separator);
                foreach (var fragment in item) yield return fragment;
                first = false;
            }
        }
        private static IEnumerable<Fragment> DisplayItems(IReadOnlyList<CollectionDisplayItemSyntax> items)
            => Separated(items.Select(item => item.IsUnpacking ? new[] { T("*"), E(item.Expression, 7) } : [E(item.Expression)]), ", ");
        private static IEnumerable<Fragment> CallArgument(CallArgumentSyntax argument)
        {
            if (argument.Kind == CallArgumentKind.Keyword) yield return T(argument.KeywordName + "=");
            else if (argument.Kind == CallArgumentKind.StarredDictionary) yield return T("**");
            else if (argument.Kind == CallArgumentKind.StarredList) yield return T("*");
            yield return E(argument.Expression, argument.Kind == CallArgumentKind.StarredList ? 7 : 1);
        }
        private static IEnumerable<Fragment> DictionaryItem(DictionaryDisplayItemSyntax item)
        {
            if (item.IsUnpacking) { yield return T("**"); yield return E(item.Value, 7); }
            else { yield return E(item.Key); yield return T(": "); yield return E(item.Value); }
        }
        private static IEnumerable<Fragment> Slice(ExpressionSyntax? start, ExpressionSyntax? end, ExpressionSyntax? step)
        {
            if (start is not null) yield return E(start);
            yield return T(":");
            if (end is not null) yield return E(end);
            if (step is not null) { yield return T(":"); yield return E(step); }
        }

        private IEnumerable<Fragment> Comprehension(string open, string close, Fragment[] value, IReadOnlyList<ComprehensionClauseSyntax> clauses)
        {
            yield return T(open);
            foreach (var fragment in value) yield return fragment;
            foreach (var clause in clauses)
            {
                yield return T(" for ");
                foreach (var fragment in LoopTarget(clause.Target)) yield return fragment;
                yield return T(" in "); yield return E(clause.Iterable, 2);
                if (clause.Condition is not null)
                    foreach (var condition in Conditions(clause.Condition)) { yield return T(" if "); yield return E(condition, 2); }
            }
            yield return T(close);
        }

        private IEnumerable<ExpressionSyntax> Conditions(ExpressionSyntax root)
        {
            var pending = new Stack<ExpressionSyntax>();
            pending.Push(root);
            while (pending.TryPop(out var condition))
            {
                if (condition is BinaryExpressionSyntax { Operator: BinaryOperatorSyntax.And } joined &&
                    FormattedStringTokenization.Read(source[(joined.Left.Span.Start + joined.Left.Span.Length)..joined.Right.Span.Start])
                        .Tokens.Any(token => token.Token == Token.If))
                { pending.Push(joined.Right); pending.Push(joined.Left); }
                else yield return condition;
            }
        }

        private IEnumerable<Fragment> LoopTarget(LoopTargetSyntax target, bool nested = false)
        {
            switch (target)
            {
                case LoopStoreTargetSyntax store:
                    if (store.IsStarred) yield return T("*");
                    foreach (var fragment in AssignmentTarget(store.Target)) yield return fragment;
                    break;
                case LoopTupleTargetSyntax tuple:
                    var list = tuple.OriginalSpan is { } span && source[span.Start] == '[';
                    if (nested || list) yield return T(list ? "[" : "(");
                    foreach (var fragment in Separated(tuple.Items.Select(item => LoopTarget(item, nested: true)), ", ")) yield return fragment;
                    if (tuple.Items.Count == 1) yield return T(",");
                    if (nested || list) yield return T(list ? "]" : ")");
                    break;
                case LoopNameTargetSyntax name: yield return T(name.OriginalSpan is { } nameSpan ? OriginalName(nameSpan) : name.Name); break;
                case LoopStarredTargetSyntax star: yield return T("*" + (star.OriginalSpan is { } starSpan ? OriginalName(starSpan) : star.Name)); break;
                default: throw new InvalidOperationException("Unknown annotation comprehension target.");
            }
        }
        private IEnumerable<Fragment> AssignmentTarget(AssignmentTargetSyntax target)
        {
            switch (target)
            {
                case NameAssignmentTargetSyntax name: yield return T(OriginalName(name.Span, last: true)); break;
                case MemberAssignmentTargetSyntax member:
                    yield return E(member.Target, 16); yield return T("." + OriginalName(member.Span, last: true)); break;
                case SubscriptAssignmentTargetSyntax item:
                    yield return E(item.Target, 16); yield return T("["); yield return E(item.Index, 0); yield return T("]"); break;
                case SliceAssignmentTargetSyntax slice:
                    yield return E(slice.Target, 16); yield return T("[");
                    foreach (var fragment in Slice(slice.Start, slice.End, slice.Step)) yield return fragment;
                    yield return T("]"); break;
                case UnpackingAssignmentTargetGroupSyntax group:
                    yield return T("(");
                    foreach (var fragment in Separated(group.Targets.Select(target =>
                        (target.IsStarred ? new[] { T("*") } : Array.Empty<Fragment>())
                            .Concat(AssignmentTarget(AssignmentTargetFacts.FromUnpacking(target)))), ", ")) yield return fragment;
                    if (group.Targets.Count == 1) yield return T(",");
                    yield return T(")"); break;
                default: throw new InvalidOperationException("Unknown annotation store target.");
            }
        }

        private IEnumerable<Fragment> LambdaParameters(LambdaExpressionSyntax lambda)
        {
            var raw = source.Substring(lambda.Span.Start, lambda.Span.Length);
            var tokens = FormattedStringTokenization.Read(raw);
            var position = 1;
            var parts = new List<IEnumerable<Fragment>>();
            var hasStar = false;
            for (var i = 0; i < lambda.Parameters.Count; i++)
            {
                var parameter = lambda.Parameters[i];
                while (position < tokens.Tokens.Count && tokens.Tokens[position].Token is not (Token.Identifier or Token.Match or Token.Case)) position++;
                var name = tokens.GetString(position++).Normalize(NormalizationForm.FormKC);
                if (parameter.Kind == FunctionParameterKind.KeywordOnly && !hasStar) { parts.Add([T("*")]); hasStar = true; }
                var prefix = parameter.Kind == FunctionParameterKind.VariadicList ? "*" : parameter.Kind == FunctionParameterKind.VariadicDictionary ? "**" : "";
                if (parameter.Kind == FunctionParameterKind.VariadicList) hasStar = true;
                var item = new List<Fragment> { T(prefix + name) };
                if (parameter.DefaultValue is not null)
                {
                    item.Add(T("=")); item.Add(E(parameter.DefaultValue));
                    var end = parameter.DefaultValue.Span.Start + parameter.DefaultValue.Span.Length - lambda.Span.Start;
                    while (position < tokens.Tokens.Count && tokens.Tokens[position].Start < end) position++;
                }
                parts.Add(item);
                if (parameter.Kind == FunctionParameterKind.PositionalOnly && (i + 1 == lambda.Parameters.Count || lambda.Parameters[i + 1].Kind != FunctionParameterKind.PositionalOnly)) parts.Add([T("/")]);
            }
            return Separated(parts, ", ");
        }

        private string FormattedBody(IReadOnlyList<FormattedStringPartSyntax> parts)
        {
            var builder = new StringBuilder();
            foreach (var part in parts)
            {
                if (part is FormattedStringTextPartSyntax text) builder.Append(text.Text.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));
                else if (part is FormattedStringExpressionPartSyntax field)
                {
                    var expression = new Writer(source, maximum).Format(field.Expression, 2);
                    builder.Append(expression.StartsWith('{') ? "{ " : "{").Append(expression);
                    if (field.Conversion is { } conversion) builder.Append('!').Append(conversion);
                    if (field.FormatSpecifierParts is not null) builder.Append(':').Append(FormattedBody(field.FormatSpecifierParts));
                    else if (field.FormatSpecifier is not null) builder.Append(':').Append(field.FormatSpecifier);
                    builder.Append('}');
                }
                if (builder.Length > maximum) throw new AnnotationTextLimitException(_span);
            }
            return builder.ToString();
        }

        private static string Float(string source)
            => PyNumberOps.RenderFloat(double.Parse(source.Replace("_", "", StringComparison.Ordinal), CultureInfo.InvariantCulture)).Replace("inf", "1e309", StringComparison.Ordinal);
        private static string Imaginary(string source)
        {
            var value = double.Parse(source[..^1].Replace("_", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
            var text = PyNumberOps.RenderFloat(value).Replace("inf", "1e309", StringComparison.Ordinal);
            return (text.EndsWith(".0", StringComparison.Ordinal) ? text[..^2] : text) + "j";
        }
        private static string Bytes(byte[] bytes)
        {
            var text = new StringBuilder();
            var quote = bytes.Contains((byte)'\'') && !bytes.Contains((byte)'"') ? '"' : '\'';
            text.Append('b').Append(quote);
            foreach (var value in bytes)
                text.Append(value == (byte)quote ? "\\" + quote : value switch
                {
                    (byte)'\\' => "\\\\", 9 => "\\t", 10 => "\\n", 13 => "\\r",
                    >= 32 and <= 126 => ((char)value).ToString(), _ => "\\x" + value.ToString("x2", CultureInfo.InvariantCulture)
                });
            return text.Append(quote).ToString();
        }
        private static string Quote(string value)
        {
            var quote = value.Contains('\'') && !value.Contains('"') ? '"' : '\'';
            var builder = new StringBuilder().Append(quote);
            foreach (var rune in value.EnumerateRunes())
            {
                if (rune.Value == quote || rune.Value == '\\') builder.Append('\\').Append(rune.ToString());
                else if (rune.Value is 9 or 10 or 13) builder.Append(rune.Value == 9 ? "\\t" : rune.Value == 10 ? "\\n" : "\\r");
                else if (rune.Value != 32 && Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or
                    UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned or UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                    builder.Append(rune.Value <= 255 ? "\\x" : rune.Value <= 65535 ? "\\u" : "\\U")
                        .Append(rune.Value.ToString(rune.Value <= 255 ? "x2" : rune.Value <= 65535 ? "x4" : "x8", CultureInfo.InvariantCulture));
                else builder.Append(rune.ToString());
            }
            return builder.Append(quote).ToString();
        }
    }
}

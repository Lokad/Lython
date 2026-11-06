using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// One compile-owned representation for executable and lowered consumers. All
// capture slots are private until the entire pattern succeeds. Validated OR
// branches bind the same names, so a successful branch overwrites every slot it
// owns; failed branches need neither a binding dictionary nor a rollback copy.
internal sealed record LoweredPatternBinding(string Name);

internal sealed record LoweredRootPattern(LoweredPattern Root, LoweredPatternBinding[] Bindings)
{
    public int StorageCount => checked(Bindings.Length + Root.WorkspaceCount);
}

internal abstract record LoweredPattern(PatternSyntax Syntax, int WorkspaceCount)
{
    public LythonSourceSpan Span => Syntax.Span;
}

internal sealed record LoweredValuePattern(MatchValuePatternSyntax Value, LoweredExpression Expression)
    : LoweredPattern(Value, 0);
internal sealed record LoweredSingletonPattern(MatchSingletonPatternSyntax Singleton)
    : LoweredPattern(Singleton, 0);
internal sealed record LoweredCapturePattern(PatternSyntax Capture, int Slot)
    : LoweredPattern(Capture, 0);
internal sealed record LoweredWildcardPattern(PatternSyntax Wildcard) : LoweredPattern(Wildcard, 0);
internal sealed record LoweredAsPattern(MatchAsPatternSyntax Alias, LoweredPattern Pattern, int Slot)
    : LoweredPattern(Alias, Pattern.WorkspaceCount);
internal sealed record LoweredOrPattern(MatchOrPatternSyntax Alternatives, LoweredPattern[] Patterns)
    : LoweredPattern(Alternatives, LoweredPatternPreparation.MaximumWorkspace(Patterns));
internal sealed record LoweredSequencePattern(PatternSyntax Sequence, LoweredPattern[] Items, int StarIndex)
    : LoweredPattern(Sequence, checked(Items.Length + LoweredPatternPreparation.MaximumWorkspace(Items)));
internal sealed record LoweredMappingItem(LoweredExpression Key, LoweredPattern Pattern);
internal sealed record LoweredMappingPattern(MatchMappingPatternSyntax Mapping, LoweredMappingItem[] Items,
    int RestSlot, object[]? ConstantKeys)
    : LoweredPattern(Mapping, checked((ConstantKeys is null ? 2 : 1) * Items.Length
        + LoweredPatternPreparation.MaximumWorkspace(Items.Select(item => item.Pattern))));
internal sealed record LoweredClassKeyword(string Name, LoweredPattern Pattern);
internal sealed record LoweredClassPattern(MatchClassPatternSyntax Class, LoweredExpression Expression,
    LoweredPattern[] PositionalPatterns, LoweredClassKeyword[] KeywordPatterns)
    : LoweredPattern(Class, Math.Max(LoweredPatternPreparation.MaximumWorkspace(PositionalPatterns),
        LoweredPatternPreparation.MaximumWorkspace(KeywordPatterns.Select(item => item.Pattern))));

internal static class LoweredPatternPreparation
{
    internal static int MaximumWorkspace(IEnumerable<LoweredPattern> patterns)
        => patterns.Select(pattern => pattern.WorkspaceCount).DefaultIfEmpty(0).Max();

    internal static LoweredRootPattern Prepare(PatternSyntax syntax)
    {
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var bindings = new List<LoweredPatternBinding>();
        Collect(syntax);
        return new(Lower(syntax), bindings.ToArray());

        void Add(PatternSyntax pattern, string? name)
        {
            if (name is null) return;
            var source = pattern.OriginalBindingName ?? name;
            indexes.Add(source, bindings.Count);
            bindings.Add(new(name));
        }

        void Collect(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case MatchCapturePatternSyntax capture: Add(pattern, capture.Name); break;
                case MatchStarPatternSyntax star: Add(pattern, star.Name); break;
                case MatchAsPatternSyntax alias: Collect(alias.Pattern); Add(pattern, alias.Name); break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var child in sequence.Items) Collect(child);
                    break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var child in mapping.Items) Collect(child.Pattern);
                    Add(pattern, mapping.RestName);
                    break;
                case MatchClassPatternSyntax type:
                    foreach (var child in type.PositionalPatterns) Collect(child);
                    foreach (var child in type.KeywordPatterns) Collect(child.Pattern);
                    break;
                case MatchOrPatternSyntax alternatives: Collect(alternatives.Patterns[0]); break;
            }
        }

        int Slot(PatternSyntax pattern, string? name)
            => name is null ? -1 : indexes[pattern.OriginalBindingName ?? name];

        LoweredSequencePattern Sequence(PatternSyntax pattern, IEnumerable<PatternSyntax> children)
        {
            var items = children.Select(Lower).ToArray();
            var star = Array.FindIndex(items, item => item.Syntax is MatchStarPatternSyntax);
            return new(pattern, items, star);
        }

        LoweredPattern Lower(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case MatchValuePatternSyntax value:
                    return new LoweredValuePattern(value, LoweredScript.LowerStandaloneExpression(value.Expression));
                case MatchSingletonPatternSyntax singleton: return new LoweredSingletonPattern(singleton);
                case MatchCapturePatternSyntax capture: return new LoweredCapturePattern(pattern, Slot(pattern, capture.Name));
                case MatchWildcardPatternSyntax: return new LoweredWildcardPattern(pattern);
                case MatchStarPatternSyntax star:
                    return star.Name is null ? new LoweredWildcardPattern(pattern) : new LoweredCapturePattern(pattern, Slot(pattern, star.Name));
                case MatchAsPatternSyntax alias: return new LoweredAsPattern(alias, Lower(alias.Pattern), Slot(pattern, alias.Name));
                case MatchOrPatternSyntax alternatives: return new LoweredOrPattern(alternatives, alternatives.Patterns.Select(Lower).ToArray());
                case MatchSequencePatternSyntax sequence: return Sequence(sequence, sequence.Items);
                case MatchMappingPatternSyntax mapping:
                    var items = mapping.Items.Select(item => new LoweredMappingItem(
                        LoweredScript.LowerStandaloneExpression(item.Key), Lower(item.Pattern))).ToArray();
                    var keys = new object[items.Length];
                    var unique = new HashSet<object>(PyValueComparer.Instance);
                    for (var i = 0; i < items.Length; i++)
                    {
                        object? key = items[i].Key switch
                        {
                            LoweredStringLiteralExpression text => text.Value,
                            LoweredBytesLiteralExpression bytes => bytes.Value,
                            LoweredIntegerLiteralExpression integer => integer.Value,
                            _ => null
                        };
                        // Only immutable, property-free keys are proven here.
                        // Dynamic keys retain Python's duplicate/get phases.
                        if (key is null || !unique.Add(key))
                            return new LoweredMappingPattern(mapping, items, Slot(pattern, mapping.RestName), null);
                        keys[i] = key;
                    }
                    return new LoweredMappingPattern(mapping, items, Slot(pattern, mapping.RestName), keys);
                case MatchClassPatternSyntax type:
                    return new LoweredClassPattern(type, LoweredScript.LowerStandaloneExpression(type.ClassExpression),
                        type.PositionalPatterns.Select(Lower).ToArray(),
                        type.KeywordPatterns.Select(item => new LoweredClassKeyword(item.Name, Lower(item.Pattern))).ToArray());
                default: throw new InvalidOperationException($"Unknown pattern type: {pattern.GetType().Name}");
            }
        }
    }

}

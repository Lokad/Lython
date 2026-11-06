using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinClassPatternTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["whole subject and identity", """
            values = [(bool, True), (bytes, b'ab'), (dict, {'a': 1}),
                      (float, 1.5), (int, 7), (list, [1, 2]),
                      (set, {1}), (str, 'ab'), (tuple, (1, 2))]
            for kind, source in values:
                match source:
                    case kind(value):
                        assert value is source
                    case _:
                        raise AssertionError('builtin did not match')
            source = [1, 2]
            match source:
                case list(value): value.append(3)
            print(source)
            """, "[1, 2, 3]\n"];
        yield return ["empty subjects and nested patterns", """
            for kind, source in [(list, []), (tuple, ()), (dict, {}), (set, set()), (str, ''), (bytes, b'')]:
                match source:
                    case kind(value): assert value is source
                    case _: raise AssertionError('empty did not match')
            match [1, [2, 3]]:
                case list([first, list([middle, last])]): print(first, middle, last)
            """, "1 2 3\n"];
        yield return ["excess positions and failed-type short circuit", """
            for kind, source in [(list, [1, 2]), (tuple, (1, 2)), (int, 7)]:
                try:
                    match source:
                        case kind(a, b): raise AssertionError('accepted excess positions')
                except TypeError as error:
                    print(error)
            match 42:
                case list(a, b): raise AssertionError('wrong type matched')
                case _: print('skip')
            """, "list() accepts 1 positional sub-pattern (2 given)\ntuple() accepts 1 positional sub-pattern (2 given)\nint() accepts 1 positional sub-pattern (2 given)\nskip\n"];
        yield return ["actual aliases and shadowed names", """
            Kind = list
            list = int
            match [10, 20]:
                case Kind([left, right]): print(left + right)
            match 7:
                case list(value): print(value)
            list = 17
            try:
                match []:
                    case list(): raise AssertionError('non-class matched')
            except TypeError: print('not a class')
            """, "30\n7\nnot a class\n"];
        yield return ["runtime class alias and lowered class body", """
            import pathlib
            Path = pathlib.Path
            match Path('/docs/guide.md'):
                case Path(name=name, suffix=suffix): print(name, suffix)
            class Owner:
                match [4, 5]:
                    case list([left, right]): total = left + right
            print(Owner.total)
            """, "guide.md .md\n9\n"];
        yield return ["zero positional builtin and invalid callable", """
            value = range(3)
            assert isinstance(value, range)
            match value:
                case range(): print('range')
            try:
                match value:
                    case range(item): pass
            except TypeError: print('no positions')
            def factory(): return list
            try:
                match []:
                    case factory(): pass
            except TypeError: print('not a class')
            """, "range\nno positions\nnot a class\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BuiltinPatternsUseActualTypesAndWholeSubjects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

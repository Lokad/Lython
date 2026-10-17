using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExpandedStringArgumentTests
{
    // Independent CPython 3.13 results, including stored expansion and widened facts.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "replace_tuple", "args=('a','b')\nprint('aa'.replace(*args))", "bb\n" },
        { "replace_list", "args=['a','b']\nprint('aa'.replace(*args))", "bb\n" },
        { "replace_count", "args=('a','b',1)\nprint('aaa'.replace(*args))", "baa\n" },
        { "replace_keyword_count", "options={\"count\":1}\nprint(\"aaa\".replace(\"a\",\"b\",**options))", "baa\n" },
        { "replace_keyword_bool", "options={\"count\":True}\nprint(\"aaa\".replace(\"a\",\"b\",**options))", "baa\n" },
        { "split", "args=(',',1)\nprint('a,b,c'.split(*args))", "['a', 'b,c']\n" },
        { "rsplit", "args=(',',1)\nprint('a,b,c'.rsplit(*args))", "['a,b', 'c']\n" },
        { "split_none", "args=(None,1)\nprint(' a b '.split(*args))", "['a', 'b ']\n" },
        { "strip", "args=(' ',)\nprint(' a '.strip(*args))", "a\n" },
        { "lstrip", "args=(' ',)\nprint(' a '.lstrip(*args))", "a \n" },
        { "rstrip", "args=(' ',)\nprint(' a '.rstrip(*args))", " a\n" },
        { "strip_none", "args=(None,)\nprint(' a '.strip(*args))", "a\n" },
        { "find", "args=('a',1,None)\nprint('ababa'.find(*args))", "2\n" },
        { "index", "args=('a',1,None)\nprint('ababa'.index(*args))", "2\n" },
        { "rfind", "args=('a',1,None)\nprint('ababa'.rfind(*args))", "4\n" },
        { "rindex", "args=('a',1,None)\nprint('ababa'.rindex(*args))", "4\n" },
        { "count", "args=('a',1,None)\nprint('ababa'.count(*args))", "2\n" },
        { "removeprefix", "args=('a',)\nprint('abc'.removeprefix(*args))", "bc\n" },
        { "removesuffix", "args=('c',)\nprint('abc'.removesuffix(*args))", "ab\n" },
        { "partition", "args=(':',)\nprint('a:b'.partition(*args))", "('a', ':', 'b')\n" },
        { "rpartition", "args=(':',)\nprint('a:b:c'.rpartition(*args))", "('a:b', ':', 'c')\n" },
        { "startswith_tuple", "args=(('z','a'),)\nprint('abc'.startswith(*args))", "True\n" },
        { "endswith_tuple", "args=(('z','c'),)\nprint('abc'.endswith(*args))", "True\n" },
        { "startswith_bounds", "args=('b',1,None)\nprint('abc'.startswith(*args))", "True\n" },
        { "join_list", "args=(['a','b'],)\nprint(':'.join(*args))", "a:b\n" },
        { "join_tuple", "args=(('a','b'),)\nprint(':'.join(*args))", "a:b\n" },
        { "join_string", "args=('ab',)\nprint(':'.join(*args))", "a:b\n" },
        { "format_map", "args=({'x':'value'},)\nprint('{x}'.format_map(*args))", "value\n" },
        { "literal_expansion", "print(\"aa\".replace(*(\"a\",\"b\")))", "bb\n" },
        { "literal_keyword_expansion", "print(\"aa\".replace(\"a\",\"b\",**{\"count\":1}))", "ba\n" },
        { "none_bound_alias", "start=None\nprint(\"abc\".find(\"b\",start))", "1\n" },
        { "unknown_expansion", "def values():return (\"a\",\"b\")\nprint(\"aa\".replace(*values()))", "bb\n" },
        { "mutable_effect", "args=[\"x\",\"y\"]\ndef change():args[0]=\"a\"\nchange()\nprint(\"aa\".replace(*args))", "yy\n" },
        { "possible_prefix_match", "args=((\"a\",1),)\nprint(\"abc\".startswith(*args))", "True\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExpandedValuesPreservePythonStringContracts(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}

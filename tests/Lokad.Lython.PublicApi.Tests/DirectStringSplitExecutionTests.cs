using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectStringSplitExecutionTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "unicode-whitespace", "print(' alpha\\tbeta\\n café\\u00a0😀\\u2009tail '.split())", "['alpha', 'beta', 'café', '😀', 'tail']\n" },
        { "empty-and-boundaries", "for text in ('',' ','\\t\\n','word',' word ',' a  b '):print(text.split())", "[]\n[]\n[]\n['word']\n['word']\n['a', 'b']\n" },
        { "fresh-results", "text='one two'\na=text.split()\nb=text.split()\na.append('three')\nprint(a,b,a is b,text)", "['one', 'two', 'three'] ['one', 'two'] False one two\n" },
        { "explicit-options", "text=' a  b c '\nprint(text.split(None),text.split(sep=None,maxsplit=1),text.split(' '),text.split(None,0))", "['a', 'b', 'c'] ['a', 'b c '] ['', 'a', '', 'b', 'c', ''] ['a  b c ']\n" },
        { "exposed", "method=' a b '.split\nprint(method(),method(None,1))", "['a', 'b'] ['a', 'b ']\n" },
        { "custom-receiver", "class Text:\n def split(self):return ['custom']\nprint(Text().split())", "['custom']\n" },
        { "expanded", "print(' a b '.split(*[],**{}))", "['a', 'b']\n" },
        { "receiver-once", "events=[]\ndef text():\n events.append('receiver')\n return ' a b '\nprint(text().split(),events)", "['a', 'b'] ['receiver']\n" },
        { "unicode-separator", "print('café😀tail😀'.split('😀'),'a<>b<><>c'.split('<>'))", "['café', 'tail', ''] ['a', 'b', '', 'c']\n" },
        { "explicit-none", "print(' a\\tb café '.split(None),'word'.split(None),''.split(','))", "['a', 'b', 'café'] ['word'] ['']\n" },
        { "separator-alias", "text='a,b'\na=text.split(',')\nb=text.split(',')\na.append('c')\nprint(a,b,text)", "['a', 'b', 'c'] ['a', 'b'] a,b\n" },
        { "separator-effects", "events=[]\ndef text():\n events.append('receiver')\n return 'a,b'\ndef sep():\n events.append('separator')\n return ','\nprint(text().split(sep()),events)", "['a', 'b'] ['receiver', 'separator']\n" },
        { "receiver-rebinding", "text='a,b'\ndef sep():\n global text\n text='changed'\n return ','\nprint(text.split(sep()),text)", "['a', 'b'] changed\n" },
        { "dynamic-separator", "class Text:\n def split(self,sep):return ['custom',sep]\nprint(Text().split(','))", "['custom', ',']\n" },
        { "exposed-and-expanded-separator", "method='a,b'.split\nprint(method(','),'a,b'.split(*[',']),'a,b'.split(**{'sep':','}))", "['a', 'b'] ['a', 'b'] ['a', 'b']\n" },
        { "separator-errors", "def run(sep):\n try:\n  print('a,b'.split(sep))\n except (TypeError,ValueError) as error:\n  print(type(error).__name__,str(error))\nrun('')\nrun(1)", "ValueError empty separator\nTypeError must be str or None, not int\n" },
        { "count-bounded_native", "print('key=a=b'.split('=',1))", "['key', 'a=b']\n" },
        { "count-index_protocol", "events=[]\nclass Count:\n def __index__(self):\n  events.append('index')\n  return 1\nprint('a,b,c'.split(',',Count()),events)", "['a', 'b,c'] ['index']\n" },
        { "count-separator_validation_order", "events=[]\nclass Count:\n def __index__(self):\n  events.append('index')\n  return 1\ndef run(sep):\n try:print('a,b,c'.split(sep,Count()))\n except Exception as error:print(type(error).__name__)\nrun(123)\nprint(events)", "TypeError\n['index']\n" },
        { "count-index_error_precedence", "class Count:\n def __index__(self):raise ValueError('index')\ndef run(sep):\n try:print('a,b,c'.split(sep,Count()))\n except Exception as error:print(type(error).__name__,str(error))\nrun(123)", "ValueError index\n" },
        { "count-large_representable_counts", "for count in ((1<<31)-1,1<<31,-(1<<31)-1,1<<62):\n try:print('a,b,c'.split(',',count))\n except Exception as error:print(type(error).__name__)", "['a', 'b', 'c']\n['a', 'b', 'c']\n['a', 'b', 'c']\n['a', 'b', 'c']\n" },
        { "count-count_overflow", "for count in (1<<100,-(1<<100)):\n try:print('a,b,c'.split(',',count))\n except Exception as error:print(type(error).__name__)", "OverflowError\nOverflowError\n" },
        { "count-count_64_boundaries", "for count in ((1<<63)-1,-(1<<63),1<<63,-(1<<63)-1):\n try:print('a,b,c'.split(',',count))\n except Exception as error:print(type(error).__name__,str(error))", "['a', 'b', 'c']\n['a', 'b', 'c']\nOverflowError Python int too large to convert to C ssize_t\nOverflowError Python int too large to convert to C ssize_t\n" },
        { "count-count_whitespace_domain", "for count in (0,1,-1,1<<62,-(1<<62)):print('  café 😀 tail  '.split(None,count))", "['café\\u2009😀 tail  ']\n['café', '😀 tail  ']\n['café', '😀', 'tail']\n['café', '😀', 'tail']\n['café', '😀', 'tail']\n" },
        { "count-count_type_slot", "class Count:\n def __index__(self):return 1\nclass Child(Count):pass\nvalue=Child()\nvalue.__index__=lambda:2\nprint('a,b,c'.split(',',value))", "['a', 'b,c']\n" },
        { "count-count_getattribute_bypass", "class Count:\n def __getattribute__(self,name):raise AssertionError('instance lookup')\n def __index__(self):return 1\nprint('a,b,c'.split(',',Count()))", "['a', 'b,c']\n" },
        { "count-count_invalid_values", "class Bad:\n def __index__(self):return 1.5\nclass Missing:pass\nclass Nil:__index__=None\nfor count in (1,None,1.5,Bad(),Missing(),Nil()):\n try:print('a,b,c'.split(',',count))\n except Exception as error:print(type(error).__name__)", "['a', 'b,c']\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" },
        { "count-count_evaluation_order", "events=[]\nclass Count:\n def __index__(self):\n  events.append('index')\n  return 1\ndef text():\n events.append('receiver')\n return 'a,b,c'\ndef sep():\n events.append('separator')\n return ','\ndef count():\n events.append('count-object')\n return Count()\nprint(text().split(sep(),count()),events)", "['a', 'b,c'] ['receiver', 'separator', 'count-object', 'index']\n" },
        { "count-count_method_forms", "events=[]\nclass Count:\n def __index__(self):\n  events.append('index')\n  return 1\nmethod='a,b,c'.split\nprint(method(',',Count()),'a,b,c'.split(sep=',',maxsplit=Count()),'a,b,c'.split(*[',',Count()]))\nprint(events)", "['a', 'b,c'] ['a', 'b,c'] ['a', 'b,c']\n['index', 'index', 'index']\n" },
        { "count-index-rebinds-receiver", "text='a,b,c'\nclass Count:\n def __index__(self):\n  global text\n  text='changed'\n  return 1\nprint(text.split(',',Count()),text)", "['a', 'b,c'] changed\n" },
        { "count-dynamic-two-arguments", "class Text:\n def split(self,sep,count):return ['custom',sep,count]\nprint(Text().split(',',1))", "['custom', ',', 1]\n" },
        { "trimmed-split-range", """
            def parts(text):return text.rstrip('\n').split(',')
            untrimmed='é'*32
            a,b='α,😀\n\n'.rstrip('\n').split(',')
            print(a, b, parts('é,tail\n'), parts('\n'))
            print(untrimmed.rstrip('\n').split(',')[0] is untrimmed)
            print((untrimmed+'\n').rstrip('\n').split(',')[0] is untrimmed)
            try:
                a,b='one\n'.rstrip('\n').split(',')
            except ValueError as error:
                print(str(error), a, b)
            """, "α 😀 ['é', 'tail'] ['']\nTrue\nFalse\nnot enough values to unpack (expected 2, got 1) α 😀\n" },
        { "trimmed-split-dynamic-paths", """
            class Text:
                def rstrip(self, chars):
                    print('trim', repr(chars))
                    return 'x,y'
            def sep():
                print('separator')
                return ','
            print(Text().rstrip('\n').split(','))
            print('α,béé'.rstrip('é').split(','))
            print('a,b\n'.rstrip('\n').split(sep()))
            try:
                'a,b\n'.rstrip('\n').split('')
            except ValueError as error:
                print(str(error))
            """, "trim '\\n'\n['x', 'y']\n['α', 'b']\nseparator\n['a', 'b']\nempty separator\n" },
        { "line-count-boundaries", """
            def count(text):return len(text.splitlines())
            print(count(''), count('\n'), count('a\n\n'), count('a\nb'))
            print(count('x'*4095+'\r\ncafé\x85😀\u2028z\x1c'))
            print(count('x'*4095+'\u2029tail'), count('x'*4095+'\x85tail'))
            print(len('a\nb'.splitlines()), len('a\nb'.splitlines(True)))
            """, "0 1 2 2\n4\n2 2\n2 2\n" },
        { "line-count-dynamic-calls", """
            events=[]
            class Text:
                def splitlines(self):
                    events.append('split')
                    return ['one']
            class Keep:
                def __bool__(self):
                    events.append('keep')
                    return True
            def observed(parts):
                events.append(parts)
                return 7
            def count(length,text):return length(text.splitlines())
            print(count(len,Text()),count(observed,'a\nb'),len('a\nb'.splitlines(Keep())))
            print(events)
            """, "1 7 2\n['split', ['a', 'b'], 'keep']\n" },
    };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task OnlyPrivateLineCountsCanSkipFundingTheList(bool asynchronous, bool exposeList)
    {
        var code = exposeList
            ? "try:\n lines=('α\\n'*4096).splitlines()\n print(lines[0],len(lines))\nexcept MemoryError:\n print('guest handler ran')"
            : "def count(text):return len(text.splitlines())\nprint(count('α\\n'*4096))";
        using var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var budget = new LythonMemoryBudget(96 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options))
        {
            if (exposeList)
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal(string.Empty, result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("4096\n", result.StandardOutput);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FixedUnpackingRefusalPrecedesTargetCallbacks(bool asynchronous)
    {
        var source = """
            import time
            text = ('α' * 32 + '|') * 7 + '😀' * 32
            items = {}
            def target(index):
                time.time()
                return index
            time.time()
            try:
                (items[target(0)], items[target(1)], items[target(2)], items[target(3)],
                 items[target(4)], items[target(5)], items[target(6)], items[target(7)]) = text.split('|')
            except MemoryError:
                print('guest handler ran')
            """.Replace("\r\n", "\n", StringComparison.Ordinal);
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var budget = new AdmissionGateBudget();
        // Leave room for the clock result. The complete set of split fields
        // needs more than this small amount, even when their backing is shared.
        var host = new AdmissionGateHost(budget, 256);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            var span = Assert.IsType<LythonSourceSpan>(result.Failure?.Span);
            Assert.Equal("text.split('|')", source.Substring(span.Start, span.Length));
            Assert.Empty(result.StandardOutput);
            Assert.Equal(0, host.AtSecondGate);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedCountedSplitStaysFundedAcrossAnotherExecution(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var input = new string('x', 32768) + "," + new string('y', 32768) + ",tail";
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = input } };
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("class Count:\n def __index__(self):return 1\nreturn text.split(',',Count())", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var parts = Assert.IsType<List<object?>>(retained.ReturnValue);
                Assert.Equal(2, parts.Count);
                Assert.Equal(32768, Assert.IsType<string>(parts[0]).Length);
                Assert.Equal(32773, Assert.IsType<string>(parts[1]).Length);
                Assert.EndsWith(",tail", Assert.IsType<string>(parts[1]));
                var held = budget.ReservedBytes;
                Assert.True(held - baseline >= input.Length);
                using (var next = script.Run(new MockLythonHost(), options)) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrimmedSplitRefusesBeforeUnpackingTargets(bool asynchronous)
    {
        var source = """
            import time
            class Target:
                def __setitem__(self, key, value):
                    time.time()
            target=Target()
            text=('λ,'*4096)+'tail\n'
            time.time()
            try:
                target[0],target[1]=text.rstrip('\n').split(',')
            except MemoryError:
                print('guest handler')
            """.Replace("\r\n", "\n", StringComparison.Ordinal);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 4096);
        using var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join(", ", compiled.Diagnostics));
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            var span = Assert.IsType<LythonSourceSpan>(result.Failure?.Span);
            Assert.Equal("text.rstrip('\\n').split(',')", source.Substring(span.Start, span.Length));
            Assert.Equal(0, host.AtSecondGate);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryAndGeneralCallsFollowPython(string name, string source, string expected)
    {
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

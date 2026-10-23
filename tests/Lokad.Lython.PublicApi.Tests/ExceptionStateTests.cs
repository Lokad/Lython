using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionStateTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "args-tuple-alias", "e=ValueError('old');old=e.args;replacement=(1,2)\ne.args=replacement\nprint(e.args is replacement,old,e.args)\ne.args=[]\nprint(replacement,e.args)", "True ('old',) (1, 2)\n(1, 2) ()\n" },
        { "systemexit-code-slot", "e=SystemExit(7);e.__dict__['code']=0\nprint(e.code,e.__dict__)\ne.code=2\nprint(e.code)", "7 {'code': 0}\n2\n" },
        { "exception-value-equality-systemexit", "a=SystemExit();b=SystemExit();print(a==b,a is b,len({a,b}))", "False False 2\n" },
        { "exception-value-equality-internal", "errors=[]\nfor i in range(2):\n try:1/0\n except ZeroDivisionError as e:errors.append(e)\nprint(errors[0]==errors[1],errors[0] is errors[1])", "False False\n" },
        { "exception-identity-control", "a=ValueError('x');b=ValueError('x');print(a==b,a is b)", "False False\n" },
        { "systemexit-none-args", "print(SystemExit(None).args,SystemExit().args)", "(None,) ()\n" },
        { "systemexit-multiple-args", "try:\n e=SystemExit(1,2);print(e.args,e.code)\nexcept Exception as e:print(type(e).__name__)", "(1, 2) (1, 2)\n" },
        { "systemexit-args-identity", "e=SystemExit(1);print(e.args is e.args)", "True\n" },
        { "generated-keyerror-none", "try:{}[None]\nexcept KeyError as e:print(e.args,str(e),repr(e))", "(None,) None KeyError(None)\n" },
        { "constructed-keyerror-none-control", "e=KeyError(None);print(e.args,str(e),repr(e))", "(None,) None KeyError(None)\n" },
        { "generated-error-args-identity", "try:{}['missing']\nexcept KeyError as e:print(e.args is e.args,e.args)", "True ('missing',)\n" },
        { "exception-cause-assignment", "e=ValueError();e.__cause__=KeyError();print(e.__suppress_context__)", "True\n" },
        { "exception-cause-none-assignment", "e=ValueError();e.__cause__=None;print(e.__suppress_context__)", "True\n" },
        { "exception-cause-finally", "a=ValueError('a');b=KeyError('b');e=TypeError('original')\ntry:\n try:raise e from a\n finally:e.__cause__=b\nexcept TypeError as caught:print(caught.__cause__ is b,caught is e)", "True True\n" },
        { "exception-context-finally", "b=KeyError('b');e=TypeError('original')\ntry:\n try:raise e\n finally:e.__context__=b\nexcept TypeError as caught:print(caught.__context__ is b,caught is e)", "True True\n" },
        { "exception-suppression-finally", "e=ValueError('x')\ntry:\n try:raise e\n finally:e.__suppress_context__=True\nexcept ValueError as caught:print(caught.__suppress_context__)", "True\n" },
        { "exception-args-list", "e=ValueError('x')\ntry:e.args=['a','b'];print(e.args)\nexcept Exception as err:print(type(err).__name__)", "('a', 'b')\n" },
        { "module-exception-type-deletion", "import csv,sys\ne=csv.Error('x');t=type(e)\ndel sys.modules['csv']\ntry:print(type(e) is t,e.__class__ is t)\nexcept Exception as err:print(type(err).__name__)", "True True\n" },
        { "exception-raised-chain-control", "a=KeyError('a');e=ValueError('x')\ntry:raise e from a\nexcept ValueError as caught:print(caught is e,caught.__cause__ is a,caught.__suppress_context__)", "True True True\n" },
        { "exception-dictionary-replacement", "e=ValueError();old=e.__dict__;replacement={'x':1};e.__dict__=replacement\nprint(e.__dict__ is replacement,old)\nreplacement['x']=2;print(e.x)", "True {}\n2\n" },
        { "exception-dictionary-self-control", "e=ValueError();e.x=1;old=e.__dict__;e.__dict__=old\nprint(e.__dict__ is old,e.x)", "True 1\n" },
        { "native-type-deletion-counter", "import collections,sys\nx=collections.Counter();t=type(x);del sys.modules['collections']\ntry:print(type(x) is t)\nexcept Exception as e:print(type(e).__name__)", "True\n" },
        { "native-type-deletion-decimal", "import decimal,sys\nx=decimal.Decimal(1);t=type(x);del sys.modules['decimal']\ntry:print(type(x) is t)\nexcept Exception as e:print(type(e).__name__)", "True\n" },
        { "native-type-null-registry", "import collections,sys\nx=collections.Counter();t=type(x);sys.modules['collections']=None\ntry:print(type(x) is t)\nexcept Exception as e:print(type(e).__name__)", "True\n" },
        { "exception-reraise-args-edit-control", "e=ValueError('old')\ntry:\n try:raise e\n finally:e.args=('new',)\nexcept ValueError as caught:print(str(caught),caught is e)", "new True\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeExceptionStateFollowsPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

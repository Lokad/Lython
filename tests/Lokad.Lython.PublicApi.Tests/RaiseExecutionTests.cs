using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RaiseExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "instance", "try:raise ValueError('payload')\nexcept ValueError as error:print(error.args,str(error))", "('payload',) payload\n" },
        { "type", "try:raise ValueError\nexcept ValueError as error:print(error.args,repr(str(error)))", "() ''\n" },
        { "cause-order", "events=[]\ndef value():events.append('value');return ValueError('outer')\ndef cause():events.append('cause');return KeyError('inner')\ntry:raise value() from cause()\nexcept ValueError as error:print(events,str(error),str(error.__cause__),error.__suppress_context__)", "['value', 'cause'] outer 'inner' True\n" },
        { "bare-reraise", "try:\n try:raise ValueError('payload')\n except ValueError:raise\nexcept ValueError as error:print(str(error))", "payload\n" },
        { "suppressed-context", "try:\n try:raise KeyError('inner')\n except KeyError:raise ValueError('outer') from None\nexcept ValueError as error:print(error.__cause__,type(error.__context__).__name__,error.__suppress_context__)", "None KeyError True\n" },
        { "protected-helper", "events=[]\ndef fail():\n try:raise ValueError('payload')\n finally:events.append('cleanup')\ntry:fail()\nexcept ValueError as error:print(str(error),events)", "payload ['cleanup']\n" },
        { "invalid-cause", "def cause():return 7\ntry:raise ValueError('payload') from cause()\nexcept TypeError as error:print(str(error))", "exception causes must derive from BaseException\n" },
        { "complete-json-validation", "import json\ndef require(configuration):\n if 'name' not in configuration:raise ValueError('missing name')\n return configuration['name']\nfor text in ['{\"name\":\"job\"}','{\"limit\":7}']:\n try:print(require(json.loads(text)))\n except ValueError as error:print(str(error))", "job\nmissing name\n" },
        { "reraise-identity-and-args", "error=ValueError('payload')\ntry:raise error\nexcept ValueError as caught:\n caught.args=('changed',)\n try:raise\n except ValueError as again:print(again is error,str(again))", "True changed\n" },
        { "finally-replacement", "def fail():\n try:raise ValueError('first')\n finally:raise TypeError('second')\ntry:fail()\nexcept TypeError as error:print(error.__context__.args,error.__cause__,error.__suppress_context__)", "('first',) None False\n" },
        { "restored-handler", "try:raise ValueError('outer')\nexcept ValueError as outer:\n try:raise TypeError('inner')\n except TypeError:pass\n try:raise\n except ValueError as again:print(again is outer)", "True\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RaisePreservesIdentityOrderAndChains(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
    }
}

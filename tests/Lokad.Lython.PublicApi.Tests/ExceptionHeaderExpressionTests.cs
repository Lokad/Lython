using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionHeaderExpressionTests
{
    [Fact]
    public async Task CallsIndexingAndConditionalHandlersResolveOrdinaryValues()
    {
        await AssertOutput("""
            errors = [ValueError, TypeError]
            def error_type():
                return errors[0]
            try:
                raise ValueError("call")
            except error_type() as error:
                print(str(error))
            try:
                raise TypeError("index")
            except errors[1]:
                print("index")
            try:
                raise ValueError("conditional")
            except ValueError if True else TypeError:
                print("conditional")
            try:
                raise ValueError("lambda")
            except (lambda: ValueError)():
                print("lambda")
            """, "call\nindex\nconditional\nlambda\n");
    }

    [Fact]
    public async Task HeadersRunOnlyWhenReachedAndSeeTheActiveException()
    {
        await AssertOutput("""
            import sys
            events = []
            original = ValueError("original")
            def header(label, cls):
                events.append(label)
                print(label, sys.exc_info()[1] is original)
                return cls
            try:
                print("normal")
            except header("never", ValueError):
                pass
            try:
                raise original
            except header("first", TypeError):
                pass
            except header("second", ValueError) as error:
                print(error is original)
            except header("third", ValueError):
                pass
            print(events, sys.exc_info()[1] is None)
            """, "normal\nfirst True\nsecond True\nTrue\n['first', 'second'] True\n");
    }

    [Fact]
    public async Task DynamicTuplesEvaluateEveryElementBeforeMatching()
    {
        await AssertOutput("""
            events = []
            def header(label, value):
                events.append(label)
                return value
            errors = (TypeError,)
            try:
                raise ValueError("tuple")
            except (header("first", ValueError), *header("rest", errors)) as error:
                print(str(error), events)
            try:
                try:
                    raise ValueError("invalid")
                except (header("match", ValueError), header("invalid", int)):
                    print("wrong")
            except TypeError as error:
                print(type(error.__context__).__name__, events)
            """, "tuple ['first', 'rest']\nValueError ['first', 'rest', 'match', 'invalid']\n");
    }

    [Fact]
    public async Task RuntimeGeneratedExceptionsKeepTheSameInstanceAcrossHeaderAndBody()
    {
        await AssertOutput("""
            import sys
            seen = []
            def header():
                seen.append(sys.exc_info()[1])
                return ValueError
            try:
                class C:
                    try:
                        int("invalid")
                    except header() as error:
                        print(error is seen[0], sys.exc_info()[1] is error)
                        raise
            except ValueError as error:
                print(error is seen[0], sys.exc_info()[1] is error)
            """, "True True\nTrue True\n");
    }

    [Fact]
    public async Task HeaderFailuresBypassSiblingsAndPreserveIdentityThroughCleanup()
    {
        await AssertOutput("""
            import sys
            original = ValueError("original")
            replacement = KeyError("header")
            events = []
            def header():
                events.append("header")
                raise replacement
            try:
                try:
                    raise original
                except header():
                    print("wrong match")
                except KeyError:
                    print("wrong sibling")
                finally:
                    print("finally", sys.exc_info()[1] is replacement)
            except KeyError as error:
                print(error is replacement, error.__context__ is original, events)
            """, "finally True\nTrue True ['header']\n");
    }

    [Fact]
    public async Task HeaderWalrusBindingsRespectFunctionAndGlobalScopes()
    {
        await AssertOutput("""
            selected = None
            def run():
                global selected, error
                try:
                    errors = [ValueError]
                    raise ValueError("x")
                except (selected := errors[0]) as error:
                    print(selected.__name__, str(error))
                try:
                    print(error)
                except NameError:
                    print("deleted")
                return selected
            print(run() is ValueError, selected is ValueError)
            """, "ValueError x\ndeleted\nTrue True\n");
    }

    [Fact]
    public async Task ClassBodyElseFailuresDoNotReenterTheirHandlers()
    {
        await AssertOutput("""
            events = []
            def header():
                events.append("header")
                return ValueError
            try:
                class C:
                    try:
                        pass
                    except header():
                        events.append("handler")
                    else:
                        raise ValueError("else")
                    finally:
                        events.append("finally")
            except ValueError as error:
                print(str(error), events)
            """, "else ['finally']\n");
    }

    [Fact]
    public async Task HandlerBodyErrorsAreActiveInClassFinallyCleanup()
    {
        await AssertOutput("""
            import sys
            original = ValueError("original")
            replacement = KeyError("body")
            try:
                class C:
                    try:
                        raise original
                    except (lambda: ValueError)():
                        raise replacement
                    finally:
                        print(sys.exc_info()[1] is replacement)
            except KeyError as error:
                print(error is replacement, error.__context__ is original)
            """, "True\nTrue True\n");
    }

    [Fact]
    public async Task GeneratorHeaderSelectionCanSuspendBeforeBinding()
    {
        await AssertOutput("""
            def run():
                try:
                    raise ValueError("x")
                except (yield "header") as error:
                    yield str(error)
                finally:
                    yield "finally"
            generator = run()
            print(next(generator))
            print(generator.send(ValueError))
            print(next(generator))
            print(next(generator, "done"))
            """, "header\nx\nfinally\ndone\n");
    }

    [Theory]
    [InlineData("except ValueError, TypeError:")]
    [InlineData("except value := ValueError:")]
    [InlineData("except yield ValueError:")]
    [InlineData("except ValueError as target.value:")]
    public async Task InvalidHeadersAreRejectedBeforeEffects(string header)
    {
        var compiled = new LythonEngine().Compile("print('effects')\ndef run():\n    try:\n        raise ValueError()\n    " + header + "\n        pass\n");
        Assert.False(compiled.IsValid);
        var result = await compiled.RunAsync(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Empty(result.StandardOutput);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("[ValueError]")]
    [InlineData("(ValueError, int)")]
    [InlineData("((ValueError,),)")]
    [InlineData("None")]
    public async Task InvalidComputedHandlerValuesRaiseCatchableTypeErrors(string value)
    {
        await AssertOutput("def header():\n    return " + value + "\n" + """
            try:
                try:
                    raise ValueError("original")
                except header():
                    print("wrong")
                except TypeError:
                    print("wrong sibling")
            except TypeError as error:
                print(type(error.__context__).__name__)
            """, "ValueError\n");
    }

    [Theory]
    [InlineData(false, "call")]
    [InlineData(true, "call")]
    [InlineData(false, "index")]
    [InlineData(true, "index")]
    [InlineData(false, "property")]
    [InlineData(true, "property")]
    public async Task HeaderExpressionsAwaitDelayedProtocols(bool classBody, string protocol)
    {
        var expression = protocol switch { "call" => "header()", "index" => "headers[0]", _ => "headers.value" };
        var body = "try:\n    raise ValueError('original')\nexcept " + expression + " as error:\n    print(str(error))\nfinally:\n    Path('/written.txt').write_text('cleanup')\n";
        var source = """
            from pathlib import Path
            def header():
                Path("/number.txt").read_text()
                return ValueError
            class Headers:
                def __getitem__(self, key):
                    return header()
                @property
                def value(self):
                    return header()
            headers = Headers()
            """ + "\n" + (classBody ? "class C:\n" + string.Join("\n", body.Split('\n').Select(line => "    " + line)) : body);
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/number.txt", "2");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("original\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal(immediate.ReadText("/written.txt"), delayed.ReadText("/written.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 2);
    }

    [Fact]
    public async Task DynamicHeaderTupleConstructionRemainsGoverned()
    {
        var compiled = new LythonEngine().Compile("""
            try:
                raise ValueError("x")
            except tuple(ValueError for _ in range(100000)):
                print("wrong")
            """);
        Assert.True(compiled.IsValid);
        foreach (var (options, exceptionType) in new[]
        {
            (new LythonRunOptions { MaxCollectionSize = 3 }, "RuntimeError"),
            (new LythonRunOptions { MaxExecutionMemoryBytes = 262144 }, "MemoryError"),
        })
        {
            foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
            {
                Assert.False(result.Success);
                Assert.Equal(exceptionType, result.Failure?.ExceptionType);
                Assert.Empty(result.StandardOutput);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedInvalidHeadersSkipSiblingsAndAwaitFinally(bool classBody)
    {
        var body = """
            try:
                raise ValueError("original")
            except header():
                print("wrong match")
            except TypeError:
                print("wrong sibling")
            finally:
                Path("/written.txt").write_text("cleanup")
            """;
        var protectedBody = classBody ? "class C:\n" + string.Join("\n", body.Split('\n').Select(line => "    " + line)) : body;
        var source = """
            from pathlib import Path
            def header():
                Path("/number.txt").read_text()
                return int
            """ + "\ntry:\n" + string.Join("\n", protectedBody.Split('\n').Select(line => "    " + line)) + "\nexcept TypeError as error:\n    print(type(error.__context__).__name__)\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/number.txt", "2");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ValueError\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal("cleanup", delayed.ReadText("/written.txt"));
        Assert.Equal(immediate.ReadText("/written.txt"), delayed.ReadText("/written.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 2);
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

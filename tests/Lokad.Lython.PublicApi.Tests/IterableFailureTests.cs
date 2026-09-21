using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R17: protocol absence is a typed signal (PyNotIterableException), never an
// English text match. User-raised TypeErrors propagate verbatim even with
// identical wording; genuine non-iterables keep their exact public messages.
public sealed class IterableFailureTests
{
    [Fact]
    public void ExactTextFromIterPreserved()
    {
        const string code = """
            class Evil:
                def __iter__(self):
                    raise TypeError("'Evil' object is not iterable")
                def __next__(self):
                    raise TypeError("'Evil' object is not iterable")
            e = Evil()
            seen = []
            try:
                x, y = e
            except TypeError as ex:
                seen.append(str(ex))
            try:
                list(e)
            except TypeError as ex:
                seen.append(str(ex))
            try:
                bytes(e)
            except TypeError as ex:
                seen.append(str(ex))
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "'Evil' object is not iterable", "'Evil' object is not iterable", "'Evil' object is not iterable" },
            result.ReturnValue);
    }

    [Fact]
    public void ExactTextFromNextPreserved()
    {
        const string code = """
            class Evil:
                def __iter__(self):
                    return self
                def __next__(self):
                    raise TypeError("'Evil' object is not iterable")
            e = Evil()
            seen = []
            try:
                next(e)
            except TypeError as ex:
                seen.append(str(ex))
            try:
                for v in e:
                    pass
            except TypeError as ex:
                seen.append(str(ex))
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "'Evil' object is not iterable", "'Evil' object is not iterable" },
            result.ReturnValue);
    }

    [Fact]
    public void GenuineNonIterablesKeepMessages()
    {
        const string code = """
            seen = []
            try:
                a, b = 5
            except TypeError as ex:
                seen.append(str(ex))
            try:
                list(5)
            except TypeError as ex:
                seen.append(str(ex))
            try:
                bytes(5.5)
            except TypeError as ex:
                seen.append(str(ex))
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "cannot unpack non-iterable int object", "'int' object is not iterable", "cannot convert 'float' object to bytes" },
            result.ReturnValue);
    }

    [Fact]
    public void CallSplatKeepsUserError()
    {
        const string code = """
            class Evil:
                def __iter__(self):
                    raise TypeError("'Evil' object is not iterable")
            def f(a, b):
                return a
            seen = []
            try:
                f(*Evil())
            except TypeError as ex:
                seen.append(str(ex))
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "'Evil' object is not iterable" },
            result.ReturnValue);
    }

    [Fact]
    public async Task AsyncExactTextPropagates()
    {
        const string code = """
            class Evil:
                def __iter__(self):
                    raise TypeError("'Evil' object is not iterable")
            e = Evil()
            seen = []
            try:
                x, y = e
            except TypeError as ex:
                seen.append(str(ex))
            try:
                for v in e:
                    pass
            except TypeError as ex:
                seen.append(str(ex))
            return seen
            """;
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "'Evil' object is not iterable", "'Evil' object is not iterable" },
            result.ReturnValue);
    }
}

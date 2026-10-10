using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BoundMethodLifetimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SmallPositionalCallsKeepReceiverAndArgumentAliases(int width)
    {
        var parameters = width == 0 ? "" : width == 1 ? ", a" : ", a, b";
        var returned = width == 0 ? "self," : width == 1 ? "self, a" : "self, a, b";
        var first = width == 0 ? "" : width == 1 ? "left" : "left, right";
        var second = width == 0 ? "" : width == 1 ? "right" : "right, left";
        var assertions = width == 0 ? "" : width == 1 ? "assert x[1] is left and y[1] is right" : "assert x[1] is left and x[2] is right and y[1] is right and y[2] is left";
        await Both($"class C:\n def get(self{parameters}):return ({returned})\nleft=[]\nright=[]\none=C()\ntwo=C()\nx=one.get({first})\ny=two.get({second})\nassert x[0] is one and y[0] is two\n{assertions}\nreturn True\n");
    }

    [Fact]
    public async Task ReturnedClosuresKeepReceiverAndValuesAfterOtherCalls()
        => await Both("""
            class C:
             def capture(self, value):
              def read():return (self,value)
              return read
            a=C()
            b=C()
            left=[]
            right=[]
            first=a.capture(left)
            second=b.capture(right)
            assert first()[0] is a and first()[1] is left
            assert second()[0] is b and second()[1] is right
            return True
            """);

    [Fact]
    public async Task DelayedGeneratorsKeepReceiverAndValuesAfterOtherCalls()
        => await Both("""
            class C:
             def generate(self, value):
              yield (self,value)
              yield value
            a=C()
            b=C()
            left=[]
            right=[]
            first=a.generate(left)
            second=b.generate(right)
            x=next(first)
            y=next(second)
            assert x[0] is a and x[1] is left and y[0] is b and y[1] is right
            assert next(first) is left and next(second) is right
            return True
            """);

    [Fact]
    public async Task RecursiveCallsKeepDistinctReceiversAndArguments()
        => await Both("""
            class C:
             def visit(self, other, n):
              if n==0:return self.label
              return self.label+other.visit(self,n-1)
            a=C()
            b=C()
            a.label='a'
            b.label='b'
            assert a.visit(b,5)=='ababab'
            assert b.visit(a,4)=='babab'
            return True
            """);

    [Fact]
    public async Task DefaultsKeywordsVariadicsAndErrorsKeepDirectFunctionSemantics()
        => await Both("""
            class C:
             def f(self,a,b=3):return (self,a,b)
             def v(self,a,/,*rest,flag=7,**kw):return (self,a,rest,flag,kw)
            m=C()
            assert m.f(1)==C.f(m,1) and m.f(1,2)==C.f(m,1,2)
            assert m.f(a=2,b=4)==C.f(m,a=2,b=4)
            assert m.v(2,3)==C.v(m,2,3)
            assert m.v(2,3,4,flag=8,z=9)==C.v(m,2,3,4,flag=8,z=9)
            def fail(call):
             try:call()
             except TypeError as error:return str(error)
             raise AssertionError('expected TypeError')
            assert fail(lambda:m.f())==fail(lambda:C.f(m))
            assert fail(lambda:m.f(1,2,3))==fail(lambda:C.f(m,1,2,3))
            assert fail(lambda:m.f(1,2,b=8))==fail(lambda:C.f(m,1,2,b=8))
            assert fail(lambda:m.f(1,z=8))==fail(lambda:C.f(m,1,z=8))
            assert fail(lambda:m.f(1,self=m))==fail(lambda:C.f(m,1,self=m))
            return True
            """);

    [Fact]
    public async Task BoundMetadataAndLiveMethodReplacementKeepTheirIdentities()
        => await Both("""
            class C:
             def f(self,x):return self.value+x
            c=C()
            c.value=1
            saved=c.f
            original=C.f
            def replacement(self,x):return self.value+x+10
            C.f=replacement
            c.value=2
            assert saved.__self__ is c and saved.__func__ is original
            assert saved(3)==5 and c.f(3)==15
            assert c.f.__func__ is replacement and saved.__name__=='f'
            return True
            """);

    private static async Task Both(string source)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(true, result.ReturnValue);
        }
    }
}

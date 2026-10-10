using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SuperDescriptorParityTests
{
    [Fact]
    public async Task InstanceClassAndPropertySuperKeepTheDefiningClass()
    {
        var compiled = new LythonEngine().Compile("""
            class Base:
             def method(self):return 3
             @classmethod
             def owner(cls):return cls
             @property
             def value(self):return 37
            class C(Base):
             def method(self):return super().method()+1
             @classmethod
             def owner(cls):return super().owner()
             @property
             def value(self):return super().value+1
            class D(C):pass
            C=7
            print(D().method(),D.owner() is D,D().value)
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("4 True 38\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ExplicitDescriptorsKeepAnchorReceiverAndBoundType()
    {
        var compiled = new LythonEngine().Compile("""
            class Base:
             def method(self):return 3
             @classmethod
             def owner(cls):return cls
            class C(Base):pass
            class D(C):pass
            receiver=D()
            unbound=super(C)
            bound=super(C,receiver)
            cls=super(C,D)
            print(unbound.__thisclass__ is C,unbound.__self__ is None,unbound.__self_class__ is None)
            print(bound.__thisclass__ is C,bound.__self__ is receiver,bound.__self_class__ is D,bound.method())
            print(cls.__self__ is D,cls.__self_class__ is D,cls.owner() is D)
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True True\nTrue True True 3\nTrue True True\n", result.StandardOutput);
        }
    }
}

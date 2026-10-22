using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctoolsStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialStateIsLiveAndDescriptorsAreReadOnly(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import partial
            def f(*args,**kwargs):return args,list(kwargs.items())
            p=partial(f,1,a=2,b=3)
            print(p.args is p.args,p.keywords is p.keywords,p.__dict__ is p.__dict__)
            p.keywords['a']=4
            print(p(5,b=6,c=7))
            p.__dict__['tag']='live'
            print(p.tag)
            p.tag='changed'
            print(p.__dict__['tag'])
            p.__dict__['args']='shadow'
            print(p.args)
            for name in ('func','args','keywords'):
                try:setattr(p,name,None)
                except AttributeError:print(name,'readonly')
            p.keywords[1]=2
            try:p()
            except TypeError:print('invalid keyword')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True True True\n((1, 5), [('a', 4), ('b', 6), ('c', 7)])\nlive\nchanged\n(1,)\nfunc readonly\nargs readonly\nkeywords readonly\ninvalid keyword\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedPartialsPreserveMetadataBoundaryAndIndependentKeywords(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import partial
            def f(*args,**kwargs):return args,kwargs
            p=partial(f,1,a=2)
            q=partial(p,b=3)
            print(q.func is f,q.args is p.args,q.keywords is p.keywords)
            p.keywords['a']=9
            print(q())
            metadata=p.__dict__
            r=partial(p,4,b=5)
            print(r.func is p,r(6,a=7))
            old=p.__dict__
            p.__dict__={'tag':8}
            old['tag']=10
            print(p.tag,old['tag'],p.__dict__ is old)
            try:p.__dict__=[]
            except TypeError:print('dict required')
            p.keywords.clear()
            p.keywords['self']=p
            print(repr(p).endswith('self=...)'))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True True False\n((1,), {'a': 2, 'b': 3})\nTrue ((1, 4, 6), {'a': 7, 'b': 5})\n8 10 False\ndict required\nTrue\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CacheAndDispatchMetadataHaveOneLiveDictionary(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import lru_cache,singledispatch
            def f(value):return value+1
            for wrapper in (lru_cache()(f),singledispatch(f)):
                metadata=wrapper.__dict__
                print(metadata is wrapper.__dict__)
                metadata['tag']=1
                print(wrapper.tag)
                wrapper.tag=2
                print(metadata['tag'])
                wrapper.__dict__={'tag':3}
                print(wrapper.tag,metadata['tag'],wrapper(4))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True\n1\n2\n3 2 5\nTrue\n1\n2\n3 2 5\n", result.StandardOutput);
    }
}

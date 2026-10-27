using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TypingAliasContractTests
{
    public static TheoryData<string,string,string> GoldenCases => new()
    {
        { "ordinary", """
            from typing import List,Dict,Tuple,Set,Type,Sequence,Mapping,TypeVar,get_origin,get_args
            import collections.abc as abc
            T=TypeVar('T')
            U=TypeVar('U')
            for alias,origin in [(List[int],list),(Dict[str,int],dict),(Tuple[int,str],tuple),(Set[int],set),(Type[int],type),(Sequence[int],abc.Sequence),(Mapping[str,int],abc.Mapping)]:
                print(get_origin(alias) is origin,alias.__origin__ is origin,alias.__args__ is get_args(alias),alias.__parameters__==())
            a=Dict[str,List[T]]
            print(a.__parameters__==(T,))
            b=a[int]
            print(b==Dict[str,List[int]],hash(b)==hash(Dict[str,List[int]]),len({b,Dict[str,List[int]]}))
            c=list[Tuple[T,U]]
            print(c.__parameters__==(T,U),c[int,str]==list[Tuple[int,str]])
            print(List[int]!=list[int],Dict[str,int]!=dict[str,int])
            for alias,index in [(List[int],str),(List[T],()),(List[T],(int,str))]:
                try: alias[index]
                except TypeError as e: print(type(e).__name__)
            """, "True True True True\nTrue True True True\nTrue True True True\nTrue True True True\nTrue True True True\nTrue True True True\nTrue True True True\nTrue\nTrue True 1\nTrue True\nTrue True\nTypeError\nTypeError\nTypeError\n" },
        { "special", """
            from typing import TypeVar,ParamSpec,Annotated,Literal,Callable,get_origin,get_args
            T=TypeVar('T')
            P=ParamSpec('P')
            a=Annotated[T,T]
            b=a[int]
            print(a.__parameters__==(T,),b.__origin__ is int,b.__metadata__==(T,),get_origin(b) is Annotated,get_args(b)==(int,T))
            a=Annotated[Annotated[int,'first'],'second']
            print(a==Annotated[int,'first','second'],a.__metadata__,repr(a))
            print(Literal['a','b']==Literal['b','a'],hash(Literal['a','b'])==hash(Literal['b','a']))
            print(Literal[True]!=Literal[1],len({Literal[True],Literal[1]}),get_args(Literal[1,2,1]))
            print(repr(Literal['a']),get_origin(Literal['a']) is Literal)
            parameters=[int,str]
            a=Callable[parameters,bool]
            parameters.append(float)
            print(a.__args__==(int,str,bool),get_args(a)==([int,str],bool),get_args(a)[0] is get_args(a)[0])
            a=Callable[[T],str]
            print(a.__parameters__==(T,),a[int]==Callable[[int],str],repr(a[int]))
            a=Callable[P,str]
            print(a.__parameters__==(P,),a[[int,str]]==Callable[[int,str],str])
            """, "True True True True True\nTrue ('first', 'second') typing.Annotated[int, 'first', 'second']\nTrue True\nTrue 2 (1, 2)\ntyping.Literal['a'] True\nTrue True False\nTrue True typing.Callable[[int], str]\nTrue True\n" },
        { "unions", """
            from typing import Union,Optional,get_origin,get_args
            u=int|str
            v=str|int
            print(u==v,hash(u)==hash(v),len({u,v}),{u:42}[v])
            print(get_origin(u) is type(u),hasattr(u,'__origin__'),get_args(u)==(int,str),get_args(v)==(str,int))
            print((int|str|int)==v,(int|int) is int)
            print(Union[int,str]==Union[str,int],hash(Union[int,str])==hash(Union[str,int]))
            print(u==Union[str,int],hash(u)==hash(Union[str,int]),len({u,Union[str,int]}))
            print(Optional[int]==Union[type(None),int],repr(Union[int,type(None)]),get_origin(Optional[int]) is Union)
            print(get_args(Optional[int])==(int,type(None)),Union[int,int] is int)
            """, "True True 1 42\nTrue False True True\nTrue True\nTrue True\nTrue True 1\nTrue typing.Optional[int] True\nTrue True\n" },
    };

    [Theory,MemberData(nameof(GoldenCases))]
    public async Task MetadataSubstitutionAndKeysMatchIndependentCpython313(string name,string source,string expected)
    {
        var root=new LythonMemoryBudget(4*1024*1024);
        using(var script=new LythonEngine().Compile(source,root))
        {
            Assert.True(script.IsValid,name+": "+string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
            var baseline=root.ReservedBytes;
            foreach(var asynchronous in new[]{false,true})
            {
                using(var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    Assert.Equal(expected,result.StandardOutput);
                }
                Assert.Equal(baseline,root.ReservedBytes);
            }
        }
        Assert.Equal(0,root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedLegacyRenderingAndNestingWitnessesStayContained(bool nesting)
    {
        var source="import time\nfrom typing import List\n"+(nesting
            ? "a=int\nfor i in range(1024):a=List[a]\nr=repr(a)"
            : "a=List['x'*262144]\ntime.time()\nr=repr(a)");
        using var script=new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[]{false,true})
        {
            var budget=new AdmissionGateBudget();
            var host=new AdmissionGateHost(budget,32768);
            using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
                :script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.False(result.Success);
                if(nesting)Assert.Contains(result.Failure?.ExceptionType,new[]{"RecursionError","RuntimeError"});
                else Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task SavedLargeGetArgsWitnessReusesItsFundedTuple()
    {
        using var script=new LythonEngine().Compile("""
            import time
            from typing import Tuple,get_args
            a=Tuple[tuple(range(32768))]
            original=a.__args__
            time.time()
            values=get_args(a)
            assert values is original
            assert get_args(a) is values
            time.time()
            return len(values)
            """);
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[]{false,true})
        {
            var budget=new AdmissionGateBudget();
            var host=new AdmissionGateHost(budget,32768);
            using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
                :script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal("32768",result.ReturnValue?.ToString());
                Assert.True(budget.AtGate>32768);
                Assert.True(host.AtSecondGate-budget.AtGate<32768);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedArgumentAliasesRemainFundedUntilResultCleanup()
    {
        const string source="""
            from typing import Annotated,Literal,get_args
            payload='x'*32768
            a=Annotated[int,payload]
            b=Literal[payload]
            assert a.__metadata__[0] is payload
            assert get_args(a)[1] is payload
            assert get_args(b)[0] is payload
            return get_args(a)[1]
            """;
        var root=new LythonMemoryBudget(2*1024*1024);
        using(var script=new LythonEngine().Compile(source,root))
        {
            Assert.True(script.IsValid);
            var baseline=root.ReservedBytes;
            foreach(var asynchronous in new[]{false,true})
            {
                using(var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    Assert.Equal(new string('x',32768),result.ReturnValue);
                    Assert.True(root.ReservedBytes>=baseline+32768);
                }
                Assert.Equal(baseline,root.ReservedBytes);
            }
        }
        Assert.Equal(0,root.ReservedBytes);
    }
}

using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class DefaultMapAllocationDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)]
    [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,8)] [InlineData(true,8)]
    [InlineData(false,32)] [InlineData(true,32)]
    public async Task General(bool asynchronous,int count)
    {
        var expression = new LoweredNoneLiteralExpression(new NoneLiteralExpressionSyntax(new LythonSourceSpan(0,4,1,1)));
        var parameters = Enumerable.Range(0,count*2+1).Select(i => new LoweredFunctionParameter("p"+i,
            FunctionParameterKind.KeywordOnly, Annotation: null, DefaultValue: i%2==1 ? expression : null)).ToArray();
        var marker = new PyList([42]);
        var calls=0;
        object Evaluate(LoweredExpression value) { Assert.Same(expression,value); calls++; return marker; }
        ValueTask<object> EvaluateAsync(LoweredExpression value) => new(Evaluate(value));
        Func<LoweredExpression,object> sync=Evaluate;
        Func<LoweredExpression,ValueTask<object>> async=EvaluateAsync;
        var samples=new List<long>();
        Dictionary<string,object>? map=null;
        for(var sample=-2;sample<8;sample++)
        {
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var call=0;call<64;call++)
                map=asynchronous ? await LythonRuntime.BuildDefaultArgumentMapAsync(parameters,async)
                    : LythonRuntime.BuildDefaultArgumentMap(parameters,sync);
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(count,map!.Count);
            Assert.Equal(Enumerable.Range(0,count).Select(i=>"p"+(2*i+1)),map.Keys);
            Assert.All(map.Values,value=>Assert.Same(marker,value));
            Assert.Single(marker);Assert.Equal(42,marker[0]);
            if(sample>=0)samples.Add(allocated);
        }
        Assert.Equal(count*64*10,calls);
        output.WriteLine("DEFAULT_MAP:"+JsonSerializer.Serialize(new { route=asynchronous?"async":"sync",count,callsPerSample=64,samples,names=map!.Keys.ToArray(),aliases=true,evaluations=calls }));
    }

    [Theory]
    [InlineData(0)] [InlineData(8)]
    public void ExecutableControl(int count)
    {
        var materialize=typeof(LythonRuntime).GetMethod("MaterializeExecutableDefaultValues",BindingFlags.NonPublic|BindingFlags.Static)!
            .CreateDelegate<Func<IReadOnlyDictionary<string,LoweredExpression>,Context,Dictionary<string,object>>>();
        var expression=new LoweredNoneLiteralExpression(new NoneLiteralExpressionSyntax(new LythonSourceSpan(0,4,1,1)));
        var defaults=Enumerable.Range(0,count).ToDictionary(i=>"p"+i,_=>(LoweredExpression)expression);
        var context=new Context(new MockLythonHost(),new LythonRunOptions());
        var samples=new List<long>();
        for(var sample=-2;sample<8;sample++)
        {
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var call=0;call<64;call++) _=materialize(defaults,context);
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            if(sample>=0)samples.Add(allocated);
        }
        output.WriteLine("DEFAULT_MAP:"+JsonSerializer.Serialize(new { route="executable",count,callsPerSample=64,samples }));
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DefaultObjectAttributeTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AttributeSlotsPreservePythonBehaviorInBothModes(string source)
    {
        var compiled = new LythonEngine().Compile(source + "\nreturn 'ok'\n");
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok", result.ReturnValue);
        }
    }

    public static TheoryData<string> Cases => new()
    {
        """
        events=[]
        class Data:
         def __get__(self,obj,owner):
          events.append('get')
          return obj._value
         def __set__(self,obj,value):
          events.append('set')
          obj._value=value
        class NonData:
         def __get__(self,obj,owner): return 99
        class C:
         value=Data()
         other=NonData()
        c=C()
        c.value=[]
        value=c.value
        c.value=value
        assert c.value is value and events==['set','get','set','get']
        assert c.other==99
        c.other=7
        assert c.other==7 and c.__class__ is C
        """,
        """
        events=[]
        class Base: pass
        class C(Base): pass
        c=C()
        c.value=2
        def read(self,name):
         events.append(('get',name))
         return object.__getattribute__(self,name)+10
        def write(self,name,value):
         events.append(('set',name))
         object.__setattr__(self,name,value+20)
        Base.__getattribute__=read
        Base.__setattr__=write
        assert c.value==12
        c.value=3
        assert c.value==33
        Base.__getattribute__=object.__getattribute__
        Base.__setattr__=object.__setattr__
        assert c.value==23
        c.value=4
        assert c.value==4 and events==[('get','value'),('set','value'),('get','value')]
        """,
        """
        events=[]
        class D:
         def __get__(self,obj,owner): raise AttributeError('descriptor')
        class C:
         value=D()
         def __getattr__(self,name):
          events.append(name)
          return 'fallback:'+name
        c=C()
        assert c.value=='fallback:value'
        assert c.missing=='fallback:missing'
        assert events==['value','missing']
        c.present=[]
        assert c.present is c.present
        """,
        """
        events=[]
        class C:
         def get(self):
          events.append('get')
          return self._value
         def put(self,value):
          events.append('set')
          self._value=value
         value=property(get,put)
         readonly=property(get)
        c=C()
        c.value=3
        c.value+=4
        assert c.value==7 and events==['set','get','set','get']
        try: c.readonly=9
        except AttributeError: pass
        else: assert False
        assert c.readonly==7
        """,
        """
        class C: pass
        c=C()
        c.value=[]
        get=object.__getattribute__
        put=object.__setattr__
        assert get(c,'value') is c.value
        put(c,'value',5)
        assert c.value==5
        bound=c.__getattribute__
        assert bound('value')==5 and bound.__self__ is c
        assert bound.__name__=='__getattribute__' and bound.__objclass__ is object
        for call in [lambda:get(c,3),lambda:put(c,3,4),lambda:get(c),lambda:put(c,'value')]:
         try: call()
         except TypeError: pass
         else: assert False
        """,
        """
        class C: pass
        c=C()
        c.value=3
        C.__getattribute__=42
        try: value=c.value
        except TypeError: pass
        else: assert False
        C.__getattribute__=object.__getattribute__
        C.__setattr__=42
        try: c.value=8
        except TypeError: pass
        else: assert False
        C.__setattr__=object.__setattr__
        assert c.value==3
        c.value=5
        assert c.value==5
        """,
        """
        class Base:
         def read(self): return self.value
        class C(Base):
         def read(self): return super().read()+1
         def capture(self): return lambda:self.value
         def generate(self):
          yield self.value
          yield self.value
        c=C()
        c.value=2
        read=c.capture()
        values=c.generate()
        assert c.read()==3 and next(values)==2
        c.value=7
        assert read()==7 and next(values)==7
        """,
        """
        events=[]
        class ReadSlot:
         def __get__(self,obj,owner):
          events.append('bind')
          return lambda name:object.__getattribute__(obj,name)
        class WriteSlot:
         def __get__(self,obj,owner):
          events.append('bind-set')
          return lambda name,value:object.__setattr__(obj,name,value)
        class DeleteSlot:
         def __get__(self,obj,owner):
          events.append('bind-delete')
          return lambda name:object.__delattr__(obj,name)
        class C:
         __getattribute__=ReadSlot()
         __setattr__=WriteSlot()
         __delattr__=DeleteSlot()
        c=C()
        c.value=3
        assert c.value==3 and events==['bind-set','bind']
        del c.value
        assert events==['bind-set','bind','bind-delete']
        """,
        """
        events=[]
        class C: pass
        c=C()
        class Holder:
         def get(self):
          events.append('receiver')
          return c
         target=property(get)
        holder=Holder()
        class Work:
         def run(self):
          def value():
           events.append('value')
           return []
          holder.target.value=value()
          assert events==['value','receiver']
          events.clear()
          def fail():
           events.append('fail')
           raise ValueError('rhs')
          try: holder.target.value=fail()
          except ValueError: pass
          else: assert False
          assert events==['fail']
        Work().run()
        """
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultSlotsAwaitPropertiesAndKeepOverlappingRunsDistinct(bool setter)
    {
        var compiled = new LythonEngine().Compile(SuspendingProperty(setter));
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var first = new DelayedLythonHost();
        var second = new DelayedLythonHost();
        first.SeedFile("/value", "first");
        second.SeedFile("/value", "second");
        var results = await Task.WhenAll(compiled.RunAsync(first), compiled.RunAsync(second)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(results, result => Assert.True(result.Success, result.Failure?.Message));
        Assert.Equal("first", results[0].ReturnValue);
        Assert.Equal("second", results[1].ReturnValue);
        Assert.True(first.CompletedAsynchronously > 0 && second.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelingSuspendedPropertyKeepsCompiledScriptReusable(bool setter)
    {
        var compiled = new LythonEngine().Compile(SuspendingProperty(setter));
        Assert.True(compiled.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "paused");
        var started = host.PauseReadUntilCancellation("/value");
        using var cancellation = new CancellationTokenSource();
        var pending = compiled.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try { await started.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { cancellation.Cancel(); }
        var canceled = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(canceled.Success);
        Assert.Contains("execution canceled", canceled.Failure?.Message, StringComparison.Ordinal);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/value", "fresh");
        var result = await compiled.RunAsync(fresh);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("fresh", result.ReturnValue);
    }

    private static string SuspendingProperty(bool setter) => setter ? """
        class C:
         def put(self,value):
          with open('/value') as f: self.saved=f.read()
         value=property(None,put)
        c=C()
        c.value=[]
        return c.saved
        """ : """
        class C:
         def get(self):
          with open('/value') as f: return f.read()
         value=property(get)
        return C().value
        """;

    [Theory]
    [InlineData("c.value=None")]
    [InlineData("c.value=other=None")]
    [InlineData("c.value,other=(None,None)")]
    [InlineData("c.value:object=None")]
    [InlineData("for c.value in [None]: pass")]
    [InlineData("c.value+=1")]
    public async Task LoweredAssignmentFormsAwaitPropertySetter(string assignment)
    {
        var source = "class C:\n def get(self):return 0\n def put(self,value):\n  with open('/value') as f:self.saved=f.read()\n value=property(get,put)\nclass Work:\n def run(self,c):\n" +
            string.Join('\n', assignment.Split('\n').Select(line => "  " + line)) +
            "\nc=C()\nWork().run(c)\nreturn c.saved\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "stored");
        var result = await compiled.RunAsync(host).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("stored", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultDeleteSlotAwaitsPropertyDeleter(bool explicitSlot)
    {
        var source = "class C:\n def remove(self):\n  with open('/value') as f:self.saved=f.read()\n value=property(None).deleter(remove)\nc=C()\n" +
            (explicitSlot ? "object.__delattr__(c,'value')" : "del c.value") + "\nreturn c.saved\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "deleted");
        var result = await compiled.RunAsync(host).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("deleted", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomSlotDescriptorsAwaitTheirBinding(bool deleting)
    {
        var source = "class Slot:\n def __get__(self,obj,owner):\n  with open('/value') as f:obj.saved=f.read()\n  return lambda *args:None\nclass C:pass\nc=C()\n" +
            (deleting ? "C.__delattr__=Slot()\ndel c.value" : "C.__setattr__=Slot()\nc.value=None") + "\nreturn c.saved\n";
        // Writing saved must delegate to the object slot to avoid reentering
        // the custom setter while its descriptor is still binding.
        source = source.Replace("obj.saved=f.read()", "object.__setattr__(obj,'saved',f.read())", StringComparison.Ordinal);
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "bound");
        var result = await compiled.RunAsync(host).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("bound", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}

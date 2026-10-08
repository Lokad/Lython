using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ZlibCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "zlib-callable-metadata-and-aliases", "import zlib\nfrom zlib import compress as encode,decompress as decode\nfor function in [encode,decode]:\n    print(function.__name__,function.__qualname__,function.__module__,function.__self__ is zlib)\n    print(type(function).__name__,repr(function),callable(function))\nmapping={encode:'encoder',decode:'decoder'}\nprint(mapping[zlib.compress],mapping[zlib.decompress],decode(encode(b'alias')))\n", "compress compress zlib True\nbuiltin_function_or_method <built-in function compress> True\ndecompress decompress zlib True\nbuiltin_function_or_method <built-in function decompress> True\nencoder decoder b'alias'\n" };
        yield return new object[] { "zlib-option-conversion-order-and-errors", "import zlib\nclass Index:\n    def __init__(self,name,value): self.name=name; self.value=value\n    def __index__(self): print(self.name); return self.value\nfor level in [-2,10,10**100]:\n    try: zlib.compress(b'x',Index('level',level),Index('window',15))\n    except Exception as error: print(type(error).__name__)\ntry: zlib.decompress(zlib.compress(b'x'),Index('window',15),Index('buffer',-1))\nexcept ValueError: print('ValueError')\ntry: zlib.compress('bad',Index('unexpected',1))\nexcept TypeError: print('TypeError')\n", "level\nwindow\nerror\nlevel\nwindow\nerror\nlevel\nOverflowError\nwindow\nbuffer\nValueError\nTypeError\n" };
        yield return new object[] { "zlib-omitted-options-and-explicit-none", "import zlib\ndata=zlib.compress(b'options',wbits=15)\nprint(zlib.decompress(data,bufsize=1),zlib.decompress(data,wbits=0))\nfor function,args,kwargs in [(zlib.compress,[b'x'],{'level':None}),(zlib.compress,[b'x'],{'wbits':None}),(zlib.decompress,[data],{'wbits':None}),(zlib.decompress,[data],{'bufsize':None})]:\n    try: function(*args,**kwargs)\n    except TypeError: print('TypeError')\n", "b'options' b'options'\nTypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "zlib-index-descriptors-and-propagated-errors", "import zlib\nclass Index:\n    @property\n    def __index__(self):\n        print('descriptor')\n        return lambda: 1\nprint(zlib.decompress(zlib.compress(b'descriptor',Index())))\nclass Bad:\n    def __index__(self): raise LookupError('guest')\nfor function,args in [(zlib.compress,[b'x',Bad()]),(zlib.decompress,[zlib.compress(b'x'),15,Bad()])]:\n    try: function(*args)\n    except LookupError as error: print(str(error))\n", "descriptor\nb'descriptor'\nguest\nguest\n" };
        yield return new object[] { "zlib-header-window-and-level-flags", "import zlib\nbase=zlib.compress(b'hello')\nfor window in range(8):\n    for level in range(4):\n        cmf=(window<<4)|8\n        flags=level<<6\n        flags+=(31-((cmf<<8|flags)%31))%31\n        data=bytes([cmf,flags])+base[2:]\n        print(window,level,zlib.decompress(data)==b'hello',zlib.decompress(data,0)==b'hello')\nfor cmf in [0x88,0x79]:\n    flags=(31-((cmf<<8)%31))%31\n    try: zlib.decompress(bytes([cmf,flags])+base[2:])\n    except zlib.error: print('error')\n", "0 0 True True\n0 1 True True\n0 2 True True\n0 3 True True\n1 0 True True\n1 1 True True\n1 2 True True\n1 3 True True\n2 0 True True\n2 1 True True\n2 2 True True\n2 3 True True\n3 0 True True\n3 1 True True\n3 2 True True\n3 3 True True\n4 0 True True\n4 1 True True\n4 2 True True\n4 3 True True\n5 0 True True\n5 1 True True\n5 2 True True\n5 3 True True\n6 0 True True\n6 1 True True\n6 2 True True\n6 3 True True\n7 0 True True\n7 1 True True\n7 2 True True\n7 3 True True\nerror\nerror\n" };
        yield return new object[] { "zlib-empty-streams-all-levels", "import zlib\nfor level in [-1,0,1,2,3,4,5,6,7,8,9]:\n    data=zlib.compress(b'',level)\n    print(level,data[0],data[1]>>6,zlib.decompress(data),zlib.decompress(data+b'ignored'))\n", "-1 120 2 b'' b''\n0 120 0 b'' b''\n1 120 0 b'' b''\n2 120 1 b'' b''\n3 120 1 b'' b''\n4 120 1 b'' b''\n5 120 1 b'' b''\n6 120 2 b'' b''\n7 120 3 b'' b''\n8 120 3 b'' b''\n9 120 3 b'' b''\n" };
        yield return new object[] { "zlib-all-levels-roundtrip-and-header", "import zlib\nfor level in [-1,0,1,2,3,4,5,6,7,8,9]:\n    data=b'hello hello hello'*40\n    compressed=zlib.compress(data,level)\n    print(level,isinstance(compressed,bytes),compressed[0],compressed[1]>>6,zlib.decompress(compressed)==data)\nprint(zlib.decompress(zlib.compress(b'')),zlib.decompress(zlib.compress(bytes(range(256))*3))==bytes(range(256))*3)\n", "-1 True 120 2 True\n0 True 120 0 True\n1 True 120 0 True\n2 True 120 1 True\n3 True 120 1 True\n4 True 120 1 True\n5 True 120 1 True\n6 True 120 2 True\n7 True 120 3 True\n8 True 120 3 True\n9 True 120 3 True\nb'' True\n" };
        yield return new object[] { "zlib-frozen-python-fixtures", "import zlib\nfor data in [bytes.fromhex('789c030000000001'),bytes.fromhex('789ccb48cdc9c957c84090003a2e067d'),bytes.fromhex('789c6360646266616563e7e0e4e2e6e1e5e3171014121611151397909492969195935750545256515553d7d0d4d2d6d1d5d33730343236313533b7b0b4b2b6b1b5b37770747276717573f7f0f4f2f6f1f5f30f080c0a0e090d0b8f888c8a8e898d8b4f484c4a4e494d4bcfc8cccacec9cdcb2f282c2a2e292d2bafa8acaaaea9adab6f686c6a6e696d6befe8eceaeee9edeb9f3071d2e42953a74d9f3173d6ec3973e7cd5fb070d1e2254b972d5fb172d5ea356bd7addfb071d3e62d5bb76ddfb173d7ee3d7bf7ed3f70f0d0e123478f1d3f71f2d4e93367cf9dbf70f1d2e52b57af5dbf71f3d6ed3b77efdd7ff0f0d1e3274f9f3d7ff1f2d5eb376fdfbdfff0f1d3e72f5fbf7dfff1f3d7ef3f7ffffd6718f5ff88f63f00a0627e90')]:\n    result=zlib.decompress(data)\n    print(len(result),result[:20],result[-10:])\n", "0 b'' b''\n17 b'hello hello hello' b'ello hello'\n768 b'\\x00\\x01\\x02\\x03\\x04\\x05\\x06\\x07\\x08\\t\\n\\x0b\\x0c\\r\\x0e\\x0f\\x10\\x11\\x12\\x13' b'\\xf6\\xf7\\xf8\\xf9\\xfa\\xfb\\xfc\\xfd\\xfe\\xff'\n" };
        yield return new object[] { "zlib-keywords-and-buffer-hints", "import zlib\ndata=b'hello hello hello'\ncompressed=zlib.compress(data,level=4,wbits=15)\nfor size in [0,1,2,16,16384,1000000]:\n    print(size,zlib.decompress(compressed,wbits=15,bufsize=size)==data)\nprint(zlib.decompress(compressed,0)==data)\n", "0 True\n1 True\n2 True\n16 True\n16384 True\n1000000 True\nTrue\n" };
        yield return new object[] { "zlib-trailing-input-and-concatenation", "import zlib\nfirst=bytes.fromhex('789ccb48cdc9c957c84090003a2e067d')\nsecond=zlib.compress(b'second')\nfor tail in [b'',b'garbage',b'\\x00'*20,second,first[:-1]]:\n    print(zlib.decompress(first+tail))\n", "b'hello hello hello'\nb'hello hello hello'\nb'hello hello hello'\nb'hello hello hello'\nb'hello hello hello'\n" };
        yield return new object[] { "zlib-every-truncated-prefix", "import zlib\nfor data in [bytes.fromhex('789c030000000001'),bytes.fromhex('789ccb48cdc9c957c84090003a2e067d')]:\n    failures=0\n    for length in range(len(data)):\n        try: zlib.decompress(data[:length])\n        except zlib.error: failures+=1\n    print(len(data),failures)\n", "8 8\n16 16\n" };
        yield return new object[] { "zlib-malformed-headers-payload-and-checksum", "import zlib\ngood=bytes.fromhex('789ccb48cdc9c957c84090003a2e067d')\ncases=[b'',b'x',b'not a zlib stream',bytes([0,0])+good[2:],bytes([0x78,0])+good[2:],good[:-1]+bytes([good[-1]^1]),good[:2]+b'\\x07'+good[3:]]\nfor data in cases:\n    try: zlib.decompress(data)\n    except zlib.error as error: print(type(error).__module__,type(error).__name__)\n    else: print('accepted')\n", "zlib error\nzlib error\nzlib error\nzlib error\nzlib error\nzlib error\nzlib error\n" };
        yield return new object[] { "zlib-error-identity-and-direct-bases", "import zlib\nfrom zlib import error\nprint(error is zlib.error,error.__name__,error.__module__,issubclass(error,Exception),error.__bases__==(Exception,))\ntry: zlib.decompress(b'invalid')\nexcept Exception as ex: print(type(ex) is error,isinstance(ex,error),isinstance(ex,Exception))\n", "True error zlib True True\nTrue True True\n" };
        yield return new object[] { "zlib-type-and-integer-errors", "import zlib\nfor data in ['x',1,None,[]]:\n    for function in [zlib.compress,zlib.decompress]:\n        try: function(data)\n        except Exception as ex: print(type(ex).__name__)\nfor level in [-2,10,1.5,'1',None,10**100]:\n    try: zlib.compress(b'x',level)\n    except Exception as ex: print(type(ex).__name__)\nfor size in [-1,1.5,'1',None,10**100]:\n    try: zlib.decompress(zlib.compress(b'x'),bufsize=size)\n    except Exception as ex: print(type(ex).__name__)\n", "TypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nerror\nerror\nTypeError\nTypeError\nTypeError\nOverflowError\nValueError\nTypeError\nTypeError\nTypeError\nOverflowError\n" };
        yield return new object[] { "zlib-argument-binding", "import zlib\nfor function,args,kwargs in [\n    (zlib.compress,[],{}),(zlib.decompress,[],{}),\n    (zlib.compress,[],{'data':b'x'}),(zlib.decompress,[],{'data':b'x'}),\n    (zlib.compress,[b'x',1],{'level':2}),\n    (zlib.decompress,[b'x',15],{'wbits':15}),\n    (zlib.compress,[b'x'],{'unknown':1}),\n    (zlib.decompress,[b'x'],{'unknown':1}),\n    (zlib.compress,[b'x',1,15,0],{}),\n    (zlib.decompress,[b'x',15,0,0],{})]:\n    try: function(*args,**kwargs)\n    except TypeError: print('TypeError')\n", "TypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "zlib-type-index-protocol", "import zlib\nclass Index:\n    def __init__(self,value): self.value=value\n    def __index__(self):\n        print('index',self.value)\n        return self.value\nlevel=Index(1)\nlevel.__index__=lambda:9\ncompressed=zlib.compress(b'hello',level,Index(15))\nprint(zlib.decompress(compressed,Index(15),Index(1)))\nclass Bad:\n    def __index__(self): return 'wrong'\nfor function,args in [(zlib.compress,[b'x',Bad()]),(zlib.decompress,[compressed,15,Bad()])]:\n    try: function(*args)\n    except TypeError: print('TypeError')\n", "index 1\nindex 15\nindex 15\nindex 1\nb'hello'\nTypeError\nTypeError\n" };
        yield return new object[] { "zlib-booleans-and-finite-constants", "import zlib\nprint(zlib.DEFLATED,zlib.MAX_WBITS,zlib.DEF_BUF_SIZE,zlib.Z_NO_COMPRESSION,zlib.Z_BEST_SPEED,zlib.Z_BEST_COMPRESSION,zlib.Z_DEFAULT_COMPRESSION)\nfor level in [False,True]:\n    data=zlib.compress(b'boolean',level)\n    print(level,zlib.decompress(data,15,False),zlib.decompress(data,15,True))\n", "8 15 16384 0 1 9 -1\nFalse b'boolean' b'boolean'\nTrue b'boolean' b'boolean'\n" };
        yield return new object[] { "zlib-expansion-json-and-struct-composition", "import zlib,json,struct\ndata=bytes.fromhex('789cedc13101000000c2a0da8b6f0d0fa00000000000000000000000000000000000000078306547a11d')\nresult=zlib.decompress(data,bufsize=1)\nprint(len(result),result[:3],result[-3:])\nsource=json.dumps({'name':'\u00e9\u03a9\ud83d\ude00','count':3}).encode('utf-8')\nframe=struct.pack('>I',len(source))+zlib.compress(source)\nprint(struct.unpack('>I',frame[:4]),json.loads(zlib.decompress(frame[4:]).decode('utf-8')))\n", "20000 b'xxx' b'xxx'\n(48,) {'name': '\u00e9\u03a9\ud83d\ude00', 'count': 3}\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task WrappedByteProgramsMatchPython(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("zlib.compress(b'x',wbits=14)")]
    [InlineData("zlib.compress(b'x',wbits=-15)")]
    [InlineData("zlib.compress(b'x',wbits=31)")]
    [InlineData("zlib.decompress(b'x',wbits=14)")]
    [InlineData("zlib.decompress(b'x',wbits=-15)")]
    [InlineData("zlib.decompress(b'x',wbits=31)")]
    [InlineData("zlib.decompress(b'x',wbits=47)")]
    [InlineData("zlib.decompress(bytes.fromhex('78bb00000001'))")]
    [InlineData("zlib.compressobj()")]
    [InlineData("zlib.decompressobj()")]
    [InlineData("zlib.adler32(b'x')")]
    [InlineData("zlib.crc32(b'x')")]
    public async Task DeferredOptionsAndObjectsFailExplicitly(string operation)
    {
        var script = new LythonEngine().Compile("import zlib\ntry: "+operation+"\nexcept NotImplementedError: print('unsupported')\n");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("unsupported\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task OptionConversionsAndBinaryPublicationAwaitMediatedFiles()
    {
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        immediate.SeedBytes("/input.z", Convert.FromHexString("789cab56ca4bcc4d55b252503abcf2dcca0ff3673428e9282825e797e69500058d6b01d3f90c4c"));
        delayed.SeedBytes("/input.z", Convert.FromHexString("789cab56ca4bcc4d55b252503abcf2dcca0ff3673428e9282825e797e69500058d6b01d3f90c4c"));
        immediate.SeedFile("/index.txt", "1");
        delayed.SeedFile("/index.txt", "1");
        var script = new LythonEngine().Compile(HostSource);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(immediate), await script.RunAsync(delayed) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("index window 1\nindex buffer 1\n{'name': '\u00e9\u03a9\ud83d\ude00', 'count': 3}\nindex level 1\nindex window 1\n(32,) True\n32\n", result.StandardOutput);
        }
        Assert.Equal(Convert.FromHexString("7b226e616d65223a2022c3a9cea9f09f9880222c2022636f756e74223a20337d"), immediate.ReadBytes("/output.bin"));
        Assert.Equal(Convert.FromHexString("7b226e616d65223a2022c3a9cea9f09f9880222c2022636f756e74223a20337d"), delayed.ReadBytes("/output.bin"));
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedConversionsAndPublicationCancelWithoutChangingOutput(bool write)
    {
        var script = new LythonEngine().Compile(HostSource);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedBytes("/input.z", Convert.FromHexString("789cab56ca4bcc4d55b252503abcf2dcca0ff3673428e9282825e797e69500058d6b01d3f90c4c"));
        host.SeedFile("/index.txt", "1");
        host.SeedBytes("/output.bin", [1, 2, 3]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = write ? host.PauseWriteUntilCancellation("/output.bin") : host.PauseReadUntilCancellation("/index.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message);
        Assert.Equal(new byte[] { 1, 2, 3 }, host.ReadBytes("/output.bin"));
        var fresh = new DelayedLythonHost();
        fresh.SeedBytes("/input.z", Convert.FromHexString("789cab56ca4bcc4d55b252503abcf2dcca0ff3673428e9282825e797e69500058d6b01d3f90c4c"));
        fresh.SeedFile("/index.txt", "1");
        var retry = await script.RunAsync(fresh);
        Assert.True(retry.Success, retry.Failure?.Message);
        Assert.Equal("index window 1\nindex buffer 1\n{'name': '\u00e9\u03a9\ud83d\ude00', 'count': 3}\nindex level 1\nindex window 1\n(32,) True\n32\n", retry.StandardOutput);
        Assert.Equal(Convert.FromHexString("7b226e616d65223a2022c3a9cea9f09f9880222c2022636f756e74223a20337d"), fresh.ReadBytes("/output.bin"));
    }

    private const string HostSource = "import zlib,json,struct\nfrom pathlib import Path\nclass Index:\n    def __init__(self,label,value):\n        self.label=label\n        self.value=value\n    def __index__(self):\n        with open('index.txt') as file: seed=file.read()\n        print('index',self.label,seed)\n        return self.value\nlevel=Index('level',1)\nlevel.__index__=lambda: 9\ncompressed=Path('input.z').read_bytes()\nraw=zlib.decompress(compressed,Index('window',15),Index('buffer',1))\nprint(json.loads(raw.decode('utf-8')))\nagain=zlib.compress(raw,level,Index('window',15))\nframe=struct.pack('>I',len(raw))+again\nprint(struct.unpack('>I',frame[:4]),zlib.decompress(frame[4:])==raw)\nprint(Path('output.bin').write_bytes(zlib.decompress(frame[4:])))\n";
}

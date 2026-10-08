using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StructCompatibilityTests
{
    [Theory]
    [InlineData("__index__", "H", "return int(value)")]
    [InlineData("__float__", "f", "return float(value)")]
    [InlineData("__bool__", "?", "return bool(value)")]
    public async Task SuspendedConversionsCancelWithoutPublishing(string hook, string code, string conversion)
    {
        var source = "import struct\nfrom pathlib import Path\nclass Number:\n    def " + hook +
            "(self):\n        with open('number.txt') as file: value=file.read()\n        " + conversion +
            "\nPath('packed.bin').write_bytes(struct.pack('>" + code + "',Number()))\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/number.txt", "2");
        host.SeedBytes("/packed.bin", [1, 2, 3]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = host.PauseReadUntilCancellation("/number.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message);
        Assert.Equal(new byte[] { 1, 2, 3 }, host.ReadBytes("/packed.bin"));
        var retryHost = new DelayedLythonHost();
        retryHost.SeedFile("/number.txt", "2");
        var retry = await script.RunAsync(retryHost);
        Assert.True(retry.Success, retry.Failure?.Message);
    }

    [Theory]
    [InlineData("struct.calcsize('@I')")]
    [InlineData("struct.calcsize('@P')")]
    [InlineData("struct.calcsize('n')")]
    [InlineData("struct.pack('I',1)")]
    [InlineData("struct.Struct('>I')")]
    [InlineData("struct.pack_into('>I',b'1234',0,1)")]
    [InlineData("struct.unpack_from('>I',b'1234')")]
    [InlineData("struct.unpack('>0p',b'')")]
    public async Task DeferredSurfacesFailExplicitly(string call)
    {
        var script = new LythonEngine().Compile("import struct\ntry: " + call + "\nexcept NotImplementedError: print('unsupported')\n");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("unsupported\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("struct.calcsize(format='>I')")]
    [InlineData("struct.unpack(format='>I',buffer=b'1234')")]
    [InlineData("struct.iter_unpack(format='>I',buffer=b'1234')")]
    [InlineData("struct.pack(format='>I')")]
    public void DirectKnownCallsRejectKeywordsStatically(string call)
    {
        var script = new LythonEngine().Compile("import struct\n" + call);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3151");
    }

    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "struct-half-ieee-expansion-inventory", "import struct,hashlib\ndigest=hashlib.sha256()\nfor sign in [0,32768]:\n    for exponent in range(32):\n        for fraction in [0,1,511,512,1022,1023]:\n            raw=struct.pack('>H',sign+exponent*1024+fraction)\n            number=struct.unpack('>e',raw)[0]\n            digest.update(struct.pack('>d',number))\n            digest.update(struct.pack('>e',number))\nprint(digest.hexdigest())\n", "a1f8fead60a8b834cc53bd204125a2a9c23ac67ad4f0cfb0e2fa141b5cac3cf6\n" };
        yield return new object[] { "struct-nan-payload-conversion", "import struct\nfor raw in ['7ff0000000000001','7ff8000000000001','7fffffffffffffff','fff0000000000001','fff8000000000001','ffffffffffffffff']:\n    value=struct.unpack('>d',bytes.fromhex(raw))[0]\n    print(raw,struct.pack('>d',value).hex(),struct.pack('>f',value).hex(),struct.pack('>e',value).hex())\nfor raw in ['7f800001','7fc00001','7fffffff','ff800001','ffc00001','ffffffff']:\n    value=struct.unpack('>f',bytes.fromhex(raw))[0]\n    print(raw,struct.pack('>d',value).hex(),struct.pack('>f',value).hex())\n", "7ff0000000000001 7ff0000000000001 7fc00000 7e00\n7ff8000000000001 7ff8000000000001 7fc00000 7e00\n7fffffffffffffff 7fffffffffffffff 7fffffff 7e00\nfff0000000000001 fff0000000000001 ffc00000 fe00\nfff8000000000001 fff8000000000001 ffc00000 fe00\nffffffffffffffff ffffffffffffffff ffffffff fe00\n7f800001 7ff8000020000000 7fc00001\n7fc00001 7ff8000020000000 7fc00001\n7fffffff 7fffffffe0000000 7fffffff\nff800001 fff8000020000000 ffc00001\nffc00001 fff8000020000000 ffc00001\nffffffff ffffffffe0000000 ffffffff\n" };
        yield return new object[] { "struct-slot-descriptors-and-error-priority", "import struct\nclass Index:\n    def __index__(self): return 7\nclass BadIndex:\n    def __index__(self): return 1.5\nclass BadFloat:\n    def __float__(self): return 7\nclass NoFloat:\n    __float__=None\n    def __index__(self): return 7\nclass Descriptor:\n    def __get__(self,obj,owner):\n        print('descriptor',owner.__name__)\n        return lambda:3\nclass Described:\n    __index__=Descriptor()\nfor value in [Index(),BadIndex(),BadFloat(),NoFloat(),Described()]:\n    for code in ['i','d']:\n        try: print(code,struct.pack('>'+code,value).hex())\n        except Exception as error: print(code,type(error).__name__)\n", "i 00000007\nd 401c000000000000\ni TypeError\nd error\ni error\nd error\ni 00000007\nd error\ndescriptor Described\ni 00000003\ndescriptor Described\nd 4008000000000000\n" };
        yield return new object[] { "struct-byte-fields-and-iterator-hints", "import struct,operator\ndata=struct.pack('>5s4p?',b'xy',b'abcdef',True)\nprint(data.hex(),struct.unpack('>5s4p?',data))\nprint(struct.unpack('>?',bytes([254])))\niterator=struct.iter_unpack('>H',struct.pack('>3H',10,20,30))\nprint(iterator.__length_hint__(),next(iterator),iterator.__length_hint__())\nprint(next(iter(iterator)),operator.length_hint(iterator),list(iterator),iterator.__length_hint__())\nprint(struct.pack('>0p',b'xyz').hex())\n", "78790000000361626301 (b'xy\\x00\\x00\\x00', b'abc', True)\n(True,)\n3 (10,) 2\n(20,) 1 [(30,)] 0\n\n" };
        yield return new object[] { "struct-standard-integer-layouts", "import struct\nvalues=(-128,255,-32768,65535,-2147483648,4294967295,-2147483648,4294967295,-9223372036854775808,18446744073709551615)\nfor prefix in ['<','>','=','!']:\n    fmt=prefix+'bBhHiIlLqQ'\n    data=struct.pack(fmt,*values)\n    print(prefix,struct.calcsize(fmt),data.hex(),struct.unpack(fmt,data)==values)\n", "< 38 80ff0080ffff00000080ffffffff00000080ffffffff0000000000000080ffffffffffffffff True\n> 38 80ff8000ffff80000000ffffffff80000000ffffffff8000000000000000ffffffffffffffff True\n= 38 80ff0080ffff00000080ffffffff00000080ffffffff0000000000000080ffffffffffffffff True\n! 38 80ff8000ffff80000000ffffffff80000000ffffffff8000000000000000ffffffffffffffff True\n" };
        yield return new object[] { "struct-integer-ranges", "import struct\nfor code,bits,signed in [('b',8,True),('B',8,False),('h',16,True),('H',16,False),('i',32,True),('I',32,False),('l',32,True),('L',32,False),('q',64,True),('Q',64,False)]:\n    low=-(2**(bits-1)) if signed else 0\n    high=2**(bits-1)-1 if signed else 2**bits-1\n    for value in [low-1,low,high,high+1,10**100]:\n        try: print(code,value,struct.pack('>'+code,value).hex())\n        except struct.error: print(code,value,'error')\n", "b -129 error\nb -128 80\nb 127 7f\nb 128 error\nb 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nB -1 error\nB 0 00\nB 255 ff\nB 256 error\nB 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nh -32769 error\nh -32768 8000\nh 32767 7fff\nh 32768 error\nh 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nH -1 error\nH 0 0000\nH 65535 ffff\nH 65536 error\nH 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\ni -2147483649 error\ni -2147483648 80000000\ni 2147483647 7fffffff\ni 2147483648 error\ni 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nI -1 error\nI 0 00000000\nI 4294967295 ffffffff\nI 4294967296 error\nI 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nl -2147483649 error\nl -2147483648 80000000\nl 2147483647 7fffffff\nl 2147483648 error\nl 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nL -1 error\nL 0 00000000\nL 4294967295 ffffffff\nL 4294967296 error\nL 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nq -9223372036854775809 error\nq -9223372036854775808 8000000000000000\nq 9223372036854775807 7fffffffffffffff\nq 9223372036854775808 error\nq 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\nQ -1 error\nQ 0 0000000000000000\nQ 18446744073709551615 ffffffffffffffff\nQ 18446744073709551616 error\nQ 10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000 error\n" };
        yield return new object[] { "struct-byte-padding-and-string-fields", "import struct\nfor fmt,values in [('>2x3c5s6p',(b'A',b'B',b'C',b'abcdef',b'xyz')),('<0s0c0x',(b'nonempty',)),('>1s1p',(b'abc',b'xyz')),('<4s4p',(b'xy',b'123456'))]:\n    data=struct.pack(fmt,*values)\n    print(fmt,struct.calcsize(fmt),data.hex(),struct.unpack(fmt,data))\ndata=struct.pack('>300p',b'x'*299)\nprint(len(data),data[0],len(struct.unpack('>300p',data)[0]),data[-1])\n", ">2x3c5s6p 16 000041424361626364650378797a0000 (b'A', b'B', b'C', b'abcde', b'xyz')\n<0s0c0x 0  (b'',)\n>1s1p 2 6100 (b'a', b'')\n<4s4p 8 7879000003313233 (b'xy\\x00\\x00', b'123')\n300 255 255 120\n" };
        yield return new object[] { "struct-float-ieee-and-signed-zero", "import struct,math\nfor prefix in ['<','>','=','!']:\n    for code in ['e','f','d']:\n        for value in [0.0,-0.0,1.0,-2.5,1.5,float('inf'),float('-inf')]:\n            data=struct.pack(prefix+code,value)\n            result=struct.unpack(prefix+code,data)[0]\n            print(prefix,code,repr(value),data.hex(),repr(result),math.copysign(1.0,result))\n", "< e 0.0 0000 0.0 1.0\n< e -0.0 0080 -0.0 -1.0\n< e 1.0 003c 1.0 1.0\n< e -2.5 00c1 -2.5 -1.0\n< e 1.5 003e 1.5 1.0\n< e inf 007c inf 1.0\n< e -inf 00fc -inf -1.0\n< f 0.0 00000000 0.0 1.0\n< f -0.0 00000080 -0.0 -1.0\n< f 1.0 0000803f 1.0 1.0\n< f -2.5 000020c0 -2.5 -1.0\n< f 1.5 0000c03f 1.5 1.0\n< f inf 0000807f inf 1.0\n< f -inf 000080ff -inf -1.0\n< d 0.0 0000000000000000 0.0 1.0\n< d -0.0 0000000000000080 -0.0 -1.0\n< d 1.0 000000000000f03f 1.0 1.0\n< d -2.5 00000000000004c0 -2.5 -1.0\n< d 1.5 000000000000f83f 1.5 1.0\n< d inf 000000000000f07f inf 1.0\n< d -inf 000000000000f0ff -inf -1.0\n> e 0.0 0000 0.0 1.0\n> e -0.0 8000 -0.0 -1.0\n> e 1.0 3c00 1.0 1.0\n> e -2.5 c100 -2.5 -1.0\n> e 1.5 3e00 1.5 1.0\n> e inf 7c00 inf 1.0\n> e -inf fc00 -inf -1.0\n> f 0.0 00000000 0.0 1.0\n> f -0.0 80000000 -0.0 -1.0\n> f 1.0 3f800000 1.0 1.0\n> f -2.5 c0200000 -2.5 -1.0\n> f 1.5 3fc00000 1.5 1.0\n> f inf 7f800000 inf 1.0\n> f -inf ff800000 -inf -1.0\n> d 0.0 0000000000000000 0.0 1.0\n> d -0.0 8000000000000000 -0.0 -1.0\n> d 1.0 3ff0000000000000 1.0 1.0\n> d -2.5 c004000000000000 -2.5 -1.0\n> d 1.5 3ff8000000000000 1.5 1.0\n> d inf 7ff0000000000000 inf 1.0\n> d -inf fff0000000000000 -inf -1.0\n= e 0.0 0000 0.0 1.0\n= e -0.0 0080 -0.0 -1.0\n= e 1.0 003c 1.0 1.0\n= e -2.5 00c1 -2.5 -1.0\n= e 1.5 003e 1.5 1.0\n= e inf 007c inf 1.0\n= e -inf 00fc -inf -1.0\n= f 0.0 00000000 0.0 1.0\n= f -0.0 00000080 -0.0 -1.0\n= f 1.0 0000803f 1.0 1.0\n= f -2.5 000020c0 -2.5 -1.0\n= f 1.5 0000c03f 1.5 1.0\n= f inf 0000807f inf 1.0\n= f -inf 000080ff -inf -1.0\n= d 0.0 0000000000000000 0.0 1.0\n= d -0.0 0000000000000080 -0.0 -1.0\n= d 1.0 000000000000f03f 1.0 1.0\n= d -2.5 00000000000004c0 -2.5 -1.0\n= d 1.5 000000000000f83f 1.5 1.0\n= d inf 000000000000f07f inf 1.0\n= d -inf 000000000000f0ff -inf -1.0\n! e 0.0 0000 0.0 1.0\n! e -0.0 8000 -0.0 -1.0\n! e 1.0 3c00 1.0 1.0\n! e -2.5 c100 -2.5 -1.0\n! e 1.5 3e00 1.5 1.0\n! e inf 7c00 inf 1.0\n! e -inf fc00 -inf -1.0\n! f 0.0 00000000 0.0 1.0\n! f -0.0 80000000 -0.0 -1.0\n! f 1.0 3f800000 1.0 1.0\n! f -2.5 c0200000 -2.5 -1.0\n! f 1.5 3fc00000 1.5 1.0\n! f inf 7f800000 inf 1.0\n! f -inf ff800000 -inf -1.0\n! d 0.0 0000000000000000 0.0 1.0\n! d -0.0 8000000000000000 -0.0 -1.0\n! d 1.0 3ff0000000000000 1.0 1.0\n! d -2.5 c004000000000000 -2.5 -1.0\n! d 1.5 3ff8000000000000 1.5 1.0\n! d inf 7ff0000000000000 inf 1.0\n! d -inf fff0000000000000 -inf -1.0\n" };
        yield return new object[] { "struct-nan-sign-and-payload", "import struct,math\nfor code,hexes in [('e',['007e','00fe']),('f',['0000c07f','0000c0ff']),('d',['000000000000f87f','000000000000f8ff'])]:\n    for data in hexes:\n        value=struct.unpack('<'+code,bytes.fromhex(data))[0]\n        print(code,math.isnan(value),math.copysign(1.0,value),struct.pack('<'+code,value).hex())\nprint(struct.pack('>d',float('nan')).hex(),struct.pack('>d',-float('nan')).hex())\n", "e True 1.0 007e\ne True -1.0 00fe\nf True 1.0 0000c07f\nf True -1.0 0000c0ff\nd True 1.0 000000000000f87f\nd True -1.0 000000000000f8ff\n7ff8000000000000 fff8000000000000\n" };
        yield return new object[] { "struct-half-rounding-and-overflow", "import struct\nfor code in ['e','f']:\n    for value in [0.000000059604644775390625,1.00048828125,1.00146484375,65504.0,65519.0,65520.0,-65520.0,1e40]:\n        try: print(code,repr(value),struct.pack('>'+code,value).hex())\n        except OverflowError: print(code,repr(value),'OverflowError')\n", "e 5.960464477539063e-08 0001\ne 1.00048828125 3c00\ne 1.00146484375 3c02\ne 65504.0 7bff\ne 65519.0 7bff\ne 65520.0 OverflowError\ne -65520.0 OverflowError\ne 1e+40 OverflowError\nf 5.960464477539063e-08 33800000\nf 1.00048828125 3f801000\nf 1.00146484375 3f803000\nf 65504.0 477fe000\nf 65519.0 477fef00\nf 65520.0 477ff000\nf -65520.0 c77ff000\nf 1e+40 OverflowError\n" };
        yield return new object[] { "struct-format-whitespace-and-zero-counts", "import struct\nfor fmt in ['<','>','=','','< 2h 3x 0c 0i 0s','>0?0e0d0q','!b\\tH\\nI','<1x2s']:\n    try:\n        size=struct.calcsize(fmt)\n        print(repr(fmt),size)\n    except struct.error: print(repr(fmt),'error')\n", "'<' 0\n'>' 0\n'=' 0\n'' 0\n'< 2h 3x 0c 0i 0s' 7\n'>0?0e0d0q' 0\n'!b\\tH\\nI' 7\n'<1x2s' 3\n" };
        yield return new object[] { "struct-malformed-format-errors", "import struct\nfor fmt in ['<2 h','<2','<z','<>i','<1-2i','<3.2i','<\\x00i',' <i','<99999999999999999999999999999999x','<9223372036854775807Q']:\n    for call in [lambda:struct.calcsize(fmt),lambda:struct.pack(fmt),lambda:struct.unpack(fmt,b'')]:\n        try: print(repr(fmt),call())\n        except struct.error: print(repr(fmt),'error')\n", "'<2 h' error\n'<2 h' error\n'<2 h' error\n'<2' error\n'<2' error\n'<2' error\n'<z' error\n'<z' error\n'<z' error\n'<>i' error\n'<>i' error\n'<>i' error\n'<1-2i' error\n'<1-2i' error\n'<1-2i' error\n'<3.2i' error\n'<3.2i' error\n'<3.2i' error\n'<\\x00i' error\n'<\\x00i' error\n'<\\x00i' error\n' <i' error\n' <i' error\n' <i' error\n'<99999999999999999999999999999999x' error\n'<99999999999999999999999999999999x' error\n'<99999999999999999999999999999999x' error\n'<9223372036854775807Q' error\n'<9223372036854775807Q' error\n'<9223372036854775807Q' error\n" };
        yield return new object[] { "struct-exact-size-and-empty-unpack", "import struct\nfor fmt,data in [('<I',b''),('<I',b'\\x01\\x00\\x00'),('<I',b'\\x01\\x00\\x00\\x00'),('<I',b'\\x01\\x00\\x00\\x00x'),('<',b''),('<0s',b''),('<0c',b'')]:\n    try: print(fmt,data.hex(),struct.unpack(fmt,data))\n    except struct.error: print(fmt,data.hex(),'error')\nfor fmt,args in [('<I',()),('<I',(1,2)),('<0s',()),('<0s',(b'',)),('<0c',(b'',))]:\n    try: print(fmt,repr(args),struct.pack(fmt,*args).hex())\n    except struct.error: print(fmt,repr(args),'error')\n", "<I  error\n<I 010000 error\n<I 01000000 (1,)\n<I 0100000078 error\n<  ()\n<0s  (b'',)\n<0c  ()\n<I () error\n<I (1, 2) error\n<0s () error\n<0s (b'',) \n<0c (b'',) error\n" };
        yield return new object[] { "struct-index-float-and-truth-slots", "import struct\nclass Number:\n    def __index__(self):\n        print('index')\n        return 513\n    def __float__(self):\n        print('float')\n        return 1.5\n    def __bool__(self):\n        print('bool')\n        return False\nnumber=Number()\nprint(struct.pack('>Hf?',number,number,number).hex())\nprint(struct.unpack('>Hf?',struct.pack('>Hf?',True,2,3)))\nclass Bad:\n    def __index__(self): raise ValueError('index failure')\n    def __float__(self): raise KeyError('float failure')\n    def __bool__(self): raise RuntimeError('truth failure')\nfor code in ['H','f','?']:\n    try: struct.pack('>'+code,Bad())\n    except Exception as error: print(code,type(error).__name__)\n", "index\nfloat\nbool\n02013fc0000000\n(1, 2.0, True)\nH ValueError\nf error\n? RuntimeError\n" };
        yield return new object[] { "struct-value-type-errors", "import struct\nfor fmt,args in [('<I',(1.2,)),('<I',('3',)),('<f',('3',)),('<c',(b'xy',)),('<c',('x',)),('<s',('x',)),('<p',(3,)),('<f',(10**1000,))]:\n    try: struct.pack(fmt,*args)\n    except Exception as error: print(fmt,type(error).__name__)\nfor data in ['abcd',[0,0,0,0],4,None]:\n    try: struct.unpack('<I',data)\n    except Exception as error: print(type(error).__name__)\n", "<I error\n<I error\n<f error\n<c error\n<c error\n<s error\n<p error\n<f error\nTypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "struct-format-input-types", "import struct\nfor fmt in [b'<I','<I',b'<2s',3,None,'<\u20ac',bytes([60,255])]:\n    try: print(repr(fmt),struct.calcsize(fmt))\n    except Exception as error: print(repr(fmt),type(error).__name__)\n", "b'<I' 4\n'<I' 4\nb'<2s' 2\n3 TypeError\nNone TypeError\n'<\u20ac' UnicodeEncodeError\nb'<\\xff' UnicodeEncodeError\n" };
        yield return new object[] { "struct-lazy-iterator-shared-cursor", "import struct,operator\ndata=struct.pack('>6H',1,2,3,4,5,6)\niterator=struct.iter_unpack('>2H',data)\nprint(iter(iterator) is iterator,operator.length_hint(iterator),next(iterator),operator.length_hint(iterator))\nprint(list(iterator),operator.length_hint(iterator),next(iterator,'done'))\nprint(list(struct.iter_unpack('>H',b'')))\nfor fmt,payload in [('<',b''),('<0s',b''),('<I',b'x')]:\n    try: struct.iter_unpack(fmt,payload)\n    except struct.error: print(fmt,'error')\n", "True 3 (1, 2) 2\n[(3, 4), (5, 6)] 0 done\n[]\n< error\n<0s error\n<I error\n" };
        yield return new object[] { "struct-exception-identity", "import struct\nfrom struct import error\nprint(error is struct.error,issubclass(error,Exception),error.__name__,error.__module__)\ntry: struct.unpack('>I',b'x')\nexcept error as value: print(type(value) is error,isinstance(value,error),isinstance(value,Exception))\n", "True True error struct\nTrue True True\n" };
        yield return new object[] { "struct-positional-only-binding", "import struct\nfunctions=[struct.calcsize,struct.unpack,struct.iter_unpack,struct.pack]\nfor function in functions:\n    try: function(format='>I',buffer=b'1234')\n    except TypeError: print('TypeError')\n", "TypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "struct-large-calcsize-without-materialization", "import struct\nfor fmt in ['<2147483648x','<1073741824Q','<0Q0d0c0x0s']:\n    print(fmt,struct.calcsize(fmt))\n", "<2147483648x 2147483648\n<1073741824Q 8589934592\n<0Q0d0c0x0s 0\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task StandardFormatsMatchPython(string name, string source, string expected)
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

    [Fact]
    public async Task TypeSlotsCanSuspendAndPublishExactBytes()
    {
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        immediate.SeedFile("/index.txt", "514");
        delayed.SeedFile("/index.txt", "514");
        immediate.SeedFile("/float.txt", "1.5");
        delayed.SeedFile("/float.txt", "1.5");
        immediate.SeedFile("/truth.txt", "yes");
        delayed.SeedFile("/truth.txt", "yes");
        var script = new LythonEngine().Compile("import struct\nfrom pathlib import Path\nclass Number:\n    def __index__(self):\n        with open('index.txt') as file: value=file.read()\n        print('index',value)\n        return int(value)\n    def __float__(self):\n        with open('float.txt') as file: value=file.read()\n        print('float',value)\n        return float(value)\n    def __bool__(self):\n        with open('truth.txt') as file: value=file.read()\n        print('truth',value)\n        return value=='yes'\nnumber=Number()\nnumber.__index__=lambda:999\nnumber.__float__=lambda:4.0\nnumber.__bool__=lambda:False\ndata=struct.pack('>Hf?',number,number,number)\nprint(data.hex(),struct.unpack('>Hf?',data))\nprint(Path('packed.bin').write_bytes(data),Path('packed.bin').read_bytes().hex())\n");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(immediate), await script.RunAsync(delayed) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("index 514\nfloat 1.5\ntruth yes\n02023fc0000001 (514, 1.5, True)\n7 02023fc0000001\n", result.StandardOutput);
        }
        Assert.Equal(Convert.FromHexString("02023fc0000001"), immediate.ReadBytes("/packed.bin"));
        Assert.Equal(Convert.FromHexString("02023fc0000001"), delayed.ReadBytes("/packed.bin"));
        Assert.True(delayed.CompletedAsynchronously > 0);
    }
}

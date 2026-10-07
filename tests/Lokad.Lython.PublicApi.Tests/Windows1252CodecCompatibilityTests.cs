using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class Windows1252CodecCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "cp1252-all-byte-decoding", "data=bytes(range(256))\nfor errors in ['ignore','replace','backslashreplace']:\n    text=data.decode('cp1252',errors)\n    print(errors,len(text),text.encode('utf-8').hex())\ninvalid=[]\nfor value in range(256):\n    try: bytes([value]).decode('cp1252')\n    except UnicodeDecodeError: invalid.append(value)\nprint(invalid)\n", "ignore 251 000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7fe282ace2809ac692e2809ee280a6e280a0e280a1cb86e280b0c5a0e280b9c592c5bde28098e28099e2809ce2809de280a2e28093e28094cb9ce284a2c5a1e280bac593c5bec5b8c2a0c2a1c2a2c2a3c2a4c2a5c2a6c2a7c2a8c2a9c2aac2abc2acc2adc2aec2afc2b0c2b1c2b2c2b3c2b4c2b5c2b6c2b7c2b8c2b9c2bac2bbc2bcc2bdc2bec2bfc380c381c382c383c384c385c386c387c388c389c38ac38bc38cc38dc38ec38fc390c391c392c393c394c395c396c397c398c399c39ac39bc39cc39dc39ec39fc3a0c3a1c3a2c3a3c3a4c3a5c3a6c3a7c3a8c3a9c3aac3abc3acc3adc3aec3afc3b0c3b1c3b2c3b3c3b4c3b5c3b6c3b7c3b8c3b9c3bac3bbc3bcc3bdc3bec3bf\nreplace 256 000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7fe282acefbfbde2809ac692e2809ee280a6e280a0e280a1cb86e280b0c5a0e280b9c592efbfbdc5bdefbfbdefbfbde28098e28099e2809ce2809de280a2e28093e28094cb9ce284a2c5a1e280bac593efbfbdc5bec5b8c2a0c2a1c2a2c2a3c2a4c2a5c2a6c2a7c2a8c2a9c2aac2abc2acc2adc2aec2afc2b0c2b1c2b2c2b3c2b4c2b5c2b6c2b7c2b8c2b9c2bac2bbc2bcc2bdc2bec2bfc380c381c382c383c384c385c386c387c388c389c38ac38bc38cc38dc38ec38fc390c391c392c393c394c395c396c397c398c399c39ac39bc39cc39dc39ec39fc3a0c3a1c3a2c3a3c3a4c3a5c3a6c3a7c3a8c3a9c3aac3abc3acc3adc3aec3afc3b0c3b1c3b2c3b3c3b4c3b5c3b6c3b7c3b8c3b9c3bac3bbc3bcc3bdc3bec3bf\nbackslashreplace 271 000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7fe282ac5c783831e2809ac692e2809ee280a6e280a0e280a1cb86e280b0c5a0e280b9c5925c783864c5bd5c7838665c783930e28098e28099e2809ce2809de280a2e28093e28094cb9ce284a2c5a1e280bac5935c783964c5bec5b8c2a0c2a1c2a2c2a3c2a4c2a5c2a6c2a7c2a8c2a9c2aac2abc2acc2adc2aec2afc2b0c2b1c2b2c2b3c2b4c2b5c2b6c2b7c2b8c2b9c2bac2bbc2bcc2bdc2bec2bfc380c381c382c383c384c385c386c387c388c389c38ac38bc38cc38dc38ec38fc390c391c392c393c394c395c396c397c398c399c39ac39bc39cc39dc39ec39fc3a0c3a1c3a2c3a3c3a4c3a5c3a6c3a7c3a8c3a9c3aac3abc3acc3adc3aec3afc3b0c3b1c3b2c3b3c3b4c3b5c3b6c3b7c3b8c3b9c3bac3bbc3bcc3bdc3bec3bf\n[129, 141, 143, 144, 157]\n" };
        yield return new object[] { "cp1252-encoding-errors", "text='A\\u20ac\\u0152\\u0153\\xe9\\u03a9\\U0001f600'\nfor errors in ['strict','ignore','replace','backslashreplace']:\n    try: print(errors,text.encode('cp1252',errors).hex())\n    except UnicodeEncodeError: print(errors,'UnicodeEncodeError')\n", "strict UnicodeEncodeError\nignore 41808c9ce9\nreplace 41808c9ce93f3f\nbackslashreplace 41808c9ce95c75303361395c553030303166363030\n" };
        yield return new object[] { "cp1252-roundtrip-surfaces", "data=bytes([i for i in range(256) if i not in [129,141,143,144,157]])\ntext=data.decode('cp1252')\nprint(len(text),text.encode('cp1252')==data,bytes(text,'cp1252')==data,str(data,'cp1252')==text)\n", "251 True True True\n" };
        yield return new object[] { "cp1252-codec-aliases", "for encoding in ['cp1252','CP1252','windows-1252','windows_1252','windows 1252','1252']:\n    print(encoding,'\\u20ac'.encode(encoding).hex(),bytes([128]).decode(encoding)=='\\u20ac')\n", "cp1252 80 True\nCP1252 80 True\nwindows-1252 80 True\nwindows_1252 80 True\nwindows 1252 80 True\n1252 80 True\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task CodecSurfacesMatchPython(string name, string source, string expected)
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

    public static IEnumerable<object[]> FileControls()
    {
        yield return new object[] { "from pathlib import Path\nwith open('source.bin',encoding='cp1252',newline='') as file:\n    print(repr(file.read(1)),repr(file.readline()),repr(file.readline()),repr(file.read()))\nprint(repr(Path('source.bin').read_text(encoding='cp1252')))\nwith Path('output.bin').open('w',encoding='cp1252',newline='') as file:\n    print(file.write('A'),file.write('\\n\\u20ac'))\n    file.flush()\n    print(file.write('\\xe9'))\nwith open('output.bin','a',encoding='cp1252',newline='') as file:\n    print(file.write('!'))\nprint(Path('output.bin').read_bytes().hex())\nprint(repr(Path('output.bin').read_text(encoding='cp1252',newline='')))\n", "'A' '\\r\\n' '\u20ac\\n' '\u00e9\\rfinal'\n'A\\n\u20ac\\n\u00e9\\nfinal'\n1 2\n1\n1\n410a80e921\n'A\\n\u20ac\u00e9!'\n", "410d0a800ae90d66696e616c", "410a80e921" };
    }

    [Theory]
    [MemberData(nameof(FileControls))]
    public void FileReadsWritesAndAppendPreservePythonBytes(string source, string expected, string seedHex, string outputHex)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/source.bin", Convert.FromHexString(seedHex));
        var result = new LythonEngine().Run(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
        Assert.Equal(Convert.FromHexString(outputHex), host.ReadBytes("/output.bin"));
    }

    [Theory]
    [MemberData(nameof(FileControls))]
    public async Task FileOperationsActuallySuspend(string source, string expected, string seedHex, string outputHex)
    {
        var host = new DelayedLythonHost();
        host.SeedBytes("/source.bin", Convert.FromHexString(seedHex));
        var result = await new LythonEngine().RunAsync(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
        Assert.Equal(Convert.FromHexString(outputHex), host.ReadBytes("/output.bin"));
        Assert.True(host.CompletedAsynchronously > 0);
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormattedFieldLexicalTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]
        {
            "fstring_tuple",
            "print(f\"{1,2}\",f\"{*[1,2],}\")",
            "(1, 2) (1, 2)\n"
        };
        yield return new object[]
        {
            "fstring_multiline_ungrouped",
            "print(f\"\"\"{1 +\n2}\"\"\")",
            "3\n"
        };
        yield return new object[]
        {
            "fstring_multiline_single_quote",
            "print(f\"{1 +\n2}\")",
            "3\n"
        };
        yield return new object[]
        {
            "fstring_multiline_grouped",
            "print(f\"{(\n1 +\n2\n)}\")",
            "3\n"
        };
        yield return new object[]
        {
            "fstring_comment_plain",
            "print(f\"\"\"{1 # comment\n}\"\"\")",
            "1\n"
        };
        yield return new object[]
        {
            "fstring_comment_punctuation",
            "print(f\"\"\"{1 # } ! : ' \" ( ]\n}\"\"\")",
            "1\n"
        };
        yield return new object[]
        {
            "fstring_comment_operator",
            "print(f\"\"\"{1 # comment\n+2}\"\"\")",
            "3\n"
        };
        yield return new object[]
        {
            "fstring_comment_debug",
            "x=1\nprint(f\"\"\"{x # comment\n=}\"\"\")",
            "x \n=1\n"
        };
        yield return new object[]
        {
            "fstring_backslash",
            "print(f\"{'\\n'.join(['a','b'])}\")",
            "a\nb\n"
        };
        yield return new object[]
        {
            "fstring_explicit_join",
            "print(f\"{1 + \\\n2}\")",
            "3\n"
        };
        yield return new object[]
        {
            "fstring_debug_control",
            "x=3\nprint(f\"{x=}\",f\"{x = :>3}\")",
            "x=3 x =   3\n"
        };
        yield return new object[]
        {
            "debug_comment_after_marker",
            "x=7\nprint(repr(f\"\"\"{x= # comment = ' } ! : \\\n}\"\"\"))",
            "'x= \\n7'\n"
        };
        yield return new object[]
        {
            "debug_comments_inside_grouping",
            "x=7\nprint(repr(f\"\"\"{(x # } ! : ' \\\n+1)= }\"\"\"))",
            "'(x \\n+1)= 8'\n"
        };
        yield return new object[]
        {
            "debug_hash_in_string",
            "print(f\"{'# ='=}\")",
            "'# ='='# ='\n"
        };
        yield return new object[]
        {
            "debug_whitespace_and_conversion",
            "x=7\nprint(repr(f\"\"\"{ x #comment\n = !s:>3}\"\"\"))",
            "' x \\n =   7'\n"
        };
        yield return new object[]
        {
            "format_hash_and_quote_are_literal",
            "print(f\"{12:#x}\",f\"{2:#>4}\",f\"{2:'>4}\")",
            "0xc ###2 '''2\n"
        };
        yield return new object[]
        {
            "dynamic_format_with_comment",
            "print(f\"{7:{2 # comment ' } : ! \\\n+2}d}\")",
            "   7\n"
        };
        yield return new object[]
        {
            "nested_quote_reuse_with_comments",
            "x=7\nprint(f\"{f\"{x # } ' : ! \\\n+1}\"}\")",
            "8\n"
        };
        yield return new object[]
        {
            "nested_debug_comments",
            "x=7\nprint(repr(f\"{f\"{x #comment\n=}\"=}\"))",
            "'f\"{x #comment\\n=}\"=\\'x \\\\n=7\\''\n"
        };
        yield return new object[]
        {
            "raw_comments_and_continuation",
            "print(rf\"{1 #comment }\n+2}\\n\")",
            "3\\n\n"
        };
        yield return new object[]
        {
            "hash_in_triple_string",
            "print(f\"{'''# not a comment } ! :'''}\")",
            "# not a comment } ! :\n"
        };
        yield return new object[]
        {
            "minimal_field_offset",
            "f\"{1}\"\nx=f\"{2}\"\nprint(x)",
            "2\n"
        };
        yield return new object[]
        {
            "mixed_subscripts_in_field",
            "class Box:\n def __getitem__(self,key):return key\nprint(f\"{Box()[1:\n3, ::-1]}\")",
            "(slice(1, 3, None), slice(None, None, -1))\n"
        };
        yield return new object[]
        {
            "class_private_multiline_field",
            "class C:\n __x=7\n value=f\"{__x #comment\n+1}\"\nprint(C.value)",
            "8\n"
        };
        yield return new object[]
        {
            "debug_crlf_source",
            "x=7\r\nprint(repr(f\"{ x #comment\r\n=}\"))",
            "' x \\n=7'\n"
        };
        yield return new object[]
        {
            "conversion_whitespace",
            "print(f\"{1!r }\",f\"{1!r\n}\")",
            "1 1\n"
        };
        yield return new object[]
        {
            "conversion_comment",
            "print(f\"{1!r #comment } ' : !\n}\")",
            "1\n"
        };
        yield return new object[]
        {
            "conversion_comment_before_format",
            "print(f\"{1!r #comment ' } : !\n:>3}\")",
            "  1\n"
        };
        yield return new object[]
        {
            "format_set_expression",
            "class F:\n def __format__(self,s):return s\nx=7\nprint(f\"{F():{{x}}}\")",
            "{7}\n"
        };
        yield return new object[]
        {
            "format_dict_expression",
            "class F:\n def __format__(self,s):return s\nx=7\nprint(f\"{F():{{'a':x}}}\")",
            "{'a': 7}\n"
        };
        yield return new object[]
        {
            "format_literal_brace_expression",
            "class F:\n def __format__(self,s):return s\nprint(f\"{F():{'{'}>4}\")",
            "{>4\n"
        };
        yield return new object[]
        {
            "format_escaped_quote",
            "print(f\"{2:\\\">4}\")",
            "\"\"\"2\n"
        };
        yield return new object[]
        {
            "format_quote_in_triple_string",
            "print(f\"\"\"{2:\">4}\"\"\")",
            "\"\"\"2\n"
        };
    }

    [Fact]
    public async Task MultilineFieldsAwaitOperandsAndNestedFormatFieldsInOrder()
    {
        var compiled = new LythonEngine().Compile("""
            events=[]
            def load(label):
             with open('/value.txt') as f:value=int(f.read())
             events.append(label)
             return value
            def render():
             return f"p{load('left') # } ! : '
            +load('right'):{load('width') # ' } : !
            +2}d}s"
            print(render(),events)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "2");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("p   4s ['left', 'right', 'width']\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "2");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 3);
    }

    [Theory]
    [InlineData("padding=1\ntext=f\"{1 +\nmissing}\"\n", 3, 1)]
    [InlineData("text=f\"{f\"{missing}\"}\"\n", 1, 12)]
    public async Task FieldFailuresRetainOriginalSourceLocations(string source, int line, int column)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Equal("NameError", result.Failure?.ExceptionType);
            var span = Assert.IsType<LythonSourceSpan>(result.Failure?.Span);
            Assert.Equal(source.IndexOf("missing", StringComparison.Ordinal), span.Start);
            Assert.Equal("missing".Length, span.Length);
            Assert.Equal(line, span.Line);
            Assert.Equal(column, span.Column);
        }
    }

    [Fact]
    public void ExcessiveFormattedNestingFailsBeforeEffects()
    {
        var expression = "1";
        for (var i = 0; i < 192; i++) expression = "f\"{" + expression + "}\"";
        var compiled = new LythonEngine().Compile("print('effect')\n" + expression);
        Assert.False(compiled.IsValid);
        Assert.NotEmpty(compiled.Diagnostics);
    }

    [Theory]
    [InlineData("print(f\"{1 # }\")")]
    [InlineData("print(f\"{(1 #comment\n]}\")")]
    [InlineData("print(f\"{1! r}\")")]
    [InlineData("print(f\"{1!r \u00a0}\")")]
    [InlineData("print(f\"{#comment\n}\")")]
    [InlineData("print(f\"{#comment\n=}\")")]
    [InlineData("print(f \"{1}\")")]
    [InlineData("print(fr \"{1}\")")]
    [InlineData("print(f\"{2:\">4}\")")]
    [InlineData("print(f'{2:'>4}')")]
    [InlineData("print(f\"{1:\n}\")")]
    [InlineData("print(f\"{1 #comment\n+}\")")]
    public void MalformedCommentFieldsFailBeforeEffects(string source)
        => Assert.False(new LythonEngine().Compile("print('effect')\n" + source).IsValid);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FieldGroupingAndCommentsFollowPython(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

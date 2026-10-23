using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvCanonicalStateTests
{
    // Independent CPython 3.13.2 outputs; no installed Python is needed by these tests.
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "plain-control", "import csv\nprint(list(csv.reader(['a,b','1,2'])))", "[['a', 'b'], ['1', '2']]\n" };
        yield return new object[] { "header-list-alias", "import csv\nfields=['a']\nr=csv.DictReader(['1,2'],fieldnames=fields)\nprint(r.fieldnames is fields,r.fieldnames is r.fieldnames)\nfields.append('b')\nprint(next(r))", "True True\n{'a': '1', 'b': '2'}\n" };
        yield return new object[] { "inferred-header-identity", "import csv\nr=csv.DictReader(['a,b','1,2'])\nprint(r.fieldnames is r.fieldnames)\nr.fieldnames[0]='x'\nprint(next(r))", "True\n{'x': '1', 'b': '2'}\n" };
        yield return new object[] { "lazy-header", "import csv\ndef source():\n print('pulled')\n yield 'a'\n yield '1'\nr=csv.DictReader(source())\nprint('made')\nprint(next(r))", "made\npulled\n{'a': '1'}\n" };
        yield return new object[] { "lazy-header-error", "import csv\ndef source():\n raise ValueError('header')\n yield 'a'\ntry:r=csv.DictReader(source())\nexcept ValueError:print('constructor')\nelse:\n print('made')\n try:next(r)\n except ValueError:print('pull')", "made\npull\n" };
        yield return new object[] { "nonstring-fieldnames", "import csv\nr=csv.DictReader(['1,2'],fieldnames=[10,20])\nprint(next(r))", "{10: '1', 20: '2'}\n" };
        yield return new object[] { "set-fieldnames", "import csv\nr=csv.DictReader(['1,2'],fieldnames=['a'])\nr.fieldnames=['x','y']\nprint(next(r))", "{'x': '1', 'y': '2'}\n" };
        yield return new object[] { "dictwriter-fieldnames-alias", "import csv,io\nfields=['a']\ns=io.StringIO()\nw=csv.DictWriter(s,fields,lineterminator='\\n')\nprint(w.fieldnames is fields)\nfields.append('b')\nw.writerow({'a':'1','b':'2'})\nprint(repr(s.getvalue()))", "True\n'1,2\\n'\n" };
        yield return new object[] { "none-quotechar", "import csv\nprint(list(csv.reader(['a,\"b\"'],quotechar=None)))", "[['a', '\"b\"']]\n" };
        yield return new object[] { "none-delimiter", "import csv\ntry:csv.reader(['a,b'],delimiter=None)\nexcept TypeError:print('TypeError')\nelse:print('accepted')", "TypeError\n" };
        yield return new object[] { "none-lineterminator", "import csv,io\ntry:csv.writer(io.StringIO(),lineterminator=None)\nexcept TypeError:print('TypeError')\nelse:print('accepted')", "TypeError\n" };
        yield return new object[] { "truth-option", "import csv\nprint(list(csv.reader(['a, b'],skipinitialspace=1)))", "[['a', 'b']]\n" };
        yield return new object[] { "none-doublequote", "import csv\nprint(list(csv.reader(['\"a\"\"b\"'],doublequote=None)))", "[['a\"b\"']]\n" };
        yield return new object[] { "default-terminator", "import csv,io\ns=io.StringIO()\ncsv.writer(s).writerow(['a'])\nprint(repr(s.getvalue()))", "'a\\r\\n'\n" };
        yield return new object[] { "escape-roundtrip", "import csv,io\ns=io.StringIO()\ncsv.writer(s,escapechar='\\\\',lineterminator='\\n').writerow(['a\\\\b'])\nprint(repr(s.getvalue()))\nprint(list(csv.reader([s.getvalue()],escapechar='\\\\')))", "'a\\\\\\\\b\\n'\n[['a\\\\b']]\n" };
        yield return new object[] { "escape-quoted", "import csv,io\ns=io.StringIO()\ncsv.writer(s,escapechar='\\\\',quoting=csv.QUOTE_ALL,lineterminator='\\n').writerow(['a\\\\b'])\nprint(repr(s.getvalue()))", "'\"a\\\\\\\\b\"\\n'\n" };
        yield return new object[] { "custom-terminator", "import csv,io\ns=io.StringIO()\ncsv.writer(s,lineterminator=';').writerow(['a;b'])\nprint(repr(s.getvalue()))", "'\"a;b\";'\n" };
        yield return new object[] { "bool-nonnumeric", "import csv,io\ns=io.StringIO()\ncsv.writer(s,quoting=csv.QUOTE_NONNUMERIC,lineterminator='\\n').writerow([True,False])\nprint(repr(s.getvalue()))", "'True,False\\n'\n" };
        yield return new object[] { "str-cell", "import csv,io\nclass Cell:\n def __str__(self):return 'cell'\ns=io.StringIO()\ncsv.writer(s,lineterminator='\\n').writerow([Cell()])\nprint(repr(s.getvalue()))", "'cell\\n'\n" };
        yield return new object[] { "bytes-cell", "import csv,io\ns=io.StringIO()\ncsv.writer(s,lineterminator='\\n').writerow([b'abc'])\nprint(repr(s.getvalue()))", "\"b'abc'\\n\"\n" };
        yield return new object[] { "none-cell-control", "import csv,io\ns=io.StringIO()\ncsv.writer(s,lineterminator='\\n').writerow([None,2,2.5])\nprint(repr(s.getvalue()))", "',2,2.5\\n'\n" };
        yield return new object[] { "mapping-row", "import csv,io\nclass Row:\n def keys(self):return {'a':None}.keys()\n def get(self,key,default=None):\n  print('get',key)\n  return 'value'\ns=io.StringIO()\ncsv.DictWriter(s,['a'],lineterminator='\\n').writerow(Row())\nprint(repr(s.getvalue()))", "get a\n'value\\n'\n" };
        yield return new object[] { "extra-field-error", "import csv,io\ntry:csv.DictWriter(io.StringIO(),['a']).writerow({'b':1})\nexcept Exception as e:print(type(e).__name__)", "ValueError\n" };
        yield return new object[] { "explicit-header-lazy-control", "import csv\ndef source():\n print('pulled')\n yield '1'\nr=csv.DictReader(source(),fieldnames=['a'])\nprint('made')\nprint(next(r))", "made\npulled\n{'a': '1'}\n" };
        yield return new object[] { "reader-iter-construction", "import csv\nclass Source:\n def __iter__(self):\n  print('iter')\n  return iter(['a'])\nr=csv.reader(Source())\nprint('made')\nprint(next(r))", "iter\nmade\n['a']\n" };
        yield return new object[] { "reader-invalid-source", "import csv\ndef make(value):return csv.reader(value)\ntry:r=make(7)\nexcept TypeError:print('constructor')\nelse:\n print('made')\n try:next(r)\n except TypeError:print('pull')", "constructor\n" };
        yield return new object[] { "local-csv-receiver", "class Parser:\n def reader(self,value):return value\ncsv=Parser()\nprint(csv.reader(7))", "7\n" };
        yield return new object[] { "writer-error-order", "import csv,io\ndef row():\n yield 'a,b'\n print('tail')\n yield 'later'\ntry:csv.writer(io.StringIO(),quoting=csv.QUOTE_NONE).writerow(row())\nexcept csv.Error:print('Error')", "Error\n" };
        yield return new object[] { "numeric-inferred-header", "import csv\nr=csv.DictReader(['1,2','3,4'],quoting=csv.QUOTE_NONNUMERIC)\nprint(next(r))", "{1.0: 3.0, 2.0: 4.0}\n" };
        yield return new object[] { "construction-order", "import csv\nclass Source:\n def __iter__(self):\n  print('iter')\n  return iter(['a'])\nclass Flag:\n def __bool__(self):\n  print('bool')\n  return True\nr=csv.reader(Source(),skipinitialspace=Flag())\nprint('made')", "iter\nbool\nmade\n" };
        yield return new object[] { "unhashable-header", "import csv\nr=csv.DictReader(['1'],fieldnames=[[]])\ntry:next(r)\nexcept TypeError:print('TypeError')", "TypeError\n" };
        yield return new object[] { "empty-and-duplicate-header", "import csv\nprint(next(csv.DictReader(['1,2'],fieldnames=[])))\nprint(next(csv.DictReader(['1,2'],fieldnames=['a','a'])))", "{None: ['1', '2']}\n{'a': '2'}\n" };
        yield return new object[] { "mutable-reader-options", "import csv\nr=csv.DictReader(['1','2,3,4'],fieldnames=['a','b'])\nr.restval='missing'\nprint(next(r))\nr.fieldnames=['x','y']\nr.restkey='extra'\nprint(next(r))", "{'a': '1', 'b': 'missing'}\n{'x': '2', 'y': '3', 'extra': ['4']}\n" };
        yield return new object[] { "numeric-writer-header", "import csv,io\ns=io.StringIO()\nw=csv.DictWriter(s,[10,20],lineterminator=';')\nw.writeheader()\nw.writerow({10:1,20:2})\nprint(s.getvalue())", "10,20;1,2;\n" };
        yield return new object[] { "mapping-mutates-header", "import csv,io\nfields=['a']\nclass Row:\n def get(self,key,default):\n  if key=='a':fields.append('b')\n  return key\ns=io.StringIO()\nw=csv.DictWriter(s,fields,extrasaction='ignore',lineterminator=';')\nw.writerow(Row())\nprint(s.getvalue())", "a,b;\n" };
        yield return new object[] { "mutable-writer-options", "import csv,io\ns=io.StringIO()\nw=csv.DictWriter(s,['a'],lineterminator=';')\nw.fieldnames=['b']\nw.restval='missing'\nw.extrasaction='ignore'\nw.writerow({'other':2})\nprint(s.getvalue())", "missing;\n" };
        yield return new object[] { "uppercase-extrasaction", "import csv,io\ns=io.StringIO()\ncsv.DictWriter(s,['a'],extrasaction='IGNORE',lineterminator=';').writerow({'a':1,'other':2})\nprint(s.getvalue())", "1;\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CanonicalStateMatchesPython(string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name+": "+string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[]{false,true})
        {
            using(var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget=budget })
                : script.Run(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(expected,result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
}

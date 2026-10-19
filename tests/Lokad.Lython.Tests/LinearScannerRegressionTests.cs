using Lokad.Lython.Frontend;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class LinearScannerRegressionTests
{
    [Fact]
    public void EnvironmentExpansionPreservesLongUnterminatedBracedVariables()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import os
value = "${" * 32768
expanded = os.path.expandvars(value)
return str(len(expanded)) + "|" + str(expanded == value)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("65536|True", result.ReturnValue);
    }

    [Fact]
    public void WildcardTranslationPreservesLongUnterminatedCharacterClasses()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import fnmatch
import glob
import re

prefix = "[" * 16384
pattern = prefix + "*"
candidate = prefix + "tail"
fnmatch_ok = fnmatch.fnmatchcase(candidate, pattern)
glob_ok = re.fullmatch(glob.translate(pattern), candidate) is not None
return str(fnmatch_ok) + "|" + str(glob_ok)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True", result.ReturnValue);
    }
}

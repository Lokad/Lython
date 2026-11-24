# Guest matching-flag union in CPython

The [oracle](compiler-flags.py) runs under isolated CPython 3.13.2. Its
[transcript](cpython-3.13.2.txt) distinguishes the requested flag object
from the object returned by the matching union. CPython calls the second
object during compilation. The bit tests depend on the parsed pattern,
scoped flags can run another union, and a compiler-side exception stops
the reported flag union. The literal, dot and Unicode probes also show
observable differences in the resulting program at the same reported flags.

The staged Lython path already raised a capability error for this operand.
The single-step path silently compiled under the reported flags. A test
on `b43389578` reproduced the discrepancy through the two provider shapes:
the single-step compilation succeeded and the staged path rejected it.
The correction requires integer matching flags at their shared resolution
point. Providers receive no compile call, reported flag callbacks are
skipped, any owned parse is disposed, and the test verifies joined delayed
file cleanup and allowance reuse. Supporting a dynamic compiler operand
would require provider cooperation beyond a fixed integer mask. This
probe records semantic behavior and supplies no performance result.

Windows and Ubuntu each pass 97 white-box and 710 public focused Release
checks on .NET 11. An initial run used CRLF source on both hosts and exposed
a line-ending dependency in the staged parser's multiline golden. The
fixture now compares the exact LF output on either checkout.

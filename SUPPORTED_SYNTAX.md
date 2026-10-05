# Supported syntax

Lython targets the semantics of Python 3.13 for its supported subset. The
handwritten frontend implements the contract in [SPEC.md](SPEC.md); it does
not inherit an external grammar. This inventory maps that contract to the
[Python 3.13 grammar](https://docs.python.org/3.13/reference/grammar.html).
The corpus is a finite sample, not a measure of the percentage of Python grammar
implemented or a claim of complete PEP 701 support.

| Grammar family | Supported commitments | Public regression family |
| --- | --- | --- |
| Tokens, names, strings | Unicode identifiers with NFKC normalization, soft keywords, numeric separators, raw/bytes/formatted and triple strings, explicit/implicit joining | `lexical`, `fstrings` |
| Expressions, comparison, inversion, primary | Arithmetic/bitwise precedence, lazy Boolean operators, comparison chains, conditional and assignment expressions, member access, scalar/slice/tuple-key subscripts | `expressions` |
| Assignment, star targets, augmented assignment | Names, attributes, subscripts, slices, tuple/list/nested unpacking, one starred leaf per level, chained and augmented stores; name annotations | `assignment` |
| For/while statements | Full assignment targets, nested tuple/list and starred unpacking, trailing commas, loop `else`, scope-correct `break`/`continue` | `loops`, `invalid` |
| Comprehensions, generator expressions | List/set/dict comprehensions and generator expressions, multiple clauses and ordered lazy filters, separate iteration scopes, restricted walrus bindings | `comprehensions`, `invalid` |
| Function definitions, lambdas, arguments | Defaults, keyword-only and variadic parameters, annotations, positional/keyword/starred calls, closures, global/nonlocal declarations, decorators | `functions`, `invalid` |
| Class definitions | Bases, decorators, methods, `super`, property/static/class methods; contained dataclass subset | `classes` |
| Try, raise, assert | Typed/bare handlers, tuple handlers and `as`, `else`/`finally`, chained exceptions, assertions | `exceptions`, `invalid` |
| With statements | Single or comma-separated managers; optional single-name bindings; ordered enter and reverse exit | `with` |
| Imports, simple statements, blocks | Allowed modules and members, aliases/dotted imports, grouped from-imports, semicolon statements, one-line and indented suites | `imports`, `invalid` |
| Formatted strings | Adjacent text/formatted literals, escaped braces, conversions/specifiers, nested format fields, quoted expressions and enclosing-quote reuse | `fstrings` |
| Match, patterns | Literal/guard, sequence/star, mapping/rest, class, OR and AS patterns; unique captures and equal OR bindings | `patterns`, `invalid` |

These rows describe the grammar surface. Module inventories, object protocols,
runtime limits and host capabilities remain governed by SPEC. `RunAsync` awaits
host effects in this same guest language.

## Explicit exclusions

Compilation rejects these forms before any guest effects:

- Parenthesized lists of `with` items and tuple/list `as` targets. Parenthesizing
  a single context expression remains supported.
- Positional-only `/` parameters in both `def` and lambda; the `u` string prefix.
- Generator functions (`yield`, `yield from`), `async def`, `await`, `async for`
  and `async with`.
- Type alias statements and type parameters, `except*`, matrix multiplication
  and complex literals.

The first two bullets are temporary compatibility boundaries retained after
the probe fixes. Tests pin their rejection with an unsupported diagnostic and
a source span. Loops and comprehensions accept ordinary assignment targets; context-manager
headers retain a narrower binding grammar.

## Corpus and verification

[cases.json](tests/Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json)
contains independently specified supported cases, invalid programs, explicitly
unsupported forms and one embedding extension. Supported output
values were established with isolated CPython 3.13.2 and rechecked after the
fixes. Invalid programs and unsupported forms must fail compilation, including
when preceding statements would perform effects.

[SyntaxCorpusTests](tests/Lokad.Lython.PublicApi.Tests/SyntaxCorpusTests.cs)
compiles each case once, then exercises synchronous and asynchronous execution
twice on fresh hosts. Focused public tests additionally cover delayed host
callbacks, callback failure/cancellation, definition order, indexing, JSON
memory reservations and prefix parsing. Existing frontend containment tests
pin source-size and nesting limits.

Top-level `return` is an intentional embedding extension: it ends execution and
projects the value to the host. CPython rejects that program; it is deliberately
not asserted as equivalent behavior. Host-mediated I/O and the contained
standard-library surface introduce additional documented boundaries.

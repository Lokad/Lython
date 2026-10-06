# Supported syntax

Lython targets the semantics of Python 3.13 for its supported subset. The
handwritten frontend implements the contract in [SPEC.md](SPEC.md); it does
not inherit an external grammar. This inventory maps that contract to the
[Python 3.13 grammar](https://docs.python.org/3.13/reference/grammar.html).
The corpus is a finite sample, not a measure of the percentage of Python grammar
implemented or a claim of complete PEP 701 support.

| Grammar family | Supported commitments | Public regression family |
| --- | --- | --- |
| Tokens, names, strings | Unicode identifiers with NFKC normalization, soft keywords, numeric separators, raw/bytes/formatted/Unicode-prefixed and triple strings, adjacent text/bytes concatenation, explicit/implicit joining | `lexical`, `fstrings` |
| Expressions, comparison, inversion, primary | Arithmetic/bitwise/matrix precedence, complex literals, lazy Boolean operators, comparison chains, conditional and assignment expressions, member access, scalar/slice/tuple-key subscripts | `expressions` |
| Assignment, star targets, augmented assignment | Names, attributes, subscripts with mixed scalar/slice/ellipsis/starred tuple keys, slices, tuple/list/nested unpacking, one starred leaf per level, chained and augmented stores, expression-list values; name annotations | `assignment` |
| For/while statements | Full assignment targets, nested tuple/list and starred unpacking, trailing commas, loop `else`, scope-correct `break`/`continue` | `loops`, `invalid` |
| Comprehensions, generator expressions | List/set/dict comprehensions and generator expressions, multiple clauses and ordered lazy filters, separate iteration scopes, restricted walrus bindings | `comprehensions`, `invalid` |
| Generator functions | Lazy yield/yield-from, tuple/starred/trailing-comma yielded values, send/throw/close, return values, delegation and suspended try/with/finally cleanup; bare yields in formatted fields/specifiers, suspension in unpacked calls/displays, outer comprehension iterables, guards, assignment/deletion targets, assert/raise operands and nested definition inputs; scope boundaries in SPEC | `generators` |
| Function definitions, lambdas, arguments | Defaults, positional-only, keyword-only and variadic parameters, annotations, positional/keyword/starred calls, closures, global/nonlocal declarations in functions and class bodies, decorators | `functions`, `invalid` |
| Modern typing | Lazy type aliases, generic functions/classes/aliases, bounds/constraints/defaults, TypeVar/TypeVarTuple/ParamSpec, annotation scopes and variadic annotations | `typing`, `invalid` |
| Class definitions | Direct/starred bases and keyword/mapping options, decorators, methods, lexical `__class__` cells, `super`, property/static/class methods, lexical private-name mangling with preserved display names; contained dataclass subset | `classes` |
| Try, raise, assert | Typed/bare handlers, general computed class/tuple headers, grouped/empty/trailing-comma tuples and `as`, `else`/`finally`, chained exceptions, assertions | `exceptions`, `invalid` |
| With statements | Single, comma-separated or grouped managers; full assignment targets; ordered enter and reverse exit, including binding failures | `with` |
| Imports, simple statements, blocks | Allowed modules and members, soft-keyword names/aliases, dotted imports, grouped from-imports, repeated scope-declaration names, semicolon statements, one-line and indented suites | `imports`, `invalid` |
| Formatted strings | Adjacent text/formatted literals, escaped braces, conversions/specifiers, nested format fields, implicitly joined multiline fields and lexical comments, debug text, quoted expressions and enclosing-quote reuse | `fstrings` |
| Match, patterns | Tuple/starred subjects; literal/guard, open/bracketed sequence/star, mapping/rest, class, OR and AS patterns; unique captures, equal OR bindings and enclosing-scope guard assignments, including delayed hosts | `patterns`, `invalid` |

These rows describe the grammar surface. Module inventories, object protocols,
runtime limits and host capabilities remain governed by SPEC. `RunAsync` awaits
host effects in this same guest language.

## Explicit exclusions

Compilation rejects these forms before any guest effects:

- `async def`, `await`, `async for` and `async with`.
- `except*`.
- Named Unicode escapes (`\N{...}`) in non-raw text literals and f-string literal
  text. Numeric Unicode escapes and literal Unicode characters are supported;
  raw/bytes literals preserve their Python escape behavior.

The generator suspension boundaries listed in SPEC are also compile-time
exclusions. Guest cleanup requires exhaustion or explicit close; CLR collection
does not execute guest code.

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

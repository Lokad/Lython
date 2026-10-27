# Python compatibility

Lython targets Python 3.13 semantics for a strict but extensive subset. Supported
programs should behave like ordinary Python; unsupported behavior fails explicitly.
It is neither all of CPython nor a general native-extension environment. See the
[module surface](MODULES.md) and
[public corpus](../tests/Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json).

## Syntax and execution

The handwritten frontend implements the supported parts of the
[Python 3.13 grammar](https://docs.python.org/3.13/reference/grammar.html).
The corpus is a finite sample, not a grammar-coverage percentage or a claim
of complete PEP 701 support.

| Grammar family | Supported commitments | Public regression family |
| --- | --- | --- |
| Tokens, names, strings | Unicode identifiers with NFKC normalization, soft keywords, form-feed whitespace/indentation reset, numeric separators, raw/bytes/formatted/Unicode-prefixed and triple strings, adjacent text/bytes concatenation, explicit/implicit joining | `lexical`, `fstrings` |
| Expressions, comparison, inversion, primary | Arithmetic/bitwise/matrix precedence, complex literals, lazy Boolean operators, comparison chains, conditional and assignment expressions, member access, scalar/slice/tuple-key subscripts | `expressions` |
| Assignment, star targets, augmented assignment | Names, attributes, subscripts with mixed scalar/slice/ellipsis/starred tuple keys, slices, tuple/list/nested unpacking, one starred leaf per level, chained and augmented stores, expression-list values; module annotation metadata and parenthesized/non-name annotations | `assignment` |
| For/while statements | Full assignment targets, nested tuple/list and starred unpacking, trailing commas, loop `else`, scope-correct `break`/`continue` | `loops`, `invalid` |
| Comprehensions, generator expressions | List/set/dict comprehensions and generator expressions, multiple clauses and ordered lazy filters, separate iteration scopes, restricted walrus bindings | `comprehensions`, `invalid` |
| Generator functions | Lazy yield/yield-from, tuple/starred/trailing-comma yielded values, send/throw/close, return values, delegation and suspended try/with/finally cleanup; bare yields in formatted fields/specifiers, suspension in unpacked calls/displays, outer comprehension iterables, guards, assignment/deletion targets, assert/raise operands and nested definition inputs; [suspension boundaries](#generators) | `generators` |
| Function definitions, lambdas, arguments | Defaults, positional-only, keyword-only and variadic parameters, eager or postponed string annotation metadata, positional/keyword/starred calls, closures, global/nonlocal declarations in functions and class bodies, decorators | `functions`, `invalid` |
| Modern typing | Lazy type aliases, generic functions/classes/aliases, bounds/constraints/defaults, TypeVar/TypeVarTuple/ParamSpec, annotation scopes and variadic annotations | `typing`, `invalid` |
| Class definitions | Direct/starred bases and keyword/mapping options, decorators, methods, lexical `__class__` cells, `super`, property/static/class methods, lexical private-name mangling with preserved display names; contained dataclass subset | `classes` |
| Try, raise, assert | Typed/bare handlers, general computed class/tuple headers, grouped/empty/trailing-comma tuples and `as`, `else`/`finally`, chained exceptions, assertions | `exceptions`, `invalid` |
| With statements | Single, comma-separated or grouped managers; full assignment targets; ordered enter and reverse exit, including binding failures | `with` |
| Imports, simple statements, blocks | Allowed modules and members, soft-keyword names/aliases, dotted imports, grouped from-imports, repeated scope-declaration names, semicolon statements, one-line and indented suites | `imports`, `invalid` |
| Formatted strings | Adjacent text/formatted literals, escaped braces, conversions/specifiers, nested format fields, implicitly joined multiline fields and lexical comments, debug text, quoted expressions and enclosing-quote reuse | `fstrings` |
| Match, patterns | Tuple/starred subjects; literal/guard, open/bracketed sequence/star, mapping/rest, class, OR and AS patterns; unique captures, equal OR bindings and enclosing-scope guard assignments, including delayed hosts | `patterns`, `invalid` |

Invalid syntax/targets and explicitly excluded forms fail compilation even in
uncalled or unreachable code. Static diagnostics catch conservative known errors;
they are not a runtime type checker. Dynamic operations preserve left-to-right
effects, short-circuiting, closures and exception cleanup under both APIs.

Top-level `return` is an embedding extension: it ends the script and projects a
value to the host. Python async syntax and `except*` are excluded. `RunAsync`
awaits host effects in this same synchronous guest language.

## Values and object behavior

Integers have arbitrary precision; floats use binary64. Booleans, complex values,
numeric promotion, floor division/modulo, comparisons, hashing and ordinary
containers follow Python within the supported operations. Decimal behavior is
provided by the contained `decimal` module. Special methods use type lookup where
Python requires it; ordinary attribute access still observes guest descriptors.

List/dict/set/tuple construction, updates, slicing and unpacking preserve evaluation
order and alias identity. Iteration accepts supported native values and guest
protocols. Builtins and higher-order functions can invoke guest callbacks; the
asynchronous host API awaits supported callback suspension.

Ordinary annotations evaluate at definition/execution time as appropriate.
Module/class annotation dictionaries are live mappings; function-local annotation
expressions do not execute. `from __future__ import annotations` stores canonical
strings. Modern generic annotations use their lazy annotation scope. Typing
metadata does not enforce runtime types.

Module and class-body `locals()`/zero-argument `vars()` expose the live execution
namespace. Optimized function locals remain fresh snapshots, as in Python 3.13.
Inlined comprehensions retain isolated local snapshots, including inside module
and class bodies.
A retained class-body mapping is separate from the class namespace created from it.

`vars(obj)` follows the object's `__dict__`, including supported descriptors.
Instance and script-module dictionaries are live. Class namespaces expose a live,
read-only `mappingproxy`; native builtin-module namespaces expose cached read-only
definitions. Function and wrapper dictionaries hold live custom attributes.
Replacing a function dictionary preserves old aliases. Replacing an instance's
whole `__dict__` is explicitly unsupported; mutate its entries instead.

Builtin exception instances use identity equality and retain a stable args tuple.
Exception `__dict__` replacement preserves Python reference/alias behavior; this
supported exception slot is separate from the ordinary-instance restriction above.
Cause/context edits survive finally and reraises. `SystemExit` accepts arbitrary
positional arguments and uses the current code attribute for the public exit code.
`RunAsync` awaits exception argument rendering and args-iterator callbacks; general
nested container rendering may still require synchronous completion.

Star imports honor explicit `__all__` by ordered sequence indexing, including
private names. Otherwise they filter underscore-prefixed module names. Invalid
entries and missing attributes raise errors after any earlier bindings take effect.

`copy.copy`, `copy.deepcopy` and class replacement hooks support awaited callbacks
under `RunAsync`. `copy.replace` resolves `__replace__` on the class before using
native dataclass/namedtuple defaults; an instance-only hook or arbitrary `_replace`
method does not supply that protocol. Reduction/pickling protocols remain unsupported.

Exception matching, chaining, reraises, suppression and context-manager cleanup
follow Python. Guest-raised `MemoryError` is catchable; host cancellation and
actual refused storage admission terminate the command after cleanup.

## Text and bytes

Strings contain valid Unicode scalars stored as UTF-8. Indexes, lengths, slices
and regex offsets use characters rather than bytes. Named Unicode escapes
(`\N{...}`) in non-raw text/f-string literal text and surrogate scalar escapes
are rejected; literal Unicode and numeric escapes are supported. Raw/bytes
literals retain their Python escape behavior. Unicode identifier normalization, string methods, f-strings,
`str.format` and text `%` formatting are available within the subset.

Whitespace and line splitting follow Python's text rules, distinct from ASCII
byte whitespace. Casing includes expansion and contextual final sigma. String
classification polls cancellation while scanning; `isidentifier` tests structure,
so `'for'.isidentifier()` is `True`. Bytes expose their contained construction/indexing/method surface, including
`__bytes__` and type-based `__index__` conversion. `bytearray` is not provided.
These values do not grant ambient binary I/O.
Byte searches preserve out-of-range starts and validate converted arguments before
empty-window results. Search, membership, width/count and line-splitting hooks use
the active execution context, including actual asynchronous host operations.

Text codecs support UTF-8/UTF-8-SIG, Latin-1, ASCII, Windows-1252 and UTF-16/LE/BE
with documented aliases and strict/ignore/replace/backslashreplace handling.
Error-handler names are case-sensitive. Unknown handlers fail eagerly, including
on valid input; there is no handler registry. Codec-generated Unicode failures
carry their five arguments and encoding/object/start/end/reason fields. Native
fields remain separate from `__dict__`; offsets count bytes for decoding and
characters for encoding.
Surrogate error handlers and other encodings fail explicitly. File newline
translation is separate from byte/string conversion. Subprocess text conversion
supports its narrower UTF-8/UTF-8-SIG contract.

## Generators

Generator functions are lazy, single-pass and retain their frame. They support
`yield`, `yield from`, `send`, `throw`, `close` and return values through
`StopIteration.value`. Delegation forwards send/throw/close; escaping body
`StopIteration` becomes `RuntimeError`. Suspended try/with/finally state survives
resumption. Explicit close runs guest cleanup; exhaustion or close is required,
because CLR collection does not execute guest code.

Suspension is supported in ordinary expression/target positions, formatted fields,
guards, computed exception headers and enclosing definition inputs. Python-forbidden
yield in comprehension bodies, postponed annotations and generic annotation scopes
is rejected at compilation. Generator frame/code introspection is excluded.

Generator expressions acquire the outer iterator at construction and defer their
remaining work. Executable creation sites support send/throw/close; required
lowered/class creation routes retain an iterator-only interface. Closing a
generator expression does not close a separately retained Python outer iterator;
native resources still receive deterministic cleanup.

## Explicit boundaries

- No Python `async def`, `await`, `async for`, `async with` or `except*`.
- No named Unicode database escapes or surrogate-valued strings.
- No native extensions, guest CLR reflection or ambient machine access.
- Module names do not imply the complete CPython API; use [module boundaries](MODULES.md).
- Dataclass `slots=True`/`weakref_slot=True`, generator introspection and implicit
  guest finalization are unsupported.

Host authorization, cooperative cancellation, managed nesting/stack admission and
[shared memory ownership](RUNTIME.md#shared-memory-ownership) are observable embedding contracts. They do
not promise exact CPython resource thresholds or OS isolation.

## Corpus and verification

[cases.json](../tests/Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json)
contains independently specified supported cases, invalid programs, explicitly
unsupported forms and one embedding extension. Supported output goldens use
isolated CPython 3.13.2. Invalid programs and unsupported forms fail compilation, including
when preceding statements would perform effects.

[SyntaxCorpusTests](../tests/Lokad.Lython.PublicApi.Tests/SyntaxCorpusTests.cs)
compiles each case once, then exercises synchronous and asynchronous execution
twice on fresh hosts. Focused public tests additionally cover delayed host
callbacks, callback failure/cancellation, definition order, indexing, JSON
memory reservations and prefix parsing. Existing frontend containment tests
preserve managed nesting protection and shared compilation admission.

The contained `subprocess.Popen` facade defers launch until wait, communicate,
pipe reading or context exit. Polling alone does not start or advance a command.
It supplies buffered completion, not live-process concurrency or timeout retry.
See [module contracts](MODULES.md) for the supported boundary.

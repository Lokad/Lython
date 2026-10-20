# Python compatibility

Lython targets Python 3.13 semantics for a strict but extensive subset. Supported
programs should behave like ordinary Python; unsupported behavior fails explicitly.
It is neither all of CPython nor a general native-extension environment. See the
[syntax inventory](SYNTAX.md), [module surface](MODULES.md) and
[public corpus](../tests/Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json).

## Syntax and execution

| Area | Current surface |
| --- | --- |
| Statements | Assignment/unpacking, annotated and augmented stores, deletion, conditionals, for/while with else/break/continue, assert, raise, try/except/else/finally, with, imports and scope directives |
| Expressions | Python precedence, lazy booleans, chained comparisons, conditional/walrus expressions, slices/tuple keys, calls, comprehensions, generator expressions and f-strings |
| Functions | Positional-only/keyword-only/variadic binding, defaults, decorators, lambdas, closures, global/nonlocal and annotations |
| Classes | Inheritance, methods, descriptors, property/static/class methods, super, private-name mangling and contained dataclasses |
| Patterns | Literal, sequence/starred, mapping/rest, class, OR/AS patterns and ordered guards with enclosing-scope captures |
| Typing syntax | Generic definitions and lazy type aliases, type-parameter bounds/constraints/defaults and annotation scopes |

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

Exception matching, chaining, reraises, suppression and context-manager cleanup
follow Python. Guest-raised `MemoryError` is catchable; host cancellation and
actual refused storage admission terminate the command after cleanup.

## Text and bytes

Strings contain valid Unicode scalars stored as UTF-8. Indexes, lengths, slices
and regex offsets use characters rather than bytes. Named Unicode escapes
(`\N{...}`) and surrogate scalar escapes are rejected; literal Unicode and numeric
escapes are supported. Unicode identifier normalization, string methods, f-strings,
`str.format` and text `%` formatting are available within the subset.

Whitespace and line splitting follow Python's text rules, distinct from ASCII
byte whitespace. Casing includes expansion and contextual final sigma. String
classification polls cancellation while scanning; `isidentifier` tests structure,
so `'for'.isidentifier()` is `True`. Bytes and bytearray expose their contained
construction/indexing/method surface; they do not grant ambient binary I/O.

Text codecs support UTF-8/UTF-8-SIG, Latin-1, ASCII, Windows-1252 and UTF-16/LE/BE
with documented aliases and strict/ignore/replace/backslashreplace handling.
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
[shared memory ownership](MEMORY.md) are observable embedding contracts. They do
not promise exact CPython resource thresholds or OS isolation.

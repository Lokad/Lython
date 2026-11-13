# XML attribute key protocol correction

2026-11-13UTC, R333 above fully qualified runtimed90dd5fe/evidenceb663bd31.
The new public regression fails on original production: a synchronous lookup
returns "missing" instead of "kept", because Element.get uses a context-free
dictionary entry. Replace that duplicate route with DictMembers.Get/GetAsync.
No new protocol dispatcher, ownership system or public option is introduced.

79public/32internal focused XML cases pass per actual Release.NET11/10.
The seven new cases are then strengthened/rechecked per target: __hash__ clears
the element's map before actually suspending, requiring the pending lookup to
retain the already-bound old map. Hash/equality cancellation joins cleanup before
budget reuse. Refusal through suppressed MemoryError remains terminal; explicit
guest-raised MemoryError remains catchable. Three isolated CPython3.13.2 controls
confirm values/default aliases, errors and old-map binding, including real file I/O.

[Exact original/final source, CPython controls and checks](correctness-checks.tar.xz).
This is a correctness delivery with no allocation/CPU gain claim. Full platform,
original stress and installed-consumer qualification will batch with the next
coherent runtime checkpoint; older R331 qualification does not cover this change.
Publication/tagging remain postponed.

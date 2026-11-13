# Correct remaining factory-slot lookup

The original157a338f delivery exposed a fourth consumer of builtin `object.__new__`:
the partialmethod factory still required a descriptor. Use the existing canonical
class binder there too, accepting the raw builtin method while retaining descriptor
binding. Remove that obsolete requirement and its duplicated dispatch. No new
route, option, cache, memory policy or public API is introduced.

Focused Release11:86internal/802public cases pass, including the unchanged existing
`SharedFixedLabelBehaviorTests.FactoryNewSlots`, builtin aliases, namespaces,
metaclasses, actual suspension, cancellation, first refusal and ownership cleanup.
No assertion or timeout is relaxed. The earlier selection missed this integration
case; the complete failed delivery remains[archived](../delivery-failed/README.md).

Only PyAttributeLookup changes from157. All573corrected production inputs and exact
bits are frozen for source-matched full qualification. No new CPU/allocation timing
is inferred from this correction, and earlier7ee timings retain their source label.
The CPython refresh will use the corrected, qualified image.

[Exact source/bits and focused checks](source-checks.tar.xz).

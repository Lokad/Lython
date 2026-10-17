# Rejected declared-builtin operand binding

This 2026-10-17 local Windows experiment compared `5b546b2e` with a shared
operand-call interface and a binder accepting borrowed operand values. It is
**rejected**: none of its runtime code, new interface or architecture-specific
white-box assertions remain in the project. Earlier named-binding and async
factory improvements are preserved.

The source criterion succeeded: declared builtin calls stopped renting, filling
and clearing an intermediate `CallArgumentValue[]`. Four route witnesses failed
before and passed afterward. Both array and borrowed binding reproduced all
4,686 historical [canonical records](../2026-10-17-named-binding/binding-traces.json.gz),
including values, applicable presence and error type/message/span. Real delayed
I/O sort callbacks, cancellation and cleanup/refunds passed too. Four public
callback-lifetime regressions are retained independently of this representation.

The added interface, binder branches and shared-result wrapper did not earn
their complexity. Allocation medians were unchanged in every cell. Synchronous
sorting medians were worse in both isolated variants:

| 128-call form | Baseline ms | First isolated candidate ms | Form-only candidate ms |
| --- | ---: | ---: | ---: |
| Synchronous `sorted(values)` | 0.4106 | 0.4684 | 0.4775 |
| Synchronous `sorted(values, reverse=True)` | 0.4521 | 0.5178 | 0.5118 |
| RunAsync `sorted(values)` | 0.7219 | 0.6375 | 0.6473 |
| RunAsync `sorted(values, reverse=True)` | 0.7167 | 0.6832 | 0.6957 |

These are medians of eight samples after four warmups, with one range construction
per sample. Inputs, source, context and compilation are precreated; host I/O and
startup are excluded. Helper-call controls and every allocation/timing sample are
in [evidence](evidence.json). Several distributions are wide: baseline async
ordinary spread is 1,287%; final candidate synchronous ordinary spread is 128.5%.
The capture does not prove a general speedup or regression for complete jobs.
It supplies insufficient evidence to ship extra machinery as an improvement.
No CPython ratio, RSS or embedding-latency gain is claimed.

An initial capture ran beside other selected fixtures; its raw output is retained
as `initial-contended-*.trx` and is not the isolated timing comparison. The first
isolated variant consumed presence policy once but rebuilt argument records in
both binder passes. The final variant read forms for validation and values only
for final placement, reusing the existing positional-only call-site fact. Its
results did not justify another variant. Inputs, iteration counts, protection
settings and process limits were not tuned. No further campaign follows this item.

Raw [baseline](baseline.trx), [first](first-candidate.trx), [final](final-candidate.trx)
and [semantic](binding-corpus.trx) output are preserved, together with the
[first patch](first-candidate.patch), [final patch](final-candidate.patch),
[temporary native diagnostic](BuiltinOperandProbe.cs.txt) and
[temporary corpus diagnostic](BuiltinOperandBindingProbe.cs.txt). They are archived
evidence and are not compiled by ordinary tests. To reproduce, use `5b546b2e`,
apply a patch, copy the selected diagnostic into the white-box project, build
Release and select only that diagnostic class. Adjust the corpus capture paths;
remove temporary source afterward. Preserve the existing 28-second process cap
plus one second for cleanup. The isolated baseline used a guarded temporary
restoration and restored candidate files byte-exactly in `finally`.

Machine and SDK are recorded in evidence; the exact test-runtime patch was not
captured. This is one bounded rejected investigation, not a current-HEAD timing
or a benchmark framework. Revisit ordinary-call transport from a material
complete-workload profile rather than retaining this unused protocol.

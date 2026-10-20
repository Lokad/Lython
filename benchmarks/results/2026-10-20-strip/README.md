# Governed UTF-8 trimming

Runtime revision `a017d12a` closes demonstrated storage and cancellation gaps in
`str.strip`/`lstrip`/`rstrip` without adding a resource policy or ownership layer.

- Admit custom scalar-set metadata, bucket/entry capacity and old/new growth
  overlap through the active execution before construction. Hold scratch through
  result ownership and release it on success, invalid UTF-8, refusal or cancellation.
- Decode the character set directly from strict UTF-8, deleting the transient CLR
  string and UTF-16 enumeration. Keep hash-set membership. Empty sets reuse the source.
- Preflight fresh slices of unowned constants through the active execution using
  the existing copy helper. Delete the former unowned-result decode/encode copy.
  Already-governed source owners and unchanged aliases remain first.
- Use existing synchronous ambient scopes and cooperative checks in set preparation
  and both boundaries. No scope crosses an await. Both public hosting APIs share
  this operation; there are no background users, retries or independent allowances.

The estimate is deliberately coarse: 256 + 64 times the custom UTF-8 byte count.
Long duplicate-heavy sets can deny earlier even with few distinct scalars. This
changes admission thresholds; there is no public C# API or Python semantic change.
The estimate covers logical ownership, not exact CLR heap or embedding latency.

[Eight original failures](red.txt) cover unfunded custom sets and uncancellable
primitive loops. [Three further failures](output-red.txt), recorded after the
set correction but before slice admission, expose constant-result allocation.
Focused Release passes: 36 white-box storage/cancellation/Unicode controls,
161 public trim/string controls and 215 complete CSV/text/JSON/file and shared-root
checks. These are overlapping selections, not a unique-test count. No assertion,
timeout, scale or allowance was relaxed. [Receipts](checks.json) retain the stages.

Cancellation witnesses invoke primitive loops without a caller entry/exit check,
and separately cancel through a cooperating budget after the method admitted
scratch. They prove interruption and immediate scratch unwind after method entry.
Public controls preserve terminal refused admission despite catch/finally return,
retained aliases and two overlapping results, then root-baseline recovery and
separate-command reuse. [Four independent CPython goldens](oracles.json) cover
Unicode, character-set semantics, expanded/exposed and unbound calls.

One declared short warm synchronous diagnostic uses 256 iterations, each calling
all three methods on the same Unicode text and four-scalar custom set. All engines
use identical source with independently checked exact output `11264`.

| Engine | Complete kernel median | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `bb2631a7` | 0.288 ms | 3.19% | Before; diagnostic |
| Lython `a017d12a` | 0.293 ms | 2.32% | After; diagnostic |
| CPython 3.13.2 | 0.093 ms | 1.24% | External baseline; diagnostic |

The nominal 1.8% higher candidate median is smaller than the observed Lython
spreads. The result is inconclusive and establishes no speedup, regression,
allocation reduction or embedding-latency improvement. The after/reference ratio
is about 3.15 (Lython elapsed / CPython elapsed). No retiming or full lane.

Measured 2026-10-20 UTC on shared LOKAD-0399, Windows build 26200, x64 with 28 logical
processors, .NET 10.0.13/workstation GC and ordinary CPython GIL/GC. Construction,
execution/result cleanup and golden checks are included; compilation, startup
and transport are excluded. The timing root is explicitly unbounded. Seven
alternating rounds follow warmup. Parent and workers finish in 7.355 seconds,
without forced collection. [Declaration](declaration.json), [frozen before identity](baseline.json),
[catalog](catalog.json), [raw samples/loaded identities](micro.json) and
[process receipt](micro.receipt.json) preserve the measurement boundary.

Full Windows/Ubuntu Release, original Stress and both explicit package consumers
pass at exact runtime revision `a017d12a` in [CI 39347621376](https://github.com/Lokad/Lython/actions/runs/39347621376).
[All final required steps](delivery.json) were inspected. Four spaced status queries;
terminal green is cached forever and must never be queried again. Publication
remains deferred.

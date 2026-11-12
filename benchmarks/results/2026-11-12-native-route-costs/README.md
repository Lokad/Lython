# Complete helper-route cost — private counterfactual

2026-11-12. Qualified `2ec9d67f`/`c9829194` versus an isolated diagnostic build that
discards NativeBody after identical preparation/call facts. Normal source, pins,
assets and build outputs remain byte-identical. No public disable switch, shipped
backend, VM collection or compilation-performance claim. [Isolation](normal-state-proof.json).

Both unchanged complete helper scripts pass both APIs, full stdout/files, allowance
balances and joined cleanup. Existing complete-job driver changes only its two-case
validation and purpose text; measurement logic/host/goldens unchanged. First launcher
used the old output path and failed before measurements; original logs survive,
completed builds reused with the correct isolated output path.

| Script | Additional CLR allocation / command | Live peak allowance increase |
| --- | ---: | ---: |
| Keyword-default search, both APIs | ~451,100 B (~440 B per helper call) | 160 B |
| Captured text, both APIs | 64 B per whole command | 144 B |

This does not establish that all ordinary calls allocate a frame. Source confirms
the existing explicit caller chain and deferred synchronous prefix. Captured text
already completes without per-call frame allocation. Canonical nonzero-argument
string calls can still require promotion, while zero-argument operations and native
helper bodies have direct existing services. [Bounded assessment](assessment.json).

Desktop execution clocks are mixed: searchRun+5.73..+9.64% (3.56%control), searchAsync
−10.87..−6.87% (4.48%control), captured controls15–24%. No general CPU attribution,
removable-cost estimate or speed forecast. Allocations are physical CLR traffic;
the much smaller live-allowance deltas show independent lifetime accounting. Do
not confuse cumulative traffic with live protection or multiply these differences
into CPython ratios. Actual admitted roots return to baseline/zero after cleanup.

The next selected milestone shares canonical affix/split/replace eligibility and
invocation between native bodies and ordinary instruction-prefix calls, removing
unnecessary invocation promotion on guarded text helpers. Dynamic protocols,
keywords/expanded calls, coercions, Python errors and real suspension keep their
ordinary frame path. No blanket bypass of CanSuspend or new dispatch framework.
Measured native helper-return optimizations remain. Stop this counterfactual:
no additional unchanged collection or new ownership audit is selected.

[Identity](identity.json), [compile inputs](compile-items.log), [preparation/failure](preparation.tar.xz),
[candidate images](candidate-images.tar.xz), phase summaries/raw observations and
[archive identity](archive-identity.json). Fully qualified production is unchanged;
publication stays postponed.

# Native JSON encoder construction — 2026-11-08

Retain one shared initializer for module-bound options, explicit JSONEncoder
construction and guest attribute assignment. Delete the temporary constructor
defaults dictionary, repeated name lookups and six-name assignment array.
Exact native module calls consume already-bound values without eight-key keyword
forwarding List/array. Native eager encoding passes its value directly with the
same managed interpreter-frame/exception policy. Mutable native attributes and
guest constructors/setters/getters/encode/iterencode overrides remain. General
forwarding still serves guest classes and unknown extras; no obsolete fallback
was added. No new settings abstraction/cache/pool/owner/public option.

Baseline committed15c06863/core1674a06c, independently fully qualified at
[cached CI42484943603](../2026-11-08-unified-json-encoding/delivery-ci.json).
Candidate15c plus archived two-file patch/coreaae80cc7; exact source hashes in
identity.json. Informational binary revision predates the candidate commit.
New-source full platform/Stress/installed-core qualification remains separate/pending.
The main CPython guide accurately names15c; these source-pair results do not update
that external baseline or silently repay earlier R102/R95/R96 regression debt.

| Complete workload | Allocation sync / async | Controlled elapsed, all four median pairings |
| --- | ---: | ---: |
| Eager JSON normalization, sync | −27.22% / — | −9.05 to −9.93% |
| Eager JSON normalization, async API | — / −27.68% | −9.06 to −11.58% |
| Streamed JSON normalization, sync | −24.03% / — | −6.25 to −7.00% |

Coarse process CPU agrees:−10/−11.11/−5.88%, respectively. Baseline spreads
0.73/1.50/0.007%, candidate0.24/1.31/0.80%. Short controlled observations, not
confidence intervals/universal gains. Streamed async allocation−23.85%; its
controlled CPU/elapsed remains unmeasured.

Config allocation−4.34%sync/−4.30%async; controlled sync elapsed−0.83to+1.33%,
coarseCPU+4.38to+5.70%: possible CPU debt/inconclusive, explicitly accepted.
Unchanged CSV−3.01to+0.69%/candidate2.43%spread: no causal gain.
Decode-heavy JSONsummary−0.08to+0.97%/CPU0to+2.5%, possible small debt/uncertainty.
Original desktop eager+8.25%adverse and baseline drift up to17.48% are preserved.
Allocation savings do not establish CPU or embedding-tail improvements.

Both affected scripts read4096records/110592B, edit fields/construct nested values
and write the complete UTF-8 JSON-lines result. Eager/streamed outputs match.
Identical frozen catalog and independent CPython full stdout/file goldens from R102
are reused byte-for-byte; no new workload framework or validation campaign.

765public/171internal focused JSON Release peractual11/10 pass, including option/
key/default order, numeric/Unicode/chunks/cycles/mutation/managed nesting, real
suspension/reentry/current cancellation, terminal first refusal, retained aliases,
shared-budget reuse and awaited cleanup. Mutable attributes are read at their
original stages; no snapshot hides callback mutations. Native encoding keeps the
existing managed call guard and translated errors. Removed transient storage no
longer needs admission; surviving traversal/output storage remains funded/preflighted.

Quiet Ubuntu24.04.4/AzureF4as_v7,4EPYC9V45vCPUs/16GiB/noswap, actual.NET11RC3;
same worker7cf37ca5/optional0.4adapter, normalGC/tiering.5swarmup/process/job,
two copies/core,7rotated150msrounds; six jobs172.758s. Command-v2 includes full
execution, mediated RAM I/O, stdout/file validation and prior-result cleanup;
excludes compilation/startup/staging/final cached teardown. Completed I/O timing
does not qualify genuinely pending-I/O latency; real suspension is tested separately.

All source/core/aux/Ready/golden/quiet/Closed/exit/join/fixture/archive/local gates
pass. B463 terminal/cached; no users/fixtures, owned RAM unmounted before archive.
Exact archived17baseline reused, uploaded onlyaae.240MiBOSfloor+7MiBevidence+
actual payload preflight, no new space maintenance/evidence/core/path deletion.
Full new-source delivery is independent; broader performance work remains.

[Controlled results](controlled-comparison.json), [allocation/desktop evidence](allocation-and-local.json),
[identity](identity.json), [status](status.json), [sources/cores/logs/raw](observations.tar.xz).

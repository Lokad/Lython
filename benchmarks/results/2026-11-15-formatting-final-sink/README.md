# Final-output method formatting

R416, 2026-11-15, retained **8afb146e** versus qualified **2865f619 / 55ba4c8a**.
Full delivery qualification is pending. No public API signature/dependency changes;
publication remains postponed.

One async formatter accepts an append-to-final-output callback. Native numeric and
padded string fields reuse the f-string appender without temporary field-result
strings/backing or a second copy. Removes the old string-return callback family,
sync/map facades and unused suffix bridge. Actual hook return strings stay owned;
date formatting and CLR name/spec metadata remain. One operation-owned metadata
reservation replaces per-field leases and covers supported nesting/replacement;
expanded specs grow its capacity. Argument copies preflight before construction.

The conservative metadata estimate (512B + 8× template UTF8 bytes, allocated only
when a nonempty field needs metadata) remains until the operation ends. Earlier
denial is intentional; no independent cap/default allowance, retry or per-object
tracking appears. Users and cleanup finish before refunds. `str.format` now awaits
supported hosted lookup/format callbacks in RunAsync, like format_map; Sync still
requires synchronously available host operations.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference / candidate spread |
| --- | --- | --- | --- |
| Method-format report / warm | −9.41..−5.79% | −7.37..−5.56% | 1.50 / 2.47% |
| Method-format report / compile-to-result | −8.98..−7.83% | −7.81..−5.52% | 0.35 / 0.90% |
| F-string report control / warm | −1.88..+1.70% | −1.74..+2.40% | 1.38 / 2.24% |
| CSV invoice control / warm | −0.21..+2.86% | −1.96..+1.37% | 0.87 / 2.19% |

Whole warm method report allocation **−17.31%**: 8,520,542.875B→7,045,970.875B;
f-string control effectively flat (−41B). Same private allocation driver/image on
both sources, natural GC, full stdout/file goldens and shared-budget baseline checks.
It includes reset/file hashing beyond quiet execution clocks and is not CPU evidence.
Earlier stdout-only screen (8,518,181.375B method / 2,562,419.5B f-string) merely
read output files; it stays separate and does not establish the qualified reduction.

Retain for complete intermediate-output/lease deletion, useful async support and
whole-script CPU/elapsed/allocation support. Accept moving controls and possible
small CSV/f-string regression as explicit remaining uncertainty, not demonstrated
neutrality. No unchanged retiming to remove the adverse pairings; different designs
remain eligible. The larger performance objective stays open.

Focused Release **10 internal / 484 public each actual .NET11/10** pass. Clean
nonincremental Release solution build and full local ordinary11 **4,781 internal /
17,882 public** pass. New cases hold actual hosted formatting through success,
cancellation and refused admission, preserve terminal refusal through guest handlers,
fund pending/retained output and verify joined file cleanup before allowance reuse.
Descriptor/call cancellation and semantic Unicode/nesting/lookup/error-order controls
remain. Two original tests explicitly expected unsupported async str.format; the
initial failed assertions and [original source](original-format-protocol-tests.cs.txt)
are preserved, and the expanded capability now requires success and the same writes.

The format-method variant of the existing JSON→Markdown report preserves all source
inputs/output/file goldens; independent CPython verifies both scripts. 1,024 records,
same report layout/values. **B809 succeeds141.884s**, quiet F4as_v7 Ubuntu24.04.4,
four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 normal tiering/workstation GC. Two
processes/source,5s full-request warmup, five rotated ~0.5s batches. Warm includes
Run, mediated RAM I/O, full output checks and prior-result cleanup; excludes compile,
startup, fixture reset/protocol and final cache teardown. Fresh includes compilation
and compiled-owner cleanup. Wider CPU includes reset/protocol. No independent
compile-only cost, pending-I/O latency, RSS or embedding-tail claim; CPython timing
is not inferred from semantic validation.

All identity/golden/budget/quiet/Closed/exit/join gates pass; persistent evidence is
archived and verified under the lease. No VM workers/RAM fixtures remain. Terminal
cached forever. Exact archived historical9e runtime aliases and older trace/response
replicas were verified by committed Git/local/remote hashes, UID/path/link/all-alias
proof before retirement. Current55ba/0ed/8afb images and canonical evidence remain;
254/252MiB disk floors stay. Original CP launcher failures (missing -S, unowned temp
prefix, assertion before supervisor cleanup), weak allocation screen and API/test
migration conditions remain in the procedure; no runtime defect is inferred from them.

[Medians](summary.json),[allocations](allocation-summary.json),[checks](checks.json),
[identity/full source hashes](identity.json),[independent CPython goldens](python-goldens.json),
[raw](observations.tgz),[source/binaries/procedures](source-checks-and-procedure.tar.xz).
[R415 source gate](r415-source-decision.json) preserves the native-span/durable-frame
tradeoff without another arena/controller prototype; it is not a permanent ban.

[Full platform/stress/package qualification passes](delivery/README.md).

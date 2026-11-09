# Optional string index metadata — R173

2026-11-09. Baseline R172/102bed2b-equivalent AA2BAB72 precommit freeze, production
source230a0572 equivalent. Candidate: fresh nonincremental uncommitted source in
identity.json. Only PyString production changes; common worker/adapter0.4 and all
managed auxiliaries remain exact per source pair. Not a CPython timing comparison.

Remove every string's separate rune-table reference. The original governor-reference
role holds either that governor or optional indexing metadata retaining the same
governor and rune table. Original UTF-8 array/inline access and scalar-length cache
remain direct. Actual shell64→56B on measured Windows actual.NET11RC3/.NET10.0.8;
no unsafe/layout dependency. No source sharing, new scope/pool/budget/token cache or
public API. Existing128B metadata admission prepays the32B indexed carrier and
stable inline-memory view. Large payload/table charges stay separate and unchanged.
This lowers actual CLR allocation, not the intentionally conservative host charges.

Reentrant admission may construct the cache first. Reuse its completed table and
refund only unpublished working admission; keep stable borrowed memory and original
owner. New deterministic old-core proof: one reentry case charges232B instead of
184B, other3stable-view cases pass. Final unchanged test source passes42internal/
4042public focused Release checks eachactual11/10, including Unicode/astral values,
CSV/JSON/regex, cancellation, first refusal, retained aliases and budget cleanup.
Initial proof binary/first prototype and final rebuilt checks are distinct identities.

## Allocation and controlled elapsed evidence

Desktop complete warm source pair, both APIs, two identical original controls:
CSV allocation about−3.58%, text−4.94%, report−2.01%, config−0.3–0.7%. The independent
1024-line Unicode-index report allocates about+1.26% from the optional carrier.
Compilation-only construction/disposal saves just0.03–0.10%; no compile CPU claim.
Source/fixture/full stdout+file goldens, output ownership and cleanup match. Desktop
timings share process JIT/GC and vary; all raw favorable/adverse data is preserved.

One quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 confirmation, ordinary GC/tiering, completes
5jobs140.642s. Four independent processes (two per source),5s warmup/role, seven
rotated150ms rounds. Warm includes mediated RAM I/O, full output validation and
prior-result cleanup; startup/reset/protocol/final caches excluded. Coarse process
CPU has a wider boundary. Ratios span all four median pairings, not confidence intervals.

| Complete script/API | Elapsed change vs both originals | Source-control spread, original / candidate |
| --- | ---: | ---: |
| CSV validation, sync | −0.44..+1.79% |0.23% /1.97%|
| Text report, sync | −1.94..+0.22% |2.02% /0.18%|
| Text report, async/completed I/O | −2.15..+3.76% |3.34% /2.51%|
| JSON report, sync | −0.31..+0.87% |0.64% /0.54%|
| Unicode-index report, sync | −2.00..+4.76% |3.09% /3.69%|

**Decision: retain the coherent field reduction and cache correction; no demonstrated
CPU gain or equivalence.** Accept the measured indexed-allocation cost. Elapsed
overlaps controls; Unicode coarse CPU−1.59..+10.11% and wider controls remain uncertain.
This is not a fixed-percentage veto or the end of the whole-script performance
objective. No pending-I/O, embedding latency, RSS or physical-GC improvement claim.

## Preserved refinements and failed conditions

The first carrier held payload+index and added type tests to payload reads. It passed
the same checks and saved allocation, but desktop async text+17–19%, sync report
+11–15%, Unicode async+9–12% were adverse. Move the carrier beside its original
governor instead; remove that payload getter/coordination path. Retain both sources
and every result; do not infer a causal JIT explanation from the changed timings.

Independent CPython3.13.2 local golden caught a literal-newline fixture mistake and
physical Windows stdout CRLF versus logical LF. Correct source uses chr(10), report
file bytes remain exact, and raw stdout/normalization and original fixtures survive.
No timed data existed during these fixture corrections.

Original quiet setup refused the disk floor before mutation. A later exact candidate
copy B504 was absent; preserve that failure and do not reprobe it. Fresh idle lease,
single aliases, SHA/metadata/parents and receipts prove the minimum selected B493/
B492/B487/B489 compressed duplicates. Their tracked canonical archives, raw data,
receipts and frozen workers survive;240MiB OS/12MiB evidence floors remain unchanged.

Startup rejected unused desktop Windows apphost/PDB identities; no guest ran. All
managed DLL/deps/runtimeconfigs matched. Preserve original identity, then scope the
check to files actually used by dotnet worker.dll (no profiling). First finite
service then lacked the protocol import link, before any guest requests. Its empty
RAM fixture was unmounted and original files/journal archived. The corrected finite
service uses the same cores/workloads/settings and verified protocol02ca606a.
One early health check showed3jobs complete. All5jobs then pass identity/output/
ownership/quiet/Closed/join/RAM/archive-under-lease/localSHA gates; no users remain.
The initial failure is part of the successful immutable archive, not discarded.

Combined delivery [42684724223](ci-terminal.json), source6d852edc (production
cac4acf8), is GREEN: Windows and Ubuntu, actual.NET11/.NET10, original Stress and
installed-package consumers, all eight gates. This qualifies R172 and R173; it
does not change their timing conclusions. The earlier R172 run42672245421 remains
partial/failed, with Ubuntu green and Windows absent without a failed-job log.
Publication remains postponed; regex development stays independent.

[Final identity/source hashes](identity.json), [quiet summary](summary.json),
[raw observations/frozen cores/failed first start](observations.tgz),
[terminal state](terminal-status.json), [archive receipt](archive-receipt.json),
[exact space proof](space-recovery.json), [desktop result](desktop.json),
[prototype identity](prototype-identity.json), [original adversity](prototype-desktop.json),
[compile allocation](compile-allocation.json), [focused checks](qualification.json),
[old regression](regression-proof.json), [independent Unicode golden](unicode-golden.json),
[all source/model/checks/desktop/preparation](source-checks-desktop.tar.xz).

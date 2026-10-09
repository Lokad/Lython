# String ownership boundaries, 2026-10-10

Short decomposition diagnostics identify split ownership adoption as a useful
target. Split construction medians are 54–55 us; fresh adoption is 259–261 us.
Several series have wide variation, and these different boundaries cannot be
subtracted to assign a share of public execution time or native-handle cost.

A subsequent isolated direct-string guard experiment is **rejected**: target
pipeline medians increase 8.98% and 1.29%. Production sources and tests remain
unchanged at the experiment's report delivery. Both original results are
retained; no favorable recollection or
full comparison lane ran. The preceding possible instruction-reference pipeline
regression and intermittent CSV lifetime issue remain unresolved.

## Independent correctness follow-up

The source audit for the pending ownership design found an existing stepped
list-deletion accounting bug. Compaction preserved the right contents, but then
released scalar coupon identities from the overwritten tail. Those identities
could include survivors, leaving stale charges and incorrect alias refcounts.
Three regressions fail the original implementation: both step directions strand
64 bytes after survivor removal, and surviving repeated aliases lose their
correct refcounts.

Correction `99827247e5c80abb0eca75e27783491ea7bf4622` releases deleted identities
before overwriting their slots, clears tail storage without a second refund and
refreshes the tracked list charge once. Four new cases cover both directions,
surviving aliases and larger array storage. Focused Debug checks pass 85; after
adding the larger-storage case, focused Release checks pass 86. A matching Debug
probe build and the full Debug suite pass all 9,071 checks (1,323 subsystem and
7,748 public). Final delivery also requires both-platform Release/package CI.

This is an independent correctness fix, not an accepted performance candidate
or an explanation for the CSV lifetime issue. It changes none of the measured
split/join, loop or empty paths. All timing receipts and frozen producer hashes
below remain the originals; no additional performance collection was run.

## Frozen workload and producer

The clean baseline Release producer is
`6fbbbbc2af1ebdce7739bc0e2bd99fddf73451e9`. Its production tree is identical to
the `b6a6f2ad` report delivery: `b9316ce881347f2afaa4cb1174a9660304137085`.
SDK 10.0.401/runtime 10.0.12 run on the dedicated four-core AMD EPYC 9V45
Ubuntu 24.04.4 VM. Workers retain ordinary workstation/Interactive GC,
tiering and PGO, with no runtime overrides or forced collections.

The unchanged canonical `strings.pipeline-ascii.medium` fixture contains 1,024
copies of `abZ!::tail/`. Replacing `::` with `/` and splitting on `/` gives
2,048 distinct governed four-byte strings plus the shared empty string. The
complete expected content is independently checked against the canonical golden.
The unchanged 14-case quick catalog is used throughout.

The standalone helper project is included for reproduction. Build it in Release
with the pinned SDK, then run it against a frozen Release worker and canonical
quick catalog with a new output path. `preflight` validates all four shapes;
`collect` performs one declared repetition. Run collection in a dedicated
30-second owned service, with inputs hashed beforehand and afterward.

```text
<dotnet> <BoundaryDiagnostic.dll> <frozen-benchmark.dll> <quick-catalog.json> <new-receipt.json> <preflight|collect>
```

## Private boundary diagnostics

The [diagnostic source](BoundaryDiagnostic/Program.cs) builds typed delegates to
the frozen library's existing `PyStringOps.Split` and `OwnSplitListResult`.
Delegate preparation, setup, verification and receipt serialization are outside
the selected timer/allocation counter. It is a private boundary diagnostic,
not a public Python workload. Fresh governors use the ordinary 1 GiB default;
all lists, items and pools stay strongly retained during each selected boundary.

Four shapes were declared prospectively, with two process repetitions:

- **Construct:** governed split creation, including string payloads/wrappers and
  list construction, before ownership adoption.
- **Adopt:** ordinary item/list adoption on fresh lists constructed before timing.
- **Tracked control:** the same adoption path on already-tracked lists/items;
  this measures duplicate checks, not fresh registrations.
- **Construct and adopt:** both existing calls inside the selected boundary.

Each shape warms for one second of whole-batch wall time, followed by two seconds
of settling. Seven measured batches per shape rotate order, each with exactly
16 fresh invocations. Verification checks every item, owner, list identity,
entry count and charge, with zero outstanding reservations or denials. The exact
per-invocation construction charges are 270,336 bytes for strings and 37,440 for
list backing; adoption adds 2,049 registry entries and tier backing, charged
295,040 bytes. Both repetitions use the same declared source and inputs.

Median times are us per invocation; IQR/median is percent. Allocated bytes are
current-helper-thread managed allocations in the selected boundary. Native
handles and other runtime threads are excluded. Setup and verification still
allocate outside that boundary and can affect subsequent ordinary GC.

| Repetition / boundary | Median, us | IQR/median, % | Median managed allocated bytes | Gen0 delta across seven batches |
| --- | ---: | ---: | ---: | --- |
| 1 / Construct | 55.29 | 67.34 | 283,168.0 | 1, 0, 1, 0, 1, 0, 0 |
| 1 / Adopt | 259.14 | 30.94 | 180,578.5 | 0, 0, 0, 0, 0, 0, 0 |
| 1 / Tracked control | 72.05 | 72.28 | 40.0 | 0, 0, 0, 0, 0, 0, 0 |
| 1 / Construct and adopt | 315.02 | 29.77 | 463,746.5 | 0, 1, 0, 1, 0, 1, 1 |
| 2 / Construct | 53.98 | 2.86 | 283,168.0 | 0, 0, 0, 0, 0, 0, 0 |
| 2 / Adopt | 261.35 | 52.52 | 180,578.5 | 0, 0, 0, 1, 0, 1, 0 |
| 2 / Tracked control | 69.02 | 3.99 | 40.0 | 0, 0, 0, 0, 0, 0, 0 |
| 2 / Construct and adopt | 312.99 | 2.83 | 463,746.5 | 1, 0, 0, 0, 0, 0, 0 |

Whole repetitions take 6.49/6.54 seconds. Wide IQRs, including 30–73% in several
series, remain visible. Fresh adoption's repeated medians justify investigating
that boundary; they do not certify its cost or prove which operation is slow.
The tracked control allocates 40 managed bytes per invocation. The fresh path
allocates entries, weak references and backing arrays, but this experiment
does not resolve native allocation or GC cost. No additive fraction or Python
memory/performance multiplier is claimed.

## Direct string guard experiment

Candidate `49fa65c1d0c18a57ca0d3b77a62683d5fc77d39a` changes one production file.
`TrackString` checks its existing direct entry before computing the charge and
entering registration. The generic guard and direct string guard share an
extracted transaction body. Funding, publication, rollback, weak ownership,
cache coupon updates and checkpoints remain the same. This guard and method
extraction are one isolated experiment; no effect is attributed to either alone.

Two repetitions of empty invocation, the basic integer loop and the ASCII
pipeline were declared before collection. The unchanged public `micro` command
uses identical precompiled source and independent golden output with fresh guest
state in baseline, candidate and isolated CPython. Each worker warms one second,
then waits two seconds for settling and runs seven rotating batches calibrated
to 25 ms. Compilation, transport and output hashing stay outside worker timers.
The supervisor alone disables tiering and clears that override in workers.
CPython is the pinned PGO/LTO 3.13.16 build with `-I -S`, ordinary GC/GIL and
no experimental JIT. These are short diagnostic observations, with no noise
qualification, confidence interval or certified multiplier.

Positive change means candidate time is higher. IQR/median columns are percent.

| Repetition / case | Baseline, us | Candidate, us | CPython, us | IQR/median baseline / candidate, % | Time change, % | Whole collection, seconds |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| 1 / Empty | 23.14 | 23.35 | 1.51 | 2.86 / 4.27 | +0.91 | 6.42 |
| 1 / Integer loop | 1640.50 | 1641.27 | 574.68 | 1.14 / 0.97 | +0.05 | 6.96 |
| 1 / ASCII pipeline | 313.37 | 341.50 | 41.48 | 2.58 / 5.68 | +8.98 | 6.76 |
| 2 / Empty | 23.76 | 23.54 | 1.51 | 3.56 / 2.78 | -0.92 | 6.44 |
| 2 / Integer loop | 1648.84 | 1631.50 | 616.85 | 0.74 / 0.50 | -1.05 | 6.95 |
| 2 / ASCII pipeline | 309.94 | 313.94 | 41.65 | 10.50 / 6.86 | +1.29 | 6.87 |

The target pipeline is slower in both repetitions. Controls give no consistent
improvement; the second pipeline series has especially wide variation. Reject
the candidate and keep it isolated. Its exact rejected source is retained in
`perf/string-tracking-guard`; no production change accompanies this report.

## Verification and next design

The candidate passes 97 focused white-box checks in both Windows Debug/Release
and 1,019 public Release checks after an explicit matching probe build. Its
frozen Linux Release build passes 97 white-box and 1,019 public checks before
collection. Existing tests cover denial/retry, orphan refunds, distinct equal
strings, aliases, cache updates and reclamation. The baseline's previously
recorded focused Release checks cover the same production/test trees. Full
local suites are not repeated for the rejected candidate or report delivery;
the unchanged production/test trees retain their preceding 9,067-check full
Debug pass, with final delivery Release/package CI on both platforms.

The helper's initial preflight caught two reflection binding mistakes (reference
span type and overloaded string renderer). Both failed preparations and their
logs are retained. Corrected preflight checks all four shapes before declaration;
no failed timing run is replaced or excluded.

Independent audits verify 596 boundary batches / 9,416 invocations and
19,293,384 exact item checks, recomputing eight boundary medians/IQRs. They also
validate all six public micro receipts, 262 responses and all 18 medians/IQRs,
declared order/counts, producer/configuration/input identities and correctness
TRX outcomes. The ten preceding rejected cache micro receipts remain unchanged.
All eight collections have 30-second owned process-group cleanup caps
(`RuntimeMaxSec=30`, `TimeoutStopSec=0`, `KillMode=control-group`) and succeed.
Final proof confirms all 13 preparation/collection services inactive/dead with
zero main PIDs, recorded worker PIDs absent, both frozen source trees clean
and the VM lease free. Preparation failures remain preserved before their
terminal unit state is reset.

Next examine a structural ownership design that can avoid eager per-item
registrations for fresh split containers while preserving funding and every
escape/mutation/denial path. [The proposal](OWNERSHIP-DESIGN.md) is pending design
and prototype work; it is not an accepted optimization. The earlier unresolved
native CPU leaves still cannot be assigned to weak handles or GC, and no link
to the CSV lifetime failure is established. Keep the accepted loop improvement
and original possible pipeline regression visible. Full comparison lanes remain
reserved for infrequent declared milestones.

Raw receipts, declarations, helpers, source bundle, preparation failures,
correctness results and independent audits are local under
`.git/agent-notes/string-ownership-boundary-20261010/`.

| Evidence | SHA-256 |
| --- | --- |
| Canonical quick catalog | `e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1` |
| Boundary helper source | `f7cb6c1447953e5561efe4bbf3eea931326eebee6c7e2266e8031b100ea161de` |
| Boundary helper assembly | `783e4ed2d77d692d1c86ce75eb2da30e50b5efc4116ad2387ff1e750d2b27a4a` |
| Boundary receipt 1 | `027014c226b24e24b1d0accaaf29d3391e992c7677a3fb676f88ea293f63d9e1` |
| Boundary receipt 2 | `d8cae1d7d61d80e548d2c260fe80a9dd9d165d6dd233304df42c69f2523f1e7d` |
| baseline library | `2e0ff05c867f152d3f93e862972ffa135df3e8940a815b9d21c735a6745e6139` |
| candidate library | `e9cb40d31f6e32d0585af679002ddb501e740fb2705ab60394cebf10645c79e1` |

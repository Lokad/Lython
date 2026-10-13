# Proved impossible string admissions: short before/after diagnostic

For the fixed 128-bound-error job, early rejection from an existing funded payload
reduces mean complete-job time from **320.486ms** to
**1.856ms**, CPU time from **328.009ms/job**
to **2.169ms/job**, and **Gen2 collections/job from 128 to 0**.
This is one short controlled comparison, not a CPython ratio or broad performance
qualification. The four-case diagnostic took **17.204s**.

| Runtime | Complete jobs | Actual window (s) | Mean whole-job time (ms) | CPU/job (ms) | Gen2/job | Peak accounted bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Before `7f5fbf70` | 4 | 1.281943 | 320.486 | 328.009 |128 | 33984 |
| After `db040f52` | 540 | 1.002184 | 1.856 | 2.169 |0 | 47968 |

Measured before source `7f5fbf70222de9ff96e9f241f2eff227fa911055` /
production `dcec7539f370c3622b55057a4fd91d0b5236e80c`; candidate source
`db040f52580ed89c1e932cddef1ec6b95fe8d5e5` /
production `9c68d231f03f6190a9119a5c078d2951854019a8`, on 2026-10-13.
Quiet Azure Linux VM, 4 logical processors, .NET SDK 10.0.401 / runtime 10.0.12.
The actual subject and observer DLL hashes are in [prepared.json](reproduce/v2/prepared.json).

## Fixed boundary and limits

The [same script](reproduce/v2/pressure-bound.py) keeps 32,000 UTF-8 bytes at an
unchanged 48,000-byte cap, catches 128 failed 32,000-byte admissions using `as error`,
and must return the exact declared catch/payload/retention/exposure counts.
Every measured execution has fresh state; code and the fixed host are reused.
The observer DLL and all other dependency files are identical for both versions;
only the actual Lython DLL differs. The before DLL is the already frozen immutable
H05 subject, verified before/after. Compilation and startup are outside measurement.

Both active and matched control processes keep the same 128MiB host graph
(131,072 arrays), with two declared full preparation collections, two real warm jobs
and the same two-second pause; all arrays must be Gen2 before measurement. Ordinary
GC/tiering settings apply. Nominal one-second closed-loop windows finish their last
whole job, explaining the actual durations above. Mean time includes Lython's Run
boundary, host activity and every result validation; it is not an isolated opcode
or a normalized CPython timing. Allocations/CPU/collection counts cover the process.

An exclusive VM lease, 30-second campaign and 8-second child / 1-second cleanup bounds
apply. All four corrected-v2 originals pass identity, exact result/cap, quiet CPU,
no paging/steal and host-age gates. [Raw distributions](raw/v2/) show only
504 before / 931 after active observations. These short
percentiles and heartbeat maxima are descriptive; they do not establish new latency
or SLO qualification. The previous [H05 qualification](../2026-10-13-embedding-exception-pressure/README.md)
remains tied to its own source/boundary and original targets.

The invalid v1 setup retained the old 1,000-observation output assertion in a
one-second diagnostic. All four original children aborted before printing metrics;
zero v1 samples qualified. [Original failures](raw/v1/) and their
[classification](v1-classification.json) remain preserved. V2 is one explicit
observer correction with a distinct source/hash/root; these are not replacement
samples in the original qualification campaign. No further micro run or ten-minute
lane followed. Both v1/v2 services are terminal, cgroups gone and the lease free;
[terminal proof](reproduce/v2/terminal-proof.json) is frozen.

## Adoption and remaining uncertainty

The implementation uses the largest single funded immutable payload visible through
bounded existing-state reads to prove an allocation cannot fit. It refunds nothing,
keeps preflights and normal live reclamation, and adds no scalar identity ledger or
policy switch. Unknown roots reduce proof coverage and leave normal relief in place.
Dead exception storage can accumulate toward the cap before ordinary young relief;
the higher peak above is expected and still within the unchanged limit.

Sync rebinding/list-element recovery and retained-alias rejection are tested.
Direct name deletion and RunAsync recovery without a host-yield boundary remain
limited by transient CLR references; a diagnostic proves a collection can be
attempted yet recover only after frame exit. The candidate is not a general R09 fix
or GC-independent accounting. Managed stack admission and nesting bounds stay.
Local full 10,793 Release tests and the isolated exact Release package consumer pass;
exact-head delivery CI is recorded in PLAN.md separately.

[Independent verifier](verify-evidence.py) recomputes raw distributions and checks
identity, eligibility and all originals. [Source archive paths](source-archive-paths.json)
and [probe/runner sources](reproduce/v2/) preserve reproduction; new rebuild hashes
must be recorded separately. Actual elapsed windows and CPU/job, rather than fewer
collections alone, support the bounded adoption decision.

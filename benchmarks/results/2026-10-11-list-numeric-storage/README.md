# Conservative numeric list storage and scoped random scratch

Governed lists now keep one subtotal for **64 bytes per occupied numeric slot**
instead of scalar identity dictionaries, refcounts and adoption rollback records.
The subtotal shares the existing list ownership snapshot. Numeric replacement
transfers its allowance; removal releases each outgoing slot. Positive net growth
is funded before mutation, and denied backing growth refunds its numeric delta.
Spare capacity and scalar-free slots retain their original backing estimates.
Large integer magnitudes keep their separate payload owner.

This removes **152 net production lines** from `PyList`: identity/refcount adoption,
staged rollback records, repetition refcount updates and scalar-release-only arrays.
It deliberately changes admission for aliases. The unchanged `[0] * 100000`
program now denies under its original 3 MiB cap rather than sharing one coupon.
Likewise the original eight-numeric-slot/32-byte governor attachment fixture now
denies before publishing the governor. Original fixtures, caps and both outcomes
are preserved. Python identity and live-memory reclamation remain intact.

The full-suite gate exposed a bounded recovery regression at an unchanged 1.5 MiB
cap. `random` population/weight/state drains committed scratch backing durably
without a release owner; the list policy exposed this existing cumulative leak
sooner. The fix uses existing caller temporary reservations, funds allocation and
coexisting replacement arrays before allocation, releases old backing after
replacement, and disposes scratch on every exit. Converted weight arrays now
reserve before allocation. This removes another **13 net production lines**,
for **165 total**, without a new owner abstraction or configuration switch.

## Frozen sources and correctness

| Role | Commit | Production tree |
| --- | --- | --- |
| Previously delivered | `e2464f75f6352f91b03870ad99c7b61653b4c7dd` | `52db6ede38d4dbf3a9b4c5f67277070451bce708` |
| Original policy and random failures / VM baseline | `a4818b7335852627c3d2ec5e2b2ff7537b36c312` | `52db6ede38d4dbf3a9b4c5f67277070451bce708` |
| Candidate | `d30dda32036a1f0c6c4e4fa74c74c681a26a3a4f` | `576e3bf4c49bc159741e22f3e45d9b30a3c2db5a` |

The original isolated gate reproduces eight prospective budget-policy differences
and two real random-drain failures, with one scalar-free control passing. The
longer random reproducer independently fails in both modes on the pre-list
production, establishing that the scratch leak predates this candidate.
Frozen baseline Release passes 525 of 535 checks and reproduces the same ten
failures. Candidate focused Release passes **542**.

Both complete Debug suites pass: **10,120 checks**, comprising 1,960 white-box and
8,160 public-boundary checks. Public tests ran on `0455a299`, then the entire
white-box suite ran after the final test-only drain-oracle update. Production and
public test source trees, and actual public/library/Probe DLLs, rehash unchanged
across that update. This qualification combines both suites; it was not one final
solution command. The initial bounded-recovery failure and the stale private
oracle outcome remain in the evidence. The oracle keeps its original 1,000-value
input and now verifies funding through caller disposal rather than a durable
unowned commit.

Checks cover fixed-slot aliases, independently owned large payloads, append/insert/
extension/repetition denial before mutation, numeric refunds after denied backing
growth, numeric replacement, deletion snapshot refresh, governor attachment,
scalar-free fees, partial extension, failed private construction, bounded refill
streams, retained/drop controls, cancellation and public projection. Random checks
retain seeded results, errors and unchanged recovery budgets. Four pure programs
strictly match CPython in both candidate modes at 1 MiB; the baseline matches three
and reproduces the recovery `MemoryError` in the fourth. Selected benchmark
outputs verify for both sources before timing.

## Short diagnostics

Two fixed passes use ordinary real workers on the dedicated Linux VM, .NET SDK
10.0.401 and CPython 3.13.16. Supervisor-only tiering configuration is removed
before worker launch; worker tiering, GC and protections remain normal. No contexts
or scopes are pooled. Eight timing groups take 7–8 seconds each including cleanup,
and both correctness groups take two seconds. Every group has a 30-second hard
bound. There are **346 protocol attempts, 168 measured batches, zero timing retries
and zero full lanes**.

Median microseconds per workload invocation:

| Workload | Pass | Baseline | Candidate | CPython | Candidate change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Empty entry | 1 | 23.63 | 23.09 | 1.50 | −2.26% |
| Empty entry | 2 | 22.68 | 22.83 | 1.47 | +0.66% |
| Integer loop | 1 | 1,131.89 | 1,135.80 | 578.10 | +0.34% |
| Integer loop | 2 | 1,106.40 | 1,140.46 | 751.60 | +3.08% |
| CSV discard | 1 | 570.43 | 560.47 | 169.07 | −1.75% |
| CSV discard | 2 | 542.62 | 541.65 | 160.35 | −0.18% |
| Numeric list build | 1 | 185.75 | 162.67 | 28.78 | −12.43% |
| Numeric list build | 2 | 195.58 | 169.71 | 29.52 | −13.23% |

The numeric-list case appends 1,024 integers and prints the same length/final-value
checksum in every engine. Both passes support the specific simplification:
candidate IQR/median is 0.78% and 0.57%, versus baseline 1.19% and 1.92%. Empty entry
changes direction, and CSV has 6.84–10.73% spread. The second integer-loop pass is
3.08% slower with under 1% spread; retain that signal, along with the earlier
alias-round loop signal, for subsequent qualification. No broad runtime gain is
established.

These collections are diagnostic, **not performance-qualified milestone lanes**.
The protocol measures no allocated-byte metric; removed maps, staging and copies
are source-proven, and no quantitative allocation saving is claimed.

All owned handles are stopped, process groups are empty, producers are clean,
actual DLLs rehash and the VM lease is free. The previous integer-alias producer
also rehashes unchanged. [Evidence](evidence.json) preserves original and intermediate
outcomes, source and binary identities, TRX hashes, declaration, complete protocol
attempts and spreads, pure comparisons and cleanup receipts. `SPEC.md` records
the list admission and random scratch contracts.

Other mutable scalar maps, weak reclamation, forced GC, general byte
materialization and stack telemetry remain separate pending work. Host-mediated
capabilities, cooperative cancellation, cycle detection and bounded projection
are preserved.

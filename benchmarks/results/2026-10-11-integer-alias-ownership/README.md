# Exact integer aliases retain their payload owner

Exact-integer views and conversions reboxed an existing `BigInteger`, separating
the visible alias from the box used to track its magnitude. A retained stream of
2,000 distinct 4,001-bit aliases could pass a 256 KiB limit while keeping over
1 MiB of magnitude arrays alive. The same paths disagreed with CPython identity.
The candidate returns the actual input or validated hook result for unchanged
exact integers. Changed magnitudes retain their independent funding.

This removes alias-result boxing and redundant ownership registration for
nonrounding `round` and positive `abs`. Six production files have a net **two
lines added**. No owner abstraction, numeric cache or configuration was added;
mutable scalar maps, weak reclamation and forced GC remain. This closes an ingress
prerequisite for conservative container charges, rather than completing P01.

## Frozen sources and checks

| Role | Commit | Production tree |
| --- | --- | --- |
| Previously delivered | `3881fa130c2a4db13e928bfa9b865e4654fa7bcd` | `ecf58aae9c7e49ecbc21ca08e78c09eb5ac831e5` |
| Original failures / VM baseline | `dde3f5470c9c69a36c670d77f34d1ddd53fd8243` | `ecf58aae9c7e49ecbc21ca08e78c09eb5ac831e5` |
| Candidate implementation | `559038f5d71510d1949444b29efd6d224983a774` | `52db6ede38d4dbf3a9b4c5f67277070451bce708` |

The original diagnostic passes ten controls and fails **84 checks** under
unchanged budgets. All 94 checks pass after the fix. Candidate focused Debug and
VM Release each pass **247 checks**; the baseline Release subset passes 143 and
reproduces the 84 original failures. Full Debug, with an explicitly built matching
Probe, passes **10,102** checks: 1,944 white-box and 8,158 public-boundary.

Checks cover views, constructors, hooks, floor-like functions, exact alias
identity, actual-box ownership without extra payload charges, distinct changed
results, bool normalization, rounding digits, invalid hook results, projection,
retained denial, and dropped streams under the same 256 KiB cap. A genuinely
delayed host read verifies escaped aliases and bound methods after source deletion
and asynchronous suspension. Host mediation and cancellation remain intact.

Four independent pure programs run in both actual modes on the VM. The candidate
matches all **27 identity values** and the dropped-stream/error controls. Raw
`Matches` is false for the identity matrix because CPython 3.13 emits its legacy
`int`/`__trunc__` deprecation warning and Lython emits no such warning; stdout is
identical. This explicit stderr difference is preserved in the evidence and is
not a strict probe pass. The other three programs strictly match. All four selected
benchmark outputs also verify for both sources before timing.

## Short diagnostics

Two fixed passes compare ordinary real workers on the dedicated Linux VM using
.NET SDK 10.0.401 and CPython 3.13.16. Supervisor-only tiering configuration is
removed before worker launch; worker tiering, GC and protections are unchanged.
No contexts or scopes are pooled. Each group includes cleanup in a 30-second
hard bound. Eight timing groups take 7–8 seconds each; two correctness groups
take two seconds each. There are **341 protocol attempts, 168 measured batches,
zero timing retries and zero full lanes**.

Median microseconds per workload invocation:

| Workload | Pass | Baseline | Candidate | CPython | Candidate change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Empty entry | 1 | 23.42 | 23.73 | 1.48 | +1.33% |
| Empty entry | 2 | 23.88 | 23.29 | 1.51 | −2.48% |
| Integer loop | 1 | 1,110.45 | 1,136.78 | 595.21 | +2.37% |
| Integer loop | 2 | 1,099.43 | 1,118.20 | 691.64 | +1.71% |
| CSV discard | 1 | 553.48 | 529.50 | 168.42 | −4.33% |
| CSV discard | 2 | 538.80 | 554.96 | 166.58 | +3.00% |
| Integer aliases | 1 | 1,237.45 | 521.60 | 141.65 | −57.85% |
| Integer aliases | 2 | 1,268.36 | 518.32 | 154.29 | −59.13% |

The alias diagnostic performs 512 iterations of five unchanged integer operations
on a funded 4,001-bit value, with the same output checksum in every engine.
Both passes support the specific change: candidate alias IQR/median is 0.26% and
0.95%, versus baseline 3.12% and 3.58%. The ordinary loop is 1.7–2.4% slower in
both passes, with approximately 1% spread; retain that small regression signal for
milestone qualification. Empty entry changes direction and CSV has 5.49–14.93%
baseline spread. These controls establish no broad runtime improvement.

These are diagnostic collections, **not performance-qualified milestone lanes**.
No quantitative allocation saving is claimed: the protocol does not measure
allocated bytes. Removed boxes and ownership lookups are source-proven.

All owned process handles are stopped, groups are empty, producer source trees
are clean, actual DLLs rehash, and the VM lease is free. The previous byte-conversion
producer also rehashes unchanged. An initial preparation script named the wrong
frozen branch and failed before build or measurement; its script, clone log and
terminal service receipt are preserved. Corrected preparation used fresh producer
roots and a new service handle.

[Evidence](evidence.json) records source and binary identities, original failing
test names, TRX hashes, prospective declaration, full protocol attempts, spreads,
pure output and warning differences, preparation failure and cleanup receipts.
`SPEC.md` documents preserved alias identity and payload lifetime. General byte
materialization, mutable container accounting, forced GC and stack policy remain
separate pending work.

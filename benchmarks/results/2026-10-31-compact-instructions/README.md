# Compact executable instructions

Baseline **21b556e9da6b81284fa0cb1db27f7887da67b712**, candidate that source plus
[source.patch](source.patch), 2026-10-31. The baseline production core is
byte-identical to bf33d502; 21b556e9 adds JSON evidence/cancellation tests only.
These are fixed source comparisons, not a new CPython baseline or current-HEAD
performance claim. Optional regex code/dependencies are unchanged and unused.

## Design and acceptance

Instruction storage duplicated domains already determined by the opcode, plus
four mutually exclusive operator fields. The candidate stores opcode, source
span and two integer operands. It deletes OperandKind, the tagged Operand record,
the four independent operator fields and three factory helpers. Named private
construction and checked operand projections/retargeting remain. No bit-packing,
unsafe layout, new backend, opcode/cancellation changes or public options/APIs.

B05's fetch/checkpoint block occupies 6–8% of guest CPU samples; this does not
attribute that entire cost to representation or predict a gain. The loop already
reads instructions by reference. The design reduces stored/copied array elements,
without reducing checks or reopening activation/lexical ownership.

Pre-recorded acceptance: >=40% smaller instruction-array elements, lower complete
compilation allocation on all six maintained scripts, meaningful field/tag/factory
deletion and no repeatable material complete-script/control regression. The
candidate meets those conditions. Existing conservative compilation admission
is unchanged; private/final instruction capacity and replacement overlap remain
funded. There is no allocation-threshold relaxation or new storage owner.

## Managed layout and complete compilation

The [adapted existing diagnostic](driver/Program.cs) allocates native managed
arrays of the real private instruction type and subtracts empty-array overhead.
It uses **no Unsafe/Marshal/native layout probing**. At 16,384 elements, storage
falls from **48 to 24 bytes per element (50%)** on this x64 runtime. This is a
measured implementation property, not a public portable-layout guarantee.

One local before / candidate / identical-before sequence measures complete
public Compile(source, sharedBudget) plus script disposal. Startup/execution
are excluded; common reflection overhead remains. Six unchanged maintained
sources each get four warmups and seven rounds of five compilations. Every
compiled owner is disposed and allowance returns to zero. No forced GC or local
Lython build/test overlaps these processes.

| Complete compilation | Before B | Candidate B | Identical-before B | Change |
| --- | ---: | ---: | ---: | ---: |
| config-edit.small | 182,744 | 176,192 | 182,744 | -3.59% |
| file-search.large | 166,728 | 160,872 | 166,728 | -3.51% |
| csv-summary-helpers.large | 268,448 | 258,344 | 268,448 | -3.76% |
| text-summary-helpers.large | 226,208 | 218,480 | 226,208 | -3.42% |
| json-summary-helpers.large | 250,400 | 241,064 | 250,400 | -3.73% |
| file-manifest-helpers.large | 309,184 | 298,600 | 309,184 | -3.42% |

Savings are 3.4–3.8% of complete compilation, not a claim of a 50% whole-frontend
improvement. [Before](before.json), [candidate](after.json) and
[identical-before](before-control.json) preserve all rounds, timings, machine,
layout, catalog and loaded assembly SHA256 identities. Local timings are
unqualified. Machine: Windows 10.0.26200, 28 logical processors, .NET 11 RC3;
ordinary GC, tiered compilation disabled equally. No background diagnostic work
remains. The maintained [catalog](../2026-10-31-architecture-profiles/representative.json)
SHA256 is `a2b51dfe71c890faeda7198687298f58b377f0c615bce2ecec065c20db16587f`.

## Fixed representative comparison

Two predetermined observations, sixteen jobs, **129.61 seconds**, below the
180-second collection bound. Both dedicated-machine quiet gates and all complete
stdout/file goldens pass. Machine is Ubuntu 24.04.4 Azure F4as_v7, four AMD EPYC
9V45 vCPUs / 16 GiB, pinned .NET 11 RC3. CPython 3.13.16 is a separately recorded
control in each job, not the baseline column below. These rows compare Lython
revisions and do not update docs/BENCHMARKS.md's primary external table.

The existing micro command uses one-second warmups, settling and seven rotating
25-ms batches per engine. Tiered compilation is disabled equally; ordinary GC
remains. Warm execution excludes compilation/startup; compile is frontend plus
compiled cleanup; compile-run freshly compiles/executes inside an existing host.
Declared fixture staging, host I/O, goldens and relevant cleanup remain inside
execution boundaries. No phase is inferred by subtracting unrelated medians.

Microseconds per complete job; delta is candidate / baseline minus one:

| Lane / workload | Observation 1 old / new | Delta | Observation 2 old / new | Delta | Old/new IQR, observations 1; 2 |
| --- | ---: | ---: | ---: | ---: | --- |
| compile / csv-summary-helpers.large | 168.7 / 236.2 | +40.1% | 239.8 / 154.9 | -35.4% | 11.2/8.1%; 21.6/11.1% |
| compile / text-summary-helpers.large | 125.2 / 116.4 | -7.0% | 125.5 / 116.0 | -7.6% | 5.6/4.0%; 7.8/2.7% |
| compile / json-summary-helpers.large | 150.6 / 147.2 | -2.2% | 163.4 / 144.1 | -11.8% | 25.9/16.0%; 17.9/5.4% |
| compile-run / config-edit.small | 3,065.4 / 3,071.0 | +0.2% | 2,922.5 / 3,036.2 | +3.9% | 4.0/7.7%; 10.4/5.1% |
| compile-run / file-manifest-helpers.large | 2,360.3 / 2,421.8 | +2.6% | 2,396.0 / 2,302.2 | -3.9% | 19.9/22.9%; 12.0/22.2% |
| warm / csv-summary-helpers.large | 7,454.7 / 7,468.3 | +0.2% | 7,343.3 / 7,330.3 | -0.2% | 6.4/6.7%; 5.3/4.8% |
| warm / text-summary-helpers.large | 5,801.8 / 5,761.6 | -0.7% | 5,899.7 / 5,666.3 | -4.0% | 7.3/7.6%; 6.7/6.8% |
| warm / json-summary-helpers.large | 8,766.4 / 7,996.7 | -8.8% | 8,134.3 / 7,879.1 | -3.1% | 7.3/4.8%; 9.5/7.0% |

CSV compilation reverses +40.1% to -35.4% and is inconclusive. Text compilation
point medians decrease 7.0%/7.6%; JSON compilation decreases 2.2%/11.8% with noisy
rows. Warm CSV is unchanged; text/JSON point medians decrease but overlap/noise
preclude a qualified overall speedup. Configuration increases 0.2%/3.9%; manifest
reverses. Preserve these outcomes; do not retime favorable rows or tune projection
branches to rescue timings. No repeatable material complete-script regression is
shown by the fixed observations. This does not prove absence under every workload.

[collection.json](collection.json) and [raw-results.tar.gz](raw-results.tar.gz)
preserve every receipt, sample, engine identity, machine gate and supervisor log.
[collect.py](collect.py) preserves the exact job list, exclusive VM lease, owned
outer deadline, per-job process-group caps and failure cleanup. All work finishes
and is joined; no retries or abandoned matching/worker processes.

## Validation and stopping point

Both targets pass **505 native / 3,256 public focused Release checks**: instruction
operand/retargeting checks, executable calls/control flow, operators/slices,
generators/comprehensions, real callbacks, compilation, refusal, cancellation and
retained ownership. Existing source diagnostics, async suspension, cleanup and
all benchmark outputs remain intact. Full delivery checks are recorded after
source commit; focused checks alone do not establish platform/package qualification.

A separate fixture correction observes the outer byte-file ValueTask's pending
status before releasing its producer. Callback entry had raced the synchronous
host promise check in preceding Ubuntu CI. The existing observed-ValueTask helper
fixes that witness without production changes, sleeps or weakened assertions;
all 14 host-borrow cases pass on each target. It is committed separately.

Stop this representation redesign after delivery qualification. The measured
storage/compilation gains do not explain the remaining CPython performance gap.
Do not extend it into a new bytecode engine, value representation or activation
pool. Final released regex-adapter qualification stays independent.

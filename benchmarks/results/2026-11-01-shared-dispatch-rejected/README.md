# Shared instruction-loop candidate rejected (B51)

2026-11-01. **Candidate removed:** it misses both frozen async-CSV CPU and
elapsed targets. Original production source restored; no new dispatcher, helper,
field, option or dormant backend remains. No speedup is claimed.

## Hypothesis and coherent deletion

The [async-hosting assessment](../2026-11-01-async-hosting-assessment/README.md)
finds a material CSV API gap and the async instruction loop as the largest named
self leaf. Those observations justify testing transport consolidation; they do
not predict a removable fraction or prove the loop causes that gap.

The [exact candidate](source.patch.gz) shares one ordinary nonasync instruction
loop, reads instructions by reference and returns an existing pending ValueTask
only to an async resumption wrapper. It shares block/PC/checkpoint and Python abrupt
routing, removing the duplicate async loop (79 net production lines removed).
Real await-capable operation helpers and appropriately different API cleanup
wrappers remain. Pending operands/owners stay live until actual users and disposal
finish; the exact instruction position resumes. Cleanup failures retain their
existing boundary. No new ownership machinery, queue, framework or public option.

The [prospective criteria](pre-code-plan.md) require >=7.5% complete process-CPU
AND >=3% elapsed improvement for async CSV against both controls, with <=5%
CPU/elapsed regressions for synchronous CSV/text. CPU/elapsed control drift<=5%,
allocation drift<=0.2%, every CPU batch>100ms. No retiming, tuning or relaxed gate.

## One frozen comparison

Before:082dd130 + B48 source.patch (same production code as95b5955c).
Candidate:20e208c0 + source.patch; intervening commits contain evidence/docs only.
Fresh nonincremental build; equal core/worker/test/probe copies, byte-identical
auxiliary host/adapter files. [Identities](candidate-identity.json) distinguish both
sources and hashes. Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB,
.NET11RC3, ordinary GC/tiering.

One **42.985-second collection /43.017-second service**; nine ordered
before/candidate/identical-before processes, four warmups/seven batches64 complete
jobs. Quiet/full stdout/file goldens/balances/CPU-resolution/control-drift gates
all pass. No profiler or favorable repetition.

| Complete warm job | Before CPU ms | Candidate CPU ms | Identical before CPU ms | Candidate CPU change | Candidate elapsed change |
| --- | ---: | ---: | ---: | --- | --- |
| Async CSV | 9.66716 | 9.76725 | 9.57419 | +1.04%/+2.02% | -2.15%/-1.53% |
| Sync CSV, control | 8.46323 | 8.33809 | 8.57502 | -1.48%/-2.76% | -0.05%/-0.44% |
| Sync text, control | 6.45816 | 6.15648 | 6.32430 | -4.67%/-2.65% | -0.68%/+0.24% |

Allocation essentially unchanged:9.904MB async CSV/9.543MB sync CSV/5.519MB text.
Sync controls meet regression limits, but the target misses both materiality
criteria. Preserve the possible CPU cost and modest elapsed observations without
calling either repeatable or causal. Do not retime or tune this prototype.

Warm execution includes fixture reset, efficient mediated real I/O, full validation
and result disposal; compilation/startup excluded. Shared unbounded funding
exercises normal ownership. Finite refusal is tested separately. Children join,
service inactive/dead/success and VM lease available.

## Correctness and restoration

Candidate builds with zero warnings/errors and passes408 native/871 public focused
Release cases on actual.NET11RC3/10.0.8. Existing independent cases cover genuine
host/protocol suspension, closures, generators/delegation, cancellation, terminal
refusal, admitted aliases/operands, awaited iterator cleanup and budget reuse.
No tests were added solely to enforce an internal design.

Restore original files with fresh timestamps, remove the new dispatch file and
perform a fresh nonincremental Release build. The same408/871 cases pass per
target on restored code. Production diff is empty; original source markers are
verified. [Restored identity](restored-identity.json) records equal fresh copies.
Its informational revision differs from the older measured baseline; no binary
reproduction or final-source timing is claimed. Unchanged production already has
terminal Windows/Ubuntu ordinary/original-stress/package qualification at95b5955c,
run41296168049; no redundant delivery campaign is started for rejected code.

[Summary](summary.json), [validation logs](validation-logs.tar.gz), source patch,
collectors/analyzer and [raw results](raw-results.tar.gz), SHA256
`9dd20eb6672350d560416cf7d9ead5e6d2e629e40676af7ac28360fd846fd690`,
preserve all samples, gates and sources/driver. A positional tar-exclusion error
was corrected during archive creation without repeating measurements. Stop B51.

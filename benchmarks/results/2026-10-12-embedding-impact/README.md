# Embedding impact: first bounded Linux matrix

The current generation relief has lower observed request p99 than full exhaustion
GC in this modeled host, but its largest heartbeat stalls are higher. Fewer full
collections do not establish better latency at every percentile or maximum.

The matrix took **149.22 seconds**: 21 independent children, 18 eligible samples
and three preserved file-script exclusions. Every child exited; the owning
service is inactive/dead, its cgroup is gone and the VM lease is free. No
ten-minute CPython comparison lane was run. Production source is unchanged.

## Fixed model and observed results

Pinned Linux .NET SDK **10.0.401**, runtime **10.0.12**, four processors, ordinary
runtime settings. Each child retains **128 MiB in 131,072 managed byte arrays**.
Eligible samples require every array to be in generation 2 before measurement.
A separate managed thread issues a small JSON request after a nominal 1 ms sleep.
Latency includes timer/scheduling lateness and request service time; heartbeat
lateness is recorded separately. This is a closed-loop observer with think time,
not a fixed offered-arrival-rate or queueing/SLO model.

Each child performs two complete warmup jobs, a fixed two-second preparation
pause and a four-second saturated measurement window, finishing its last job.
Three independent sessions reverse order in the middle session. Exact outputs,
file bytes, source/binary identities, >=1,000 observations, idle CPU, paging,
steal and host-generation gates precede eligibility. The outer child deadline is
28 seconds plus cleanup; the original campaign deadline is 240 seconds.

Values below are ranges across the three independent sessions, in milliseconds.
Added values subtract that session's matching host-only control; no pooled win
or population latency guarantee is inferred.

| Runtime / job | Eligible | Request p99 | Added p99 | Largest heartbeat stall | Largest added stall |
| --- | ---: | ---: | ---: | ---: | ---: |
| Current / host only | 3/3 | 0.062–0.066 | — | 0.073–0.077 | — |
| Current / CSV summary | 3/3 | 0.869–0.874 | 0.805–0.811 | 5.289–5.941 | 5.865 |
| Full-GC control / CSV summary | 3/3 | 2.502–2.560 | 2.440–2.499 | 3.320–3.971 | 3.897 |
| Current / text summary | 3/3 | 0.344–0.362 | 0.279–0.299 | 0.405–0.647 | 0.573 |
| Current / JSON summary | 3/3 | 0.695–0.717 | 0.633–0.653 | 1.037–1.139 | 1.062 |
| Current / file manifest | 0/3 | Excluded | — | Excluded | — |

All eligible current jobs satisfy the prospective model targets: added request
p99 <=5 ms and added largest heartbeat stall <=25 ms. This is evidence for these
finite Linux synchronous samples only. Current CSV performs **3,942–4,083 Gen0,
one Gen1 and zero Gen2 collections per roughly four-second sample**; the full-GC
CSV control performs **1,334–1,380 collections in each generation**. Counts include
natural and observer allocations. Zero Gen2 establishes no full collection in
those samples; positive counts do not independently identify induced events.
The remaining process-wide collection frequency and the cause of the larger
current maximum are explicit follow-up concerns.

The full-GC variant differs from current source only in MemoryGovernor exhaustion
relief; all managed stack, storage ownership, scalar charging, async-thread and
correctness fixes remain. The saturated model completes 85/88/88 current CSV
jobs versus 29/29/30 full-GC jobs. Those differing completion counts explain
exposure to allocation/collection traffic. They are not CPython ratios, a
general performance guarantee or an isolated dispatch measurement.

## Exclusions and remaining gates

All three file-manifest children validate complete outputs but fail the declared
old-host gate: their arrays begin at generations **6,709 / 15,961 / 108,402**,
rather than 0 / 0 / 131,072. Their raw data remains published and is excluded.
No completed case was rerun or threshold relaxed.

Two Windows prototype attempts are excluded: a four-second observation window
had insufficient samples, and a prospective sixteen-second smoke yielded 988,
below the unchanged 1,000-sample gate. No native timer or stack probing was added.
Linux preparation built both artifacts successfully but its shell wrapper then
exited 127 on a trailing CR line. Existing artifacts were independently hashed
and finalized without rebuilding; the original failure receipt is preserved.

This phase does **not** close the containment milestone. Missing gates include
asynchronous embedding impact, Windows latency, file-workload host comparability,
retained-data pressure impact and architectural independence from forced GC.
The current synchronous CSV/text/JSON evidence does not license an unconditional
latency guarantee for other heaps, request models, GC settings or applications.

Four additional public cases now retain the exact representative CSV/JSON records:
Run and genuinely delayed RunAsync reject with MemoryError under the same
256 KiB cap that admits dropped-data jobs. All **32 focused Release script tests**
pass, including exact complete outputs and read cancellation. Production code
was not changed for those tests.

## Reproduction and provenance

[Evidence](evidence.json), [raw observations](raw/) and
[probe/runner sources](reproduce/) retain every sample and exclusion. Probe
source is fully managed with unsafe compilation disabled; it links the public
test host and consumes the unchanged ordinary scripts and independent goldens.

Current producer: f4c6c3a8124a593d30f26fb51bce645ec61122d0.
Private control: af66d61ce6534ce108ecac8ff3798794f6083d7c.
The included control patch reproduces that source difference without replacing
the current production branch. Rebuilds must record their own actual DLL hashes.

Generate fixtures with tools/verify-representative-scripts.py, then build
reproduce/Probe.csproj in Release with -p:SubjectRoot pointing to the chosen
checkout. Invoke Probe.dll with the case, generated fixture root and
benchmarks/scripts directory. The published runner records bounded process
cleanup, environment/noise/identity gates and fixed session order. Preparation,
raw observations and validation receipts are separate from release NuGet outputs.

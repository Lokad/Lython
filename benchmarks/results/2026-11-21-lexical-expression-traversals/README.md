# B1263 lexical expression traversals

Compared `31abfeb3c` with `405a8bcf9` on November 21, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The source
is retained at `51b750ed1`. Syntax
validation establishes when lexical preparation needs expression
bindings or expression scopes. Ordinary scripts keep statement
bindings, function layouts and postponed annotation preparation
while avoiding both expression revisits.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated validator | Compile | −7.11% to −5.81% | −7.38% to −5.78% |
| Inherited dataclass configuration | Compile | −0.12% to +1.42% | +0.63% to +2.60% |
| Generated validator | Compile-to-result | −7.34% to −6.01% | −6.05% to −5.46% |

Ranges cover all pairings of two independent worker medians on each
image. The dataclass script contains a generator expression and keeps
both expression visits. Its CPU change is adverse despite a mixed
elapsed range. The validator's gains appear across both clocks.

Compilation includes disposal of its owner. Compile-to-result adds
execution, mediated RAM-backed input, output verification and
prior-result cleanup. Process CPU covers the complete request.
Startup and final teardown are outside guest clocks. All 84 timed
batches passed in a 276-second campaign.

Local compile allocation falls 0.76% for the validator and 2.67% for
postponed factories. Dataclass allocation is flat. Warm allocations
and accounted peaks match across all three cases, and shared allowance
returns to baseline. Both frozen 32-file images passed the three
CPython goldens. The measured patch passed 920 public and 88 white-box
focused Release checks on Windows .NET 11 and Ubuntu .NET 10.

## B1264 exact-image confirmation

Same images and goldens, with longer 22-second per-worker warmups
and 0.9-second target batches. The dataclass control ran first.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Inherited dataclass configuration | Compile | −1.39% to −0.36% | −1.52% to +0.43% |
| Generated validator | Compile-to-result | −5.03% to −4.93% | −6.63% to −5.60% |

The original control CPU increase does not recur. Validator
compile-to-result improves on both clocks again, with both worker
elapsed spreads below 0.1%. All 56 timed batches passed in a
239-second campaign. Both runs passed their quiet and archive checks.

[Confirmation summary](confirmation/summary.json) · [Confirmation observations](confirmation/observations.tgz).

[Summary](summary.json) · [Local allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).

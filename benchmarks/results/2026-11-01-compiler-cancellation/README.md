# Ordinary compiler cancellation (B47)

Correctness change against **6b631425**, 2026-11-01. Ordinary parsing previously
received the token without observing it. Compilation could also return a valid
script after cancellation during annotation working-storage admission.

Central token consumption now checks cancellation, including recovery/trivia
paths. Source/literal scans and lookahead loops check progress; formatted-string
helpers forward the current token. Major later-phase transitions check terminal
admission refusal before cancellation. Remove manual recovery increments and
the uncancellable bytes-literal predicate scan. No public option, watchdog,
counter, parser framework or memory infrastructure is introduced.

Six new native parser regressions fail on the original source. A deterministic
immutable token view triggers cancellation during parsing; reaching its end after
cancellation fails, so an exit-only check cannot pass. The public annotation
storage test also fails originally when admission succeeds. Its simultaneous
refusal variant already passes and preserves that first origin. The in-flight
and refusal cases retain ownership until cleanup and demonstrate allowance reuse.
Original failure logs accompany this evidence.

Cancellation returns an invalid `LA2011` script; disposing it ends its owner.
Refusal remains authoritative. Existing diagnostic messages, Python behavior,
managed stack bounds and coarse preflights remain. Later diagnostic/annotation/
lexical-fact/lowering/instruction passes can still finish their current work before
phase checks; CLR copies/conversions are not preemptible. This is not a claim of
in-flight cancellation inside every compiler pass. The current
[runtime guide](../../../docs/RUNTIME.md) records this contract and limitation.

## One fixed cost diagnostic

Measured **6b631425 + the raw archive's source.patch**, dedicated Ubuntu 24.04.4
Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3, ordinary GC/tiering.
Nine ordered before/candidate/identical-before processes; **53.406 seconds**
collection / 53.428-second service, below the 60-second bound. No retiming or
performance acceptance target: cancellation is required independently of cost.

| Per job | Before | Candidate | Identical before |
| --- | ---: | ---: | ---: |
| CSV compilation CPU, us | 88.000 | 87.246 | 87.045 |
| Text compilation CPU, us | 68.208 | 68.190 | 66.751 |
| Warm text CPU, ms | 6.206 | 5.323 | 6.266 |
| Warm text elapsed, ms | 6.804 | 6.811 | 6.689 |

Compilation CPU changes -0.86%/+0.23% CSV, -0.03%/+2.16% text; allocation is
unchanged. Baseline CPU drift is below 2.14%, allocation drift below 0.001%; all
CPU batches exceed 100 ms. This short observation establishes no clear slowdown
or speedup, repeatability or external-baseline multiplier.

**The unchanged warm runtime has a 14–15% CPU change without elapsed improvement.**
That is uncertainty in the control, not a runtime gain caused by compiler checks.
Process CPU includes background JIT/GC. Do not equate such CPU observations with
guest instruction cost or elapsed improvements; no favorable recapture follows.

Compile samples use 512 warmups / seven batches of 4,096 fresh public compilations,
validity checks and disposal. Startup/guest execution are excluded; full output
goldens run before/after. Warm samples use four warmups / seven batches of 128
complete public jobs, including fixture reset, mediated I/O, goldens and result
disposal, excluding compilation. Shared unbounded funding exercises normal
ownership; finite refusal is separately tested. Quiet gates, complete goldens,
budget balances and joined cleanup pass. Service inactive/dead/success; lease free.

Fresh nonincremental copies agree and auxiliary binaries are byte-identical.
Measured core SHA256 `5a18c6fb14e740a6baee2eba4b45deb6562b0b0561fbbae24d2ea72108adce03`;
baseline `dfa2e2df7a97386ad5304817c9681879bf00fecdbf64122e9f78e987503826b6`.
The timing precedes three additional lookahead checks found during source review.
[Final patch](final-source.patch.gz) and [identity](final-source-identity.json)
preserve the completed source separately; do not call these final-HEAD timings.
No second comparison was run for the coverage correction.

Final source passes 111 native / 301 public focused checks per actual .NET 11 RC3
and .NET 10.0.8; build has zero warnings/errors. Before those three lookahead
checks, full local ordinary suites pass 3,962 native / 15,499 public per target.
Platform/stress/package delivery is recorded in PLAN.md when terminal; these
local results do not imply its completion.

[Summary](summary.json), [prospective criteria](pre-code-plan.md) and
[raw results](raw-results.tar.gz), SHA256
`3f384f6ba736b7207dcca046cd8c305bed33a417d3966c4dbdaa1a001e6eca6f`, preserve all
samples, machine gates, source/assembly identities, measured patch, catalog and
existing diagnostic adaptations. No new benchmark framework or core dependency.

# B1835 Place deferred definition storage while emitting instructions

On `19df23f55`, the compiler assigns the final keyword slot when
linearization already identifies the surviving declaration and its
store. This removes the subsequent slot placement loop. The existing
deferred-site table and runtime workspace still own the same values.
The ordinary factory removes one visit to each of its 93 deferred sites.

Fifteen compilation fixtures produce identical instruction tapes,
including source spans, frame capacities, deferred-site indices,
captures and native-return facts. The comparison covers a deferred
definition before a returned callable pair as well as forwarded reads,
rebindings and ineligible scopes. The 113 tested budget levels match
admission, first refusal and diagnostics. The cancellation snapshots match.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 593 focused Release
checks. The four scripts match independent CPython 3.13.2 stdout and file
goldens. Windows allocation diagnostics restore the borrowed allowance
after every run. Compiled admission and execution peaks match in both APIs.
Median CLR allocation differs by at most 56 bytes per invocation.

## Local timing

Each WSL .NET 11 lane uses one logical CPU and two workers per source,
with five seconds of warmup and seven rotated batches per worker.
Ranges span all four pairs of worker medians. Compilation includes
compiled-owner disposal. Compile-to-result adds verified execution, any
mediated I/O and cleanup. Process CPU also includes framing and fixture
reset. Process startup sits outside these lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compilation only | −1.37% to +6.47% | −2.42% to +7.89% |
| Protected file search control | Compilation only | +0.72% to +4.68% | −2.06% to +5.27% |
| Escaping rules control | Compile-to-result | −19.16% to +3.45% | −8.62% to +14.91% |
| CSV invoice control | Compilation only | −2.45% to +4.76% | −4.20% to +3.23% |
| Ordinary definitions | Compile-to-result | −11.32% to −4.78% | −0.66% to +2.31% |

Ordinary compile-only elapsed and CPU span zero in the local run.
Compile-to-result elapsed falls 4.78–11.32% with CPU spanning zero.
Protected search elapsed rises 0.72–4.68% with uncertain CPU. The unchanged
escaping-rule candidate workers differ by 20.65% in CPU. Retain the
removed compiler pass and matching compiler/ownership facts. A general
compile CPU improvement remains unproven. The external B1834 comparison
uses the reference core and stays separate.

All 140 batches verify. Workers join, guest fixtures are removed and
the persistent archive verifies.

[Images](identity.json) · [Medians and spreads](summary.json) ·
[Allocation and admission](allocation-summary.json) · [Compiler parity](compiler-parity.json) ·
[Observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Exact patches and checks](procedures.tar.xz).

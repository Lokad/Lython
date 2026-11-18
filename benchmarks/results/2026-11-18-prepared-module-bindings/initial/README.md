# Prepared live module binding prototype

R520/B949,2026-11-18UTC. **Private, unpromoted** a5038a28 versus corrected committed
9e8eaba1 / 84b1a159; reference full44250743374 independently pending. Ten production
files. Existing lexical compilation records completed child code; one prepared
name-to-slot location array per linked code replaces repeated hash lookup in
ordinary/global operand reads under exact module-code identity. No guest values
cached, no AST traversal/runtime lookup-cache layer/public option. Independent
code and inactive/different module frames retain generic namespace resolution.

This differs from R479's withdrawn spare-instruction/name-guard fixup: R509 has
since removed ordinary global producers. Preserve its adverse fresh/unstable warm
evidence; do not revive that source unchanged. Existing rough compilation admission
covers added metadata; the new preparation loop forwards the active token. All
mutable values stay with their existing owners; cached compiled code retains no
execution/token/guest-value reference. Ordinary generic location discovery is
removed only where the module/code relationship proves the live slot.

Both actual.NET11/10:106internal/374public focused Release checks pass, including
old live-global/builtin/deletion, closure/class/type-parameter/import, suspension,
cancellation/refusal, aliases and cleanup regressions. Nonincremental solution
build and actual32-file13goldens/allocation/reuse/four timed-case goldens pass.
Warm allocation/accounted peaks are flat; compilation metadata increases storage
within its already-admitted coarse owner. No allocation-reduction claim.

| Whole script | Phase | Elapsed change | Wider CPU change | Reference / candidate spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON report | Compile | +1.97..+2.34% | +1.08..+2.71% | 0.08 / 0.29% |
| Same | Warm | −14.63..−8.93% | −13.98..−6.81% | **4.95** / 1.65% |
| Same | Compile-to-result | −6.71..+3.71% | −6.54..+2.97% | 2.15 / **8.84%** |
| Helper text | Warm | +0.53..+2.64% | −0.48..+2.93% | 1.10 / 0.99% |
| Positional calls | Warm | −1.49..+0.52% | −3.53..+0.83% | 0.33 / 1.71% |

Five jobs232.65s, eight-second rotated full-request warmups/five rounds, default
GC/tiering. Same quiet VM/toolchain/boundaries as earlier source pairs; warm Run
includes mediated RAM I/O, goldens and previous-result cleanup, fresh adds compile/
compiled-owner disposal. Startup/reset/protocol/final caches are outside guest
clocks; process CPU wider. Ranges combine process medians, not confidence intervals.
Each process IQR is stable while fresh process controls disagree. Therefore warm
benefit is promising and fresh latency unresolved. Compilation adversity remains.
One bounded unchanged-image warm/fresh/helper confirmation uses12s warmups and
opposite initial roles; no extra compile run or whole qualification campaign.
Retention/refinement/debt/withdrawal depends on usable observations, no rigid veto.

All users joined, quiet/golden/Closed/exit/SHAarchive pass before RAM teardown.
Only exact archived2638862f core replicas retired with fresh path/UID/hash/all-eight-
alias proof. Current2ede/a71/c036/84images/catalogs/toolchains/unmatched evidence
and254/252MiBfloors remain. Publication held; objective remains active.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[source](candidate-sources.tar.xz),[patch](prototype.patch.gz),
[checks](checks.json),[procedures](procedures-and-checks.tar.xz).

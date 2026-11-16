# One compact native iteration-result protocol

R448/B849–B850, 2026-11-16 UTC. Retain the complete protocol migration for
simplification, lower allocation and numeric whole-script evidence. Reference
qualified R445 `cc794368`, frozen core `16c37b02`; measured candidate private
`d545a2ad` plus [exact source](measured-sources.tar.xz), core `3bb9da5a`.
No public API, policy or regex-provider change. [Full Windows/Ubuntu actual .NET11/10, original stress and installed-package delivery passes](delivery/README.md).

Replace synchronous bool/out-object iteration with the single result already used
asynchronously. Store the existing executable slot; its unassigned state replaces
the separate exhaustion flag. Both former result and replacement have measured
16-byte array stride on actual Windows .NET 11/10 and Ubuntu .NET 11. This is
managed allocation evidence, not a general layout guarantee or identity proof.
Delete the range-only integer pull and its native-prefix detour. Move native
slots through generator yields, acquired iterator wrappers, executable loop stores
and sum. Object consumers retain materialization boundaries. No larger operand
carrier, typed-iterator side protocol, ledger, per-yield owner, cache or unsafe API.

| Complete source / phase | Elapsed change | Wider process CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| B849: scripts.numeric-squared-error.large / compile | -2.42..-0.09% | -3.63..-0.31% | 0.05 / 2.34% |
| B849: scripts.numeric-squared-error.large / warm | -12.97..-4.65% | -14.56..-3.96% | 3.10 / 6.27% |
| B849: scripts.numeric-squared-error.large / compile-run | -14.00..-2.40% | -13.66..-3.15% | 8.14 / 4.95% |
| B849: scripts.numeric-squared-error.async / warm | -7.87..-3.33% | -8.17..-3.32% | 0.04 / 4.90% |
| B849: scripts.numeric-report.integer.large / warm | -3.98..-2.99% | -2.17..-0.73% | 0.05 / 0.98% |
| B849: scripts.text-normalization.large / warm | -2.24..+9.56% | -1.65..+7.81% | 8.86 / 2.95% |
| B849: calls.positional.large / warm | +1.41..+2.64% | +0.03..+2.88% | 1.08 / 0.13% |
| B850 confirmation: scripts.numeric-squared-error.large / warm | -15.49..-4.88% | -13.78..-5.08% | 2.47 / 9.85% |
| B850 confirmation: scripts.text-normalization.large / warm | -1.00..+10.12% | -1.33..+8.12% | 7.32 / 3.64% |
| B850 confirmation: calls.positional.large / warm | +2.42..+2.96% | +1.66..+3.63% | 0.34 / 0.19% |

B849 seven jobs 210.648 seconds; B850 bounded three-job confirmation 94.052 seconds.
Same five-case catalog (SHA256 `5575335e`), source/fixtures/stdout/file goldens,
32-file image identity, normal optional 0.4 adapter and unchanged worker/protocol.
Two independent processes per source; four-second full-request warmup, five rotated
approximately half-second samples. Four pairings of medians form ranges, not
confidence intervals. Every image/Ready/golden/quiet/Closed/exit/join/archive gate
passes. All users are joined, verified archives persist and RAM fixtures are gone.

The squared-error script reads 4,096 numeric pairs from 36,231 bytes of JSON,
calls a helper in a generator reduction, and writes a complete JSON report. Its
672-byte source and CPython 3.13.16 independent goldens are archived. Warm includes
mediated RAM I/O, complete stdout/file hashing and prior-result cleanup; excludes
compilation, startup, fixture reset/protocol and final cache teardown. Fresh adds
compilation and compiled-owner disposal; compilation measures code construction
and disposal only. Wider process CPU includes reset/protocol. Independent phase
medians are not additive. Async input completes synchronously; this is not a
pending-I/O latency comparison. No refreshed CPython timing, RSS, embedding-tail
or general throughput claim is made here.

Local natural-GC whole-process allocation is 821,688 → 690,448 bytes for the report
(−15.972%) and 821,792 → 690,736 through RunAsync (−15.948%). Conservative execution
peak remains 906,718 bytes. Ordinary numeric/text/call allocations are effectively
flat; all stdout/files, compiled baselines and disposal-to-zero checks pass.
Allocation reduction is separate from timing. Every numeric elapsed/CPU pairing
favors the candidate, but large unchanged process spreads prevent a precise effect
size. The second collection confirms direction, not a universal 5–15% promise.

The ordinary call loop is repeatedly about 2–3% slower, with supporting CPU in the
confirmation. Accept that measured debt at this coherent migration boundary;
investigate general cursor/result transport before any new range-only shortcut.
Normalization controls move 7–9%, with pairings from slightly favorable to +10%:
no clean regression or no-regression claim. Preserve uncertainty and the originals.
This does not explain or close the existing normalization/CPython gap.

Five independent public Python goldens cover held integers, tuple aliases, retained
sum callback inputs, reentrant generator advancement, None/False and exact overflow.
Both APIs return allowance to the compiled baseline, then zero. Before final tidy,
757 internal / 3,010 public focused checks pass on each actual Windows target;
full local .NET 11 ordinary passes 4,806 internal / 17,935 public. Existing checks
retain genuine suspension, first refusal, cancellation and awaited cleanup.

Final source cleanup removes unused out-local initializers, formats the tee adapter
and directly forwards native sync results through the user iterator's async wrapper.
The measured image remains immutable. Managed PE method-IL comparison identifies
16 changed helper bodies only; all generator/sum/cursor/executable and benchmark
helper bodies are identical. This is not binary equality or a timing claim for
those helpers. Final source/checks and the comparison program are archived. Final nonincremental
Release build passes both targets with no warnings/errors; final selected ordinary
checks pass 1,119 internal / 2,001 public each actual Windows .NET 11/10.
An earlier expanded filter also included selected original stress: 1,130/2,028 each
target pass; its duplicate TRX name overwrote the internal report, so full console
logs retain both summaries. Final ordinary reports use distinct per-suite names.
An overbroad tidy briefly removed required None initializers in two test iterators;
the compiler rejected it and their exact tested snapshots were restored. No
assertions or timeout/containment guarantees were weakened.

All initial rewrite/build/fixture and layout-target failures remain in the procedure
archive. The original slow read-only inventory completed (despite its misleading
local `initial-stopped-inventory.json` name); no mutation was interrupted. An early
confirmation retrieval was rejected by its local not-before guard before SSH and
produced no VM observation; both authoritative terminals are now cached forever.
Only three exactly proved committed working JSON replicas were retired before
B849 under the idle lease, after UID/path/hash/inode/all-alias verification. No
retirement was needed for B850; images are verified hardlinks. Canonical evidence,
selected images/toolchains and 254/252 MiB floors remain. Publication/tagging held.

[Summary](summary.json), [confirmation](confirmation-summary.json),
[allocation](allocation-summary.json), [first raw archive](observations.tgz),
[confirmation raw archive](confirmation.tgz), [measured sources](measured-sources.tar.xz),
[final sources](final-sources.tar.xz),
[checks/procedures/images](procedure-and-checks.tar.xz),
[decision](decision.json). The substantial whole-script roadmap continues.

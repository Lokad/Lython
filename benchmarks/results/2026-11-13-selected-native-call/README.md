# Selected native call entry

2026-11-13UTC, frozen485e008e versus fully qualified40d92aef/91d0540d.
Three runtime files change; no frontend, public API, memory policy or backend
dependency change. Focused Release11:186internal/440public cases pass, including
actual256KiB continuation recursion, once-only suspension, live defaults/receivers,
terminal refusal, cancellation, retained aliases and joined cleanup/reuse.

The current compiled-layout gate finds only3free caller operands versus6callee
slots in search, and7versus12in invoice. Do not grow every caller into an arena.
The selected deletion instead removes the native start's impossible decline result,
duplicate eligibility selection and false-return branches. A selected call completes
or transfers its exact work once. Native bodies validate the managed stack while
reusing call admission, as ordinary bound bodies already do, without another
logical interpreter-depth increment. Function recursion, native-depth handoff,
managed stack probes, exception context and pending-child admission remain.
Internal tests stop asserting the removed boolean; their independent assertions
are preserved. No replacement controller, configuration or execution route is added.

## Complete-script comparison

Same quiet AzureF4as_v7/Ubuntu24.04.4, fourEPYC9V45vCPUs/16GiB/no swap, ordinary
.NET11RC3 GC/tiering, fixedfec79worker/catalog/adapter0.4. Two independent processes
per source,5s warmup, five rotated500ms rounds. All goldens/shared balances/source
identities/quiet/join/SHA-archive gates pass. B680 five jobs finish229.223s.

| Warm script | Elapsed change | Wider process CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Keyword-default search |−6.01..+7.53%|−5.12..+6.24%|6.77/6.68%;3.10%|
| Captured text |−5.86..−0.55%|−5.26..−0.60%|3.04/2.53%;1.08%|
| JSON-lines |−1.80..+0.41%|−0.78..+0.59%|1.58/0.64%;0.89%|
| Invoice |−3.43..−1.08%|−5.30..−1.04%|1.11/1.28%;1.70%|
| Report |−5.56..+0.60%|−2.58..0.00%|3.28/2.94%;3.37%|

Ranges span all four independent median pairings, not confidence intervals.
Negative means less time. Warm includes mediated RAM-file I/O, full output/file
validation and prior-result cleanup. Compilation/startup/staging/reset/protocol/
final cache teardown are excluded; wider CPU includes reset/protocol. No fresh,
compilation, allocation, RSS, embedding-latency or new CPython claim is made.

**Retain for simpler call ownership and the supported modest text/invoice direction.**
Search is uncertain because unchanged controls move materially; JSON/report timing
is also inconclusive. These observations do not resolve the earlier integer-chain
report debt or complete the substantial whole-script performance objective.
Full platform/original stress/installed-package qualification is pending at retention.

All VM users join and RAM traces verify before unmount. Only the withdrawncf window
core's aliases are retired under fresh idle-lease/UID/inode/hash/canonical-evidence
proofs; its original data and exact bits remain preserved. Existing free-space
floors stay unchanged. An initial local inspector build raced a clean reference;
the corrected gate uses the frozen qualified core. No timing is taken from that
failed build. The first generated launcher fails local indentation parsing before
any VM mutation; its correction validates the actual91reference-source identity
before collection. Publication remains postponed.

[All medians/controls](summary.json),[raw observations](observations.tgz),
[exact sources/bits/checks](source-checks.tar.xz).

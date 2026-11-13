# Byte-bound window header capacity: withdrawn current form

Frozen cf762db1 versus fully qualified40d92aef/91d0540d,2026-11-13UTC. Delete the
counted form's extra line scan: every unbounded non-inline view consumes at least
9decoded UTF8 bytes, so remaining bytes bound its header capacity. One grant/window
stays with the execution owner, including unused headroom after close/early exit.
Sized/accumulated reads keep independent funding. No per-object credits or tracing.

198internal/106public Release11 checks pass on this source, including first refusal/
cancellation before reader movement, retained aliases, mixed UTF8/UTF16 reads,
refills, Python differential behavior, gzip protocols and awaited cleanup/reuse.

| Complete warm script | Elapsed change | Wider CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Keyword-default file search |−12.88..+5.66%|−10.84..+3.60%|9.18/10.15%;2.01%|
| Captured text |−2.94..+4.01%|−4.49..+3.65%|4.43/2.61%;1.98%|
| JSON-lines |−2.98..−0.08%|−2.00..−1.33%|2.00/0.92%;1.03%|
| Invoice control |−1.16..+2.79%|−1.92..+3.08%|1.05/2.90%;1.87%|
| Report control |−2.90..−1.07%|−2.75..−0.35%|1.10/0.77%;2.10%|

B678 five jobs228.425s; same quiet VM, fixed worker/catalog/adapter, source-pair
protocol and five rotated500ms rounds as the[counted form](../counting/README.md).
Warm includes mediated RAM-file I/O/full goldens/prior-result cleanup. Startup/reset/
staging/protocol/final cache teardown are outside; widerCPU includes reset/protocol.
Ranges span four paired medians, not confidence intervals. Large unchanged search
controls establish uncertainty, not a failed design or a generic speedup. The
small JSON result has supporting CPU; report is an unaffected control and does not
attribute its gain to header admission. No new CPython or host-latency claim.

B679 uses the existing eight-fixture ALC allocation tool, unchanged source rebuilt
nonincrementally before freezing its binary; no new framework. It finishes25.683s
with all goldens/shared balances/cleanup/quiet/identity/join/SHA-archive gates.
Allocation is effectively flat(−0.06..0.00% across fixtures; affected search/JSON
exactly flat). Its clocks remain diagnostic, sharing CoreLib/processGC. The tool's
existing admitted-peak capture runs outside allocation samples:

| Script | Reference peak charged root B | Candidate B | Change |
| --- | ---: | ---: | ---: |
| Search |244644|579236|+136.8%|
| Guarded search |248292|582884|+134.8%|
| Captured text |1778425|2025849|+13.9%|
| JSON-lines |4162639|5210575|+25.2%|
| CSV validation / invoice / both reports |Unchanged|Unchanged|0%|

These are **conservative allowance peaks, not CLR heap or RSS**. They quantify
earlier rejection/headroom, not increased physical allocation. The same source
still fits the focused1MiB mixed-read fixtures and returns allowance at cleanup.

**Withdraw this implementation**, retaining useful semantic regressions. It adds
window admission state and unused retained capacity while keeping individual
funding for other read paths. The measured headroom cost and limited/uncertain
whole-script benefit do not justify that complexity. This is a tradeoff decision,
not a fixed percentage veto or permanent ban on capacity-level admission. Fewer
external allowance calls alone do not establish better embedding latency. A future
coherent storage redesign needs a new removal/ownership argument; do not retime
unchanged bits to average away these controls. Restore qualified production and
force a nonincremental build before checking the retained regressions.

Restoration is verified: all573production inputs normalize-match fully qualified
40d92aef. Forced nonincremental Release11 builds succeed;198internal/106public
checks pass, including the retained BufferedLineAdmission and MixedTextWindowReads
cases. [Restored checks/source proof](restored-checks.tar.xz). Reuse the source's
cached full qualification; no new runtime change or unchanged full-CI replay.

All VM users join and RAM archives verify before unmount. Only canonically verified
obsolete fd/2e VM aliases were retired under fresh idle lease/inode/UID/hash checks;
their exact bits, source and qualification remain archived. Current91 and every
adverse condition survive; no floor is lowered. Publication remains postponed.

[All medians](summary.json),[raw timing](observations.tgz),
[allocation](allocation/summary.json),[charged peaks](allocation/admitted-peaks.json),
[exact sources/bits/checks](source-checks.tar.xz).

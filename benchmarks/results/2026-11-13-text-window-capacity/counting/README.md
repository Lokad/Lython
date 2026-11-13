# Counted window header capacity: intermediate, not promoted

Frozenfd258a65 versus fully qualified40d92aef/91d0540d,2026-11-13UTC. One decoded
window grant replaces per-long-line header admission on unbounded readline/iteration.
A scan counts potential remaining long lines; a window marker retains admission.
Sized/accumulated reads remain independently funded. Unused capacity stays with the
execution owner after early exit/reader close; first refusal remains terminal.

Final198internal/106public Release11 cases pass, including UTF8/UTF16/newlines,
mixed reads/refills, retained aliases, cancellation/refusal before cursor mutation,
awaited cleanup, budget reuse, Python differential checks and64gzip cases. An earlier
198/42run precedes adding checks within the new scan; final source repeats those
checks and includes the gzip boundary. Full delivery is not claimed for this form.

| Complete warm script | Elapsed change | Wider CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Keyword-default file search |−1.87..+0.26%|−0.18..+1.08%|2.15/0.03%;2.16%|
| Captured text |−4.33..+0.06%|−3.44..+0.59%|3.50/0.92%;1.71%|
| JSON-lines |−0.96..+1.87%|−0.12..+2.65%|2.44/0.41%;2.35%|
| Invoice control |−3.89..+1.52%|−1.49..+1.52%|3.56/2.00%;2.24%|
| Report control |−5.92..+0.38%|−4.57..+0.95%|5.51/0.82%;2.35%|

B676 five jobs228.141s, quiet AzureF4as_v7/Ubuntu24.04.4/fourEPYC9V45vCPUs/16GiB/no
swap, actual.NET11RC3 ordinary GC/tiering, fixedfec79worker/adapter0.4. Two
processes/image,5rotating500ms rounds,5second warmups(12captured,8JSON/report).
Ranges span four paired medians, not confidence intervals. Warm includes mediated
RAM-file I/O, full goldens and prior-result cleanup. Startup/reset/staging/protocol/
final cache teardown are outside; widerCPU includes reset/protocol. All source/
output/shared balances/quiet/join/SHA-archive gates pass; no remaining VM users or
mounts. No stable CPU gain, allocation observation, host-latency improvement or
new CPython ratio is inferred. The prepared allocation job has not run.

**Refine**, without calling this a failed design or pooling away uncertainty. Replace
the extra counting scan with the simple conservative bound that each non-inline
view consumes at least9remaining decoded UTF8 bytes. This removes work and its scan
state, at the cost of more unused admitted header headroom. Reads advance
monotonically; sized reads keep separate funding. No scalar credits/countdown or
identity tracking. Changed source requires its own checks and bounded decision.
Original moving/adverse controls remain here; no fixed percentage veto.

Only canonically verified prior8f VM aliases and redundant B671/B675 observation
archives were retired under fresh idle lease/path/inode/UID/hash checks. Their
canonical exact bits, observations and full qualification remain. Current91 and
all adverse evidence survive; no OS/evidence floor was lowered.

[All medians/control detail](summary.json),[raw evidence](observations.tgz),
[exact sources/bits/checks](source-checks.tar.xz).

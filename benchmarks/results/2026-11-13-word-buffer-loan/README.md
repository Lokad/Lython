# Owned word-buffer/cursor consolidation

2026-11-13. Candidate frozen9d157bb2,576production source inputs, against fully
qualified23a65e9e/dbcd4051/frozenbff487b5. The existing one-idle word-range cache now
owns its array and private cased-word cursor state in one loan. Per-line cursor
and temporary-reservation allocation disappear. No second pool, return dispatcher,
configuration, policy or tracing subsystem is added. Idle loans clear guest/source
references; active/nested/late users stay funded through cleanup. Plain larger
splits admit slightly more buffer metadata; tiny thresholds are not contractual.

Ordinary operand retirement now clears its owned slot before disposal, matching
native/async transfer. Reentrant refund can lend a returned buffer to a new user
and then fail. Retrying the old slot must not dispose that new loan. The new test
fails with the old retirement (the next borrower becomes exhausted), then passes
after exact source restoration and a forced nonincremental build.

## Allocation and source checks

A local Windows/.NET11RC3 diagnostic uses the existing complete scripts and file
host. Four-second warmups, five64-run blocks, two fresh processes per source in
reversed order. Full I/O/reset, stdout/file checking and result cleanup are included;
compile/startup/final cache teardown are excluded. Process-wide precise CLR allocation
counters do not force GC. All outputs and shared256MiB budget baselines pass.

| Script | Prior Lython bytes/run | Candidate bytes/run | Change |
| --- | ---: | ---: | ---: |
| Captured text summary |840888.625|546029.750–546037.125|−35.064..−35.065% |
| Unchanged file search |147378.875|147360.000–147370.875|−0.013..−0.005% (effectively flat) |

This establishes allocated-byte reduction, not exact live memory, CPU or embedding
tail latency. Sources, complete image hashes, counter observations and tool are retained.
Initial190internal/311public Release11 checks pass. After the retirement correction,
47focused lifetime/admission and758public cleanup/comprehension/call/text checks pass.
Actual.NET10 passes61internal/236public focused cases. No assertion or timeout is relaxed.
Full new-source Windows/Ubuntu, stress and installed-consumer qualification remains.

## Quiet-VM comparisons and decision

Both runs use identical fixed B685 workers and optional0.4 adapters, the same complete
captured-text and search scripts, and normal GC/tiering on the F4as_v7 VM. Warm timing
includes mediated RAM I/O, complete stdout/file checks and prior-result cleanup;
compile/startup/reset/protocol/final cache teardown are outside the guest clock.
Wider process CPU includes reset/protocol. These are diagnostic comparisons, not
isolated interpreter CPU or confidence intervals. All identity/golden/quiet/join/
archive gates pass and original conditions remain available.

| Collection | Conditions / complete duration | Captured-text elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| B690 original |Six roles including two CPython processes; text12s/search5s warmups, five500ms rotating rounds;146.078s|+1.65..+3.36%|+2.95..+3.18% |
| B691 same-image confirmation |Four Lython roles; both12s warmups, five1s rotating rounds;151.102s|−2.56..−5.53%|−1.72..−5.23% |

The original adverse run has8.67%candidate search-process spread. Its500ms CPU
batches also have coarse10ms counter resolution. The bounded confirmation changes
only measurement conditions: longer warmups/batches and Lython-only process roles,
under the same180s owner. No CPU result is inferred by comparing absolute times
across collections. B691 text process spreads are1.17%candidate/1.91%reference;
search stays inconclusive (elapsed−4.77..+4.76%, wider CPU−4.64..+4.08%).

Retain the coherent storage/lifetime change: allocation−35.1% is stable and the
confirmation supports modest text CPU direction. The exact CPU magnitude remains
diagnostic; preserve the opposite original result rather than declaring it explained
away. Search and embedding tail latency receive no improvement claim. Stop unchanged
retiming at this checkpoint; further measurement needs a new attribution/observability
reason. The broader whole-script objective remains open.

B690's simultaneous CPython3.13.16 text medians are0.907–0.932ms versus candidate
1.472–1.478ms (1.58–1.63×); these belong to that adverse collection. B691 makes no
new CPython measurement. Do not combine its times with earlier CPython clocks or
multiply these internal gains into the representative comparison table.

All raw streams and full collector sources are retained. Only byte-proved historical
VM working copies were retired after their canonical Git archives were verified;
current reference images and original/adverse evidence remain. Publication is held.

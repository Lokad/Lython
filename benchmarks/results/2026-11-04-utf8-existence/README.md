# Native UTF8 string existence and text EOF

2026-11-04. Use the existing logical byte length for PyString truth and text-file
EOF/iteration checks. Count readlines characters only when a positive hint uses
them. Strict valid UTF8 makes empty/nonempty independent of scalar count. Keep
Python scalar lengths, indexes and positive-hint behavior, admission, retained
file/result funding and cancellation. No new flag, cache, opcode, allocation,
backend or option. Two production files; two net lines deleted.

Baseline qualified97bc43b2/frozen0d452f95; candidate97bc43b2 plus archived patch,
frozen7cf2623c. [Exact identities](candidate-identity.json) and source snapshot
separate measured precommit bits from subsequent commit metadata.

| Complete workload / phase | Candidate elapsed | Change vs both baseline roles | Same-source spread | Candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| ASCII chunk scan / warm;1,048,576 B |0.2608ms|−51.27% /−49.36%|3.76%|4.65%|
| Unicode/NUL chunk scan / warm;1,052,672 B |0.3010ms|−50.68% /−52.71%|4.29%|1.35%|
| ASCII line filter / warm;1,048,576 B |0.5877ms|−33.73% /−32.51%|1.81%|0.69%|
| Config edit / fresh;1,024 paths |0.2991ms|+1.82% /−0.60%|2.44%|0.54%|
| Existing text helpers / warm;3,072 lines |3.1020ms|+1.41% /+0.93%|0.48%|0.67%|
| Chunk script / compilation |28.785µs|−0.04% /−0.10%|0.06%|1.54%|

Negative means less elapsed. Process allocation is essentially unchanged. Retain
the deletion and substantial complete-scan improvements. Config is control-limited;
compile near unchanged. Existing small text helpers move adversely0.9–1.4%, near
one role's1.38%IQR; preserve this uncertain small regression without another
retiming. Accept it at this bounded milestone alongside the affected scan gains.
No general CPU/RSS/embedding-latency gain or updated CPython ratio is claimed.
[All metrics](assessment.json).

Same quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering, three isolated
images and identical auxiliary binaries/CoreLib/hash witnesses. Existing driver:
6sec guest warmup/image, alternating order,2sec settle,9rotated200ms samples sized
from warmed baseline only. Complete invocations include actual mediated RAM-file
I/O, stdout/file validation and result cleanup. Exclude fixture reset/staging,
startup, hashing and final cached teardown; compilation is separate. Both1MiB
scripts open/read/close real files and check every EOF; they search chunks/lines
for markers. No repeatedly tested cached-string microbenchmark. Trusted independent
CPython3.13.16 full-output goldens, owner baseline/final-zero and fixture checks pass.
The213-second owned collection ends normally below240s, both quiet gates pass,
service inactive/MainPID0 and global lease free. [Original plan](pre-measurement-plan.md),
[all observations](observations.tgz), SHA256
`22f373b1a283e91c979ebaeb77c4425907871878b71b6727633b6a63f721d279`.

Before any timed job, setup failed on a CRLF shell file, Windows stdout goldens,
and a CR in a piped startup command. Preserve all three failures. Correct only
launch/newline handling; no successful source-pair resampling. The initial untimed
count/startswith script variants demanded scalar lengths themselves; their
source-gate correction is also preserved, never presented as timing evidence.

Release full-solution nonincremental build has zero warnings/errors and matching
core/public-test/benchmark images.49 internal/241 public tests per actual11/10
pass, unchanged tests. [Scope](validation.json), [sources, driver and initial/final
logs](source-and-validation.tgz). Full platform/Stress/installed-consumer checks are
pending the next coherent runtime delivery checkpoint; D5's green qualification
does not qualify this new patch. Publication remains postponed.

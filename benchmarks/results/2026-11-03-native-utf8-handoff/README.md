# Native producer UTF-8 handoff — B158

2026-11-03. Retained in **b3f1e9c3**. Native JSON/CSV text-file output copies
the producer's admitted UTF-8 range straight into admitted file staging. The
intermediate Python string and payload copy disappear. JSON also consumes its
existing machine within one guarded native delivery, rather than publishing and
reacquiring an iterator for every chunk. These effects are measured together.
There is one encoder and one file codec/newline/BOM implementation. Binary files,
custom sinks, buffered CSV history and publicly yielded chunks keep ordinary
independent values. No new public API, ownership subsystem or dispatch framework.

The producer retains its funded range until the synchronous staging copy ends.
Actual host publication and disposal still await flush/close. Ordinary writes
retain codec-error input identity; unpublished native data materializes an admitted
error input only when needed. A consumer write error leaves an escaped encoder
resumable; encoder failure still finishes it. The advance guard remains active
through callbacks and native copying. Cancellation and terminal refusal keep their
existing guards. This changes neither the I/O boundary nor the host capability.

## Complete scripts

Same quiet Azure F4as_v7, Ubuntu24.04.4, .NET11 RC3, CPython3.13.16 and ordinary
GC/tiering as B157. Both engines use actual file APIs with `TMPDIR=/dev/shm`.
Protocol2 excludes fixture reset from both execution clocks; execution includes
invocation, guest I/O, full output checks and prior-result cleanup. Compile-run
also includes compilation and its per-invocation disposal. Startup, transport,
last-result/cached-owner/process/fixture teardown are outside the batch clocks;
all users are joined afterward. This does not establish ordinary-disk performance.

One frozen pair plus identical-binary control; six jobs, six-second warmups,
nine rotated approximately200ms batches, one-second settling, reversed setup for
text/report. Config includes CPython in the same comparison. Collection takes
174.182 seconds. Every response verifies complete output/files and source hashes.

| Complete job | Earlier ms | New ms | Elapsed Δ / control | Process CPU Δ / control |
| --- | ---: | ---: | ---: | ---: |
| Config warm |0.257278|0.206733|−19.65 /+0.10%|−17.56 /−0.90%|
| Config compile-run |0.346292|0.289951|−16.27 /+0.55%|−15.08 /+0.21%|
| CSV rewrite warm |3.887657|3.870899|−0.43 /+0.63%|−2.11 /−0.72%|
| CSV rewrite compile-run |3.979579|3.960433|−0.48 /+2.57%|−1.05 /+2.00%|
| Text warm |3.525804|3.435966|−2.55 /−3.50%|−0.76 /−1.82%|
| Report warm |6.098381|5.767796|−5.42 /−6.59%|−5.56 /−6.88%|

Config new IQR0.565/0.862%; unchanged control0.857/0.314%. This supports config
elapsed/CPU improvement. CSV elapsed is effectively flat; do not claim a precise
CPU gain from its small differences. Text/report drift prevents attribution:
new times are about1.0/1.3% adverse against the later control despite being faster
than the first baseline. Both observations remain visible. The shared ordinary
text-write implementation also changes, so these are controls rather than a proof
of zero impact. No material regression is established; no unchanged replay is
needed to retain the demonstrated config/storage simplification. Process CPU
includes reset, runtime/JIT and response work, not guest-only CPU.

| Config phase | New Lython ms | CPython ms | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm |0.206733|0.181170|1.141×|
| Compile-run |0.289951|0.250962|1.155×|

The input is7,177B. These current config points complement the older nine-script
ordinary-filesystem table; they do not replace it or establish a generic multiplier.
CSV rewrite reads57,361B and checks all61,458 output bytes across4097 records.
No external CSV timing ratio was collected.

## Allocation and ownership

Reuse the existing B154 complete-job helper unchanged:512 warmups per image,
32-invocation batches, nine rotated rounds, ordinary GC/tiering, shared CoreLib,
hash witness and three isolated images. Its legacy purpose label is retained;
actual catalogue and binary hashes identify this experiment. It measures complete
Run/reset/I/O/checks/result cleanup; compile/startup excluded. Its elapsed is
diagnostic, separate from the worker comparison above.

| Complete job | Allocation Δ | Unchanged control |
| --- | ---: | ---: |
| Large config |−37.786%|0.000%|
| Small config |−9.289%|−1.909%|
| CSV rewrite |−10.233%|−0.022%|
| Report |−0.0003%|−0.0003%|

Small-config control drift limits the exact magnitude: new45,625B versus
first50,297B and later49,337B per invocation, about7.5% below the later control.
Do not claim a precise9.3% effect. All image/hash/golden/balance/cleanup assertions
pass. This is an allocation reduction, not a CLR collection or tail-latency claim.

| Accounted execution / root peak | Earlier B | New B |
| --- | ---: | ---: |
| Large config |389,897 /463,145|254,327 /327,575|
| Small config |15,229 /88,477|14,681 /87,929|
| CSV rewrite |5,944,403 /6,012,627|5,358,537 /5,426,761|
| Report |4,556,536 /4,638,968|4,556,536 /4,638,968|

These are conservative admission peaks, not physical RSS. Retained results,
aliases and pending users remain funded; allowance returns without CLR collection.

## Source and qualification

Baseline **ca4822f4b9493abaff64944abb832b256c6ebb50** was nonincrementally built
and frozen before mutation: core SHA256
`11e95e80b6b16b8877ec04d45c5b93df133fb2b10a3b0dcf08520d5994754126`.
Candidate core SHA256
`5b4c4e6e2411c94eb8b42cb612996a419490b336830b6c7169ebafdd713868a5`.
Six production source hashes and the patch are retained. They match the committed
runtime source; later rebuild commit metadata can change binary hashes. Worker,
unused Windows apphost and unchanged adapter bits are normalized from baseline;
only core DLL/PDB differ. Every frozen build/test process was joined first.

Focused actual .NET11/.NET10:315 internal and1303 affected public cases each;
final68 public cases each after removing the redundant CSV native-text branch.
New independent CPython3.13.2 controls cover codec error input/positions,
Unicode scalar counts, CRLF/BOM and delayed cleanup/budget reuse. Initial Windows
CPython expectations used implicit newline translation; the preserved failed
condition was corrected to explicit `newline=''` for portable error-input goldens.
The encoder-continuation regression first exposed the class-method global-fact
bug, fixed separately inca4822f4 with214 focused public cases per target.

Full Windows/Ubuntu .NET11/.NET10, original Stress and installed Release consumers
are **complete/GREEN** in [41625654805](https://github.com/Lokad/Lython/actions/runs/41625654805)
for exactb3f1e9c3. Every required step in both jobs is verified in the
[cached receipt](delivery-ci-status.json). B157 is independently qualified;
publication remains postponed.

The owned VM service exited0 in216.279 seconds. The outer PowerShell text pipe
then fed a trailing carriage-return line to bash and returned127; retain
[that controller output](vm.log). The [unit journal](unit-journal.log), complete
timing record, allocation gates and worker cleanup verify successful actual work.
The unit is inactive/dead and the shared lease was independently acquired afterward.
No workload was retried to conceal the wrapper error or adverse measurements.

[All observations](observations.tar.gz), [timing assessment](assessment.json),
[allocation assessment](allocation-assessment.json), [peaks](accounted-peaks.json),
[identity](identity.json), [source patch](source.patch.gz),
[focused logs](focused-logs.tar.gz), [source gate](source-gate.md).

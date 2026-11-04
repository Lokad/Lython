# Managed bulk newline search

2026-11-04. Replace the shared per-byte newline/mode loop with managed Span
IndexOf, IndexOfAny or CRLF sequence search. Choose mode once per call. Preserve
byte offsets, LF/CR/CRLF/universal behavior, trailing terminators, caller holdback/
window logic, limited scalar reads, cancellation and funding. No new allocation,
owner, cache, counter, representation, backend or unsafe code. One production
helper, **17 net lines deleted**. No public API or supported behavior change.

The [current CPU gate](../2026-11-04-builtin-method-source-gate/README.md) has810
long-line/2,000CSV guest samples. ReadLine29.51% includes inlined copying/admission/
scanning, not all attributed to this loop. Source establishes real scalar work;
these timings do not claim recovery of the entire profile share.

Baseline qualifieda00fe76d/frozen1dcca6f2, terminal41874093956 all eight gates green;
candidate1acf45de docs/evidence source plus archived helper patch,frozen5b36b41d.
[Exact bits/source](candidate-identity.json) distinguish measured precommit metadata.
Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering, identical auxiliary
bits/CoreLib/hash witnesses, three isolated images including two old controls.

| Complete workload / phase | Candidate elapsed | Change vs both baseline roles | Same-source spread | Candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| ASCII prefix/count / warm;262,144B |0.1922ms|−24.16% /−25.78%|2.18%|0.73%|
| Unicode prefix/count / warm;263,168B |0.1945ms|−27.03% /−23.01%|5.22%|0.66%|
| CSV helpers / warm;4,096 records |5.0609ms|+2.82% /+1.16%|1.65%|0.34%|
| Text helpers / warm;3,072 lines |3.0864ms|−3.49% /+0.88%|4.34%|0.20%|
| CSV helpers / compilation |80.480µs|−1.46% /−1.34%|0.12%|0.34%|

Negative means less elapsed; allocation essentially unchanged. Retain17-line
simplification and substantial complete long-line gains. Unicode magnitude is
control-limited; text straddles shifted controls and is inconclusive. Accept
possibleCSVwarm+1.2..2.8% regression debt explicitly at this bounded change;
compilation's implementation is unchanged, so do not attribute its elapsed shift
to a compiler optimization. No further retiming selected. Preserve all adverse
values; do not add preceding source-pair percentages or claim a broad CPU gain.
[All metrics](assessment.json).

Existing driver only:6sec guest warmup/image, alternating preparation,2sec settle,
9rotated200ms samples calibrated from warmed baseline.155s collection ends normally
below240s. Actual mediated RAM-file I/O, full output/file checks and result cleanup
included; reset/staging/startup/hash/final cache teardown excluded. Compilation
separate. All exact images, independent CPython3.13.16 complete goldens, per-job
owner baselines/final zero, fixture cleanup and quiet gates pass. All users joined,
service inactive/MainPID0, global lease free. [Plan](pre-measurement-plan.md),
[all observations](observations.tgz), SHA256
`93ef0bd86dc28c33bece5e60483f9d268370f735e50bdb6fdb13641f8a88b14f`.

Release nonincremental solution build:zero warnings/errors, matching core/test/
benchmark images. Existing45 internal/193 public checks per actual11/10 pass,
covering shared scanner modes/byte positions, text/gzip/ZIP reads, window/EOF,
suspended callbacks, cancellation/refusal and retained file/result owners. No test
changes. [Scope](validation.json), [sources/driver/full logs](source-and-validation.tgz).
New helper's full Windows/Ubuntu/original Stress/installed-consumer qualification
is pending the next coherent runtime checkpoint. The earlier greena00 revision
does not qualify this new patch. Current CPython table intentionally excludes it.
Publication/tagging remains postponed.

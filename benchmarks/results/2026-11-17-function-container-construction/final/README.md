# Corrected function/container construction

Measured production4b975a44, core5d9633a8, versus qualified0d2a6ab7/core8d26963c.
Quiet VM B923: four jobs155.09s, normal tiering/GC, five rotated rounds and two
processes per source. Complete JSON-record transform,4,096records/110,592bytes:
warm elapsed15.16–21.64%lower/CPU14.68–20.54%lower; fresh compile-to-result
elapsed15.65–17.92%lower/CPU15.74–18.35%lower. Warm same-source spreads4.22/3.88%
limit precision; fresh0.92/1.82%. Allocation39.54%lower,4,806,520→2,905,888B/run;
accounted peak6,576,643→6,576,387B, all six allocation/budget-reuse checks pass.

Floating warm elapsed−13.37..+0.03%/CPU−14.44..+2.01% with5.72/9.22%source-control
spread; fresh−4.20..+0.93%/CPU−2.91..+2.35%with4.92/0.41%spread. This remains
uncertain; no recovery or general speedup claim. Earlier8e738bd7 adversity and
29964188 refinement remain separately recorded in the parent and ../refined.
Retain the coherent deletion/record improvement with this explicit uncertainty;
do not retime unchanged images as an open campaign. CPython's named0d checkpoint
remains separate; no new CPython ratio is inferred here.

The final periodic cancellation check covers larger bound callback storage,
including a2,049field regression. Explicit nonincremental builds and11affected
public/script cases eachactual11/10 pass. Six exact-worker goldens pass. The
unchanged reference preflight/allocation evidence is reused only after all32image
hashes/catalog compare; see reference-reuse and its procedure. Timed auxiliary
worker/adapter files remain the qualified reference; only the core is replaced.
Sources/identity identify this explicitly; full current workers are qualified
separately. Discovery counts are diagnostic and are not substituted for runtime
test totals. Full delivery44187816311 remains independently pending at this writing.

Warm includes execution, mediated RAM I/O, goldens/prior-result cleanup; fresh
adds compilation/compiled-owner disposal. Startup/protocol/reset/final cache
teardown excluded, process CPU wider. Same stock toolchain/machine/boundaries as
the parent. All workers joined, quiet gates passed, RAM traces archived and
SHA-verified under the lease before unmount. First staging floor refusal launched
no work; extra committed duplicate-catalog proof allowed staging without lowering
254/252MiB guards. Different/unmatched old evidence remains untouched.

[Summary](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[identity](identity.json),[sources](candidate-sources.tar.xz),[procedures/checks](procedures-and-checks.tar.xz).
No publication; the broader performance roadmap continues.

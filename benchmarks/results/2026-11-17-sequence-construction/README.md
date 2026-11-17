# Shared sequence construction and native generator consumption

R460/B866, completed2026-11-17UTC; started2026-11-16UTC. Qualified9e8d1cf5/e92d9598
versus retained private82e45812 plus archivedaaef443f, now promoted with matching
production text. [Full delivery qualification passes](delivery/README.md); publication remains held.

One existing PyList builder now supplies iterable tuple construction. Exact-sized
backing transfers its funding; a trim copy admits full overlap. Async tuple staging,
the separate array-growth/per-item scalar-reservation loop and duplicate append
loops are removed. Native consumers share the existing generator engine and only
buffer into admitted capacity under the same execution authority. Final Add ends
the borrow before growth; live list mutation, exception state and awaited cleanup
are preserved. No arena, ledger, policy, universal value layout or new interpreter.

| Complete workload | Boundary | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Normalization,4,096lines/52,224B | Warm Run | −12.47..−8.98% | −11.26..−5.92% |
| JSON-to-tuple-to-Markdown report | Warm Run | −5.32..−1.99% | −6.80..−2.78% |
| Same tuple report | Compile and Run once | −4.84..−4.67% | −4.85..−2.69% |
| Generator/list/render,16,384values | Warm Run | −15.72..−5.96% | −13.19..−8.05% |
| Same generator/list script | Compile and Run once | −9.28..−5.19% | −12.67..−6.22% |
| Numeric report control,4,096records | Warm Run | −0.81..+6.65% | −2.79..+6.90% |
| CSV invoice control,4,096rows | Warm Run | −0.53..+1.63% | −0.87..+0.88% |

All four pairings of two process medians/source retained; negative means faster.
Normalization/report/generator outcomes favor the candidate with supporting CPU.
Numeric reference/candidate movement4.96/2.44%limits attribution; do not claim
absence of regression. Generator warm controls move5.53/5.73%, so precise magnitude
is uncertain. CSVroughly flat. Retain the coherent storage/engine simplification
and affected-script gains with that explicit uncertainty; no further unchanged
retiming. Earlier partial shapes and adverse controls remain [preserved](../2026-11-16-generator-batches/README.md).

Allocation is essentially flat (largest absolute change<0.03%). Conservative
per-capacity scalar headroom raises the tuple-report accounted peak3,577,427→
4,000,049B (11.81%). Larger scalar estimates remain funded. Some supplied allowances
can refuse earlier; no default cap, new configuration or exact tiny-budget contract.
Private builder metadata, retained capacity and replacement overlap stay admitted;
cleanup restores the shared allowance independently of physical CLR collection.

The report variant materializes tuple(render(record) for record in records) before
writing its existing header/lines. Input JSON, renderer, stdout and file goldens
are reused and checked with CPython. This is an ordinary additional script shape,
not a causal comparison to the old script's streaming error order. B866 requests
warm/compile-run, which invoke **Run**. The case name ending.async and runAsync
catalog metadata did not change that worker lane. Initial async timing labels
were corrected before commitment; allocation diagnostics explicitly call both
APIs. [The actual warm-async supplement is complete](async/README.md): report and generator
medians favor the candidate, with substantial moving-control uncertainty; CSV is
0.1–3.6% adverse. No new worker protocol/framework or async compile-to-result claim.

Quiet AzureF4as_v7 Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3,
normal tiering/natural workstation GC. Seven jobs214.280s;4s full-request warmup,
five rotated~0.5s samples/process. Warm includes Run, mediated RAM I/O, full stdout/
file goldens and prior-result cleanup; excludes compile/startup/reset/protocol/
final-cache teardown. Fresh includes compilation and compiled-owner disposal;
CPU clocks are wider. All identity/golden/quiet/Closed/exit/join/archive gates pass.
Evidence SHA-verified persistently under lease before RAM teardown; no users remain.
Terminal cached permanently. Only an exact committed superseded4fc worker core
was retired after fresh ownership/path/hash/all-inode-alias proof; selected images,
toolchains/canonical evidence and254/252MiB staging/final floors preserved.
No new CPython timing/RSS/embedding-tail claim.

Actual Windows.NET11/10 each277public/172internal focused cases pass before and
after promotion; full nonincremental Release solution builds with zero warnings/
errors. Nine frozen-worker/CPython goldens pass. New cases cover aliases, late errors,
exact/grown/trimmed storage, live list backing mutation and actual delayed I/O.
Final-array refusal occurs before allocation while another owner's funding remains
intact. A foreign generator previously ran a visible after effect despite consumer
cancellation; the same-authority eligibility guard now preserves per-pull checks
and leaves that independent source usable. Both original failing rows are retained.

Two exact-charge assertions were updated to require the old payload floor, bounded
linear headroom, exact balances and aliases; tight positive factory tests/refusal
checks remain. No timeout or containment assertion relaxed. Original charge failures,
new test compile corrections and constructor-overload ambiguity are archived.

[Results](summary.json),[allocation](allocation-summary.json),[decision/checks](validation.json),
[frozen inputs/images](identity.json),[source](candidate-sources.tar.xz),
[exact core](measured-core.tar.xz),[raw evidence](observations.tgz),
[all focused logs/failures](focused-checks.tgz),[procedures/custody](procedures.tgz).

[Delivery accounting corrections](accounting-checks/README.md) preserve the original
two CI failures and strengthen ownership/cleanup checks without changing runtime.

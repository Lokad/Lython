# Targeted agent-workload comparison

R130/B490, 2026-11-09: committed459813a3/core1be2 versus CPython3.13.16 on quiet
Azure F4as_v7/Ubuntu24.04.4/.NET11RC3. Nine jobs193.226s. Three independent processes
per job (CPython/two Lython controls), five-second warmup/role, seven rotated150ms
rounds, normal GC/tiering. Reuses existing scripts/catalog goldens/fixtures/workers;
current script source hashes match. No unchanged main-five-case replay or new harness.

| Script | Input | Compile-to-result Lython / CPython | Warm ratio | Compile ratio |
| --- | --- | ---: | ---: | ---: |
| Config edit |181B, one JSON file |1.09–1.10× (Variable) |1.26–1.27× |1.08–1.09× |
| Protected file search |1,025lines,42,010B |1.40–1.41× |1.69–1.71× |0.96–0.97× |
| Copy/checksum/manifest |twofiles,4,107B |1.47–1.48× |1.56–1.59× |1.25–1.27× |

Ratios span duplicate Lython process medians / CPython median, not confidence
intervals. Config compile-to-result controls0.54%, max IQR3.95%: variable elapsed
remains visible. Other rows' control/IQR diagnostics are in the summary, not proof
of universal performance. Prior726 measurements are retained; cross-checkpoint
ratios cannot attribute individual changes. Main five CSV/text/JSON/report rows keep
their explicit6f76 source in the maintained guide.

Compile-only includes code construction/prior-script disposal. Warm excludes compile;
compile-to-result includes it. Guest host-mediated RAM-file I/O, full output/file
validation and prior-result cleanup are timed. Startup, fixture staging/reset,
protocol and final cache teardown are excluded. CPython retains imported modules;
Lython isolates run imports. These are embedding comparisons, not isolated CPU
kernels. Two small manifest files do not establish large-tree throughput.

Source459 has42internal/529public focused Release cases peractual11/10 and the
separate R129 controlled source comparison. Full Windows/Ubuntu/actual11+10/stress/
installed-core qualification remains independently pending; prior CI cannot qualify
later code. No backend/producer qualification or publication follows from this run.

All identities/core/auxiliary hashes, independent goldens, budget balances,
quiet-before/after, Closed, joined users, RAM removal, archive-under-lease and local
SHA verification pass. No disk recovery required; terminal cached, no remaining
users/owned fixtures. The next source gate is bounded current warm execution/I/O
attribution: search's runtime gap despite competitive compilation is evidence for
investigation, not proof of a removable dispatch cost.

[Source identity/inventory](identity.json), [summary with absolute times/controls](summary.json),
[receipt](archive-receipt.json), [raw evidence](observations.tgz),
[original guide](prior-BENCHMARKS.md), [selection](selection.md), [preparation/analysis](preparation.tar.xz).

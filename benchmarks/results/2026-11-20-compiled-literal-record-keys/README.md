# B1249 and B1250 compilation-owned dictionary keys

Both campaigns compare `c62457d49` with frozen prototype `6c964a71b`
on the Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on
November 20, 2026 UTC. The change is retained at `2ef0b2f44` alongside
the independent regex correction.

For displays with distinct literal string keys, compilation prepares the
immutable key table from the existing constant pool. The runtime drops
the key tuple, lookup-cache slot and first-use table construction for
these displays. It fills the final private values array from evaluated
operands. Dynamic and duplicate keys still use general display semantics.

| Campaign and workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| B1249 JSON normalization, 3,072 records | Warm | −4.20 to −2.29% | −3.79 to −2.99% |
| B1249 guarded manifest | Warm | −1.10 to −0.09% | −1.31 to −0.38% |
| B1249 guarded manifest | Compile-to-result | −0.42 to +0.84% | −0.29 to +1.25% |
| B1249 validator, 96 fields | Compile-to-result control | +0.78 to +1.37% | +0.06 to +1.28% |
| B1250 validator, 96 fields | Compile-to-result control | −1.68 to +2.53% | −1.05 to +1.12% |
| B1250 JSON normalization, 3,072 records | Warm | −4.31 to −3.62% | −3.77 to −2.96% |

The ranges compare each of two candidate worker medians with each of
two reference worker medians. They show process variation, with seven
batches per worker. B1250 reverses the worker and lane order and uses
longer warm-up and batches on the same images. It confirms the warm JSON
gain, which includes reading and writing 189,441 bytes each. The initial
validator slowdown remains unresolved because the repeat spans zero
and its candidate control spread is 2.45%. Manifest warm changes are
small relative to worker variation and compile-to-result spans zero.

Warm includes guest execution, mediated RAM-backed I/O, output checks
and prior-result cleanup. Compile-to-result adds script compilation
and code-owner disposal. Process startup and final teardown are outside
the guest clocks. These comparisons use CPython output goldens. They
provide no new CPython timing or isolated compilation timing.

Local managed allocation falls 0.006% for JSON and 1.14% for the manifest.
Accounted peaks fall 608 and 2,176 bytes respectively. The validator
warm allocation and peak match. Local median compile allocation rises
392 and 1,424 bytes respectively, and the validator median stays flat.
The borrowed allowance returns to baseline after each measured cleanup.

Both 32-file worker images verify against the three complete-script
goldens. The exact prototype passes 73 white-box and 1,192 public
focused Release checks on Windows .NET 11 and Ubuntu .NET 10.
Both VM runs pass their quiet checks. All workers join and the raw
archives verify under the lease before guest fixture removal.

[Initial summary](summary.json) ·
[Initial observations](observations.tgz) ·
[Repeat summary](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tgz) ·
[Allocation diagnostic](allocation-summary.json) ·
[Source delta, identity, verification and procedures](checks-and-procedures.tar.xz) ·
[Repeat procedures](confirmation/checks-and-procedures.tar.xz).

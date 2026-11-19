# B1017 postfix syntax storage

Measured `6c1da75b7` / core `c9969271` against prototype `6de94be12` /
core `ff1b1895` on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3 on
2026-11-19 UTC. The retained implementation is `c6d4683e5`.
Both complete 32-file images and the catalog verified. Five jobs took
232.14 seconds, with two processes per source and five interleaved
rounds. Both quiet gates and all 100 timed responses passed. Workers
joined and the archive was verified before cleanup.

| Workload and phase | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Configuration manifest, compilation | −3.95% to −2.11% | −4.31% to −3.22% |
| Generated configuration validator, compilation | −2.97% to −2.33% | −3.53% to −0.67% |
| Configuration manifest, compile-to-result | −2.96% to −0.32% | −2.34% to +0.23% |
| Expanded forwarding, warm | −2.90% to +1.26% | −3.33% to +1.01% |
| Positional calls, warm | −0.47% to +0.16% | −2.43% to −0.87% |

Postfix parsing now uses direct parser operations. Empty calls share empty
storage. Common calls own their completed argument list, retaining its
spare capacity in exchange for eliminating the separately ordered array
and filtered enumerations. A starred operand after a keyword uses one
stable partition, with cancellation checks during the copy. Class headers
consume the same arguments directly. Simple indexes and slices skip
temporary tuple-item construction. Managed nesting checks remain in the
postfix chain and nested operand parsers.

The Windows first-compile allocation diagnostic falls 7.40% for the
generated validator and 0.86% for the manifest. Accounted execution peaks
and compiled reservations agree, and budget returns to baseline. Both
compilation elapsed ranges favor the candidate with lower CPU. Manifest
compile-to-result CPU and both warm elapsed controls span zero. The
positional CPU control moves despite comparable elapsed.

Windows .NET 11 and WSL .NET 10 each passed 32 white-box and 2,704 public
focused Release cases. Forty probes matched through both APIs, including
diagnostic order and spans, positional-before-keyword evaluation,
grouping, class headers and slice keys. The initial isolated WSL run
lacked the probe and fixture layout. The archive preserves that run and
the passing rerun with the matching build and checkout fixtures.
The retained source also passed the full platform, stress and package
workflow. [Delivery](delivery/README.md).

Compilation includes compile-owner disposal. Compile-to-result includes
mediated I/O, output verification, compilation and result/owner cleanup.
Startup, fixture reset, request framing and final cache teardown are
outside guest elapsed. Process CPU has a wider boundary.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)

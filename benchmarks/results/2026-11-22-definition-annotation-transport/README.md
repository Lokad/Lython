# B1413 Carry only the evaluated annotation value through construction

Compared `55424591f` with private `dc4cfd429` on November 22, 2026 UTC
on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The
same four-file patch at `3502f0a11` received the focused checks on the
newer delivery tree. Only the core DLL differs between the measured
32-file images. The reference is the later image in [B1412](../2026-11-22-factory-checkpoint-source-pair/README.md).

The prototype keeps the eager read plan and immutable postponed text on
the compiled function with its annotation keys. Function construction
passes the evaluated eager value by reference and reads the capacity and
repeated-read fact from the compiled metadata. This removes the tuple
carrying those facts through both execution paths. Publication and refund
still use the same preflight and the same annotation dictionary semantics.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −3.74 to +1.86% | −2.76 to +2.28% | 5.34 / 0.45% |
| Postponed nested factories | Compile to result | −3.66 to +3.55% | −4.80 to +5.72% | 2.04 / 5.33% |
| Eager nested factories | Warm | +1.33 to +6.18% | +0.65 to +6.91% | 0.62 / 4.14% |
| Factories observing default tuples | Warm | +0.58 to +3.03% | −0.49 to +3.69% | 1.80 / 0.62% |
| Fully supplied helper | Warm | −1.26 to −0.45% | −2.07 to −0.39% | 0.03 / 0.79% |
| Quote-heavy CSV invoice | Warm | −2.54 to +0.19% | −1.54 to +0.38% | 1.12 / 1.66% |

Ranges use every pairing of two process medians per source. Each worker
warms for eight seconds and supplies seven rotated batches. Warm elapsed
includes mediated RAM-backed I/O where applicable, verification and
prior-result cleanup. Compile to result adds compilation and compiled-owner
cleanup. Wider CPU includes reset and request handling. Startup and final
teardown are outside these elapsed boundaries. The five CPython goldens
prove fixture parity. This is a Lython-only timing comparison.

Keep the prototype private. The postponed target spans zero at both
boundaries. The eager workload takes longer in every warm elapsed and
CPU pairing. The defaults observer also takes longer across all elapsed
pairings. The fully supplied helper improves slightly. Allocation and
admitted peak match on all five local diagnostics, with allowance reuse.
Fewer transported fields alone leave the warm factory regression open.

All 18 inspected code objects agree through the executable tape, scope,
and routing checks. Windows .NET 11 and Ubuntu .NET 10 each pass 1,290
focused Release checks, including annotation snapshots, closures, awaited
definitions, cancellation and admission refusal. All 168 timed batches
and both quiet gates pass. The workers join and the archive verifies
before the VM lease ends. The guest fixtures are removed.

[Sources and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).

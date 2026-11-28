# B1928 Share and defer primitive numeric call contracts

One numeric member table covers integer and boolean values, their
inferred types, and float values and inferred types. The change deletes
81 identical rows. Each remaining row keeps its accepted argument
shape, mutation fact, diagnostic code and message. The general member
catalog can initialize without constructing the numeric table.

The comparison uses the source at `56887480f` and its two-file candidate
patch. The 32-file images differ only in the core DLL. The contract
witness compares 57,408 receiver-and-member queries, 962 matching
contracts and 11 compiler diagnostic cases. All observations match.
Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 1,995 public and
437 white-box focused Release checks, including static diagnostics and
admission failure. Each image matches three independent CPython
3.13.16 output and file goldens.

Two candidate startup traces verify the dispatch. The configuration
edit invokes the general data factory, now 11,125 IL bytes. It skips
the numeric factory. The numeric methods script invokes the 1,629-byte
numeric factory and skips the general data factory. B1927 measured the
unsplit factory at 16,573 IL bytes.

## Startup and warm compilation

November 28, 2026 on Ubuntu WSL and .NET 11 RC3, pinned to one logical
CPU. The cold images live on the native persistent WSL filesystem.
The parent measures launch through child exit and pipe drain after
preparing the empty fixture directory. The child performs file setup,
verification, reply writing and owner cleanup inside that boundary. OS caches stay
warm. Twelve rounds rotate the two cases and the order of the two
images. All 48 timed launches verify. CPU sums child user and system
time. The paired ranges preserve all rounds.

| Fresh workload | Median elapsed, reference → candidate | Median CPU, reference → candidate | Paired CPU change |
| --- | ---: | ---: | ---: |
| Configuration edit | 259.33 → 258.62 ms | 222.71 → 220.34 ms | −8.60% to +6.31% |
| Numeric methods | 209.06 → 192.29 ms | 162.86 → 153.18 ms | −13.98% to −2.44% |

Elapsed spans both directions in both workloads. Numeric median CPU
falls 5.94%. Configuration startup remains inconclusive.

The warm run uses the existing two-workers-per-image schedule with
seven rotated batches per lane. Compile includes prior compiled-owner
disposal. Compile-to-result adds execution, golden checks and result
cleanup. Process CPU also covers framing and fixture reset where
applicable. All 84 batches verify, all workers join and the persistent
archive verifies after removing the fixtures.

| Warm workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | −0.70% to +5.24% | −0.38% to +2.88% |
| Numeric methods | Compile to result | −1.81% to +0.94% | −1.30% to +0.43% |
| Guarded report control | Compile | −2.65% to +0.72% | −5.22% to −0.40% |

Keep the smaller metadata set and the cold numeric CPU reduction.
Configuration compile has 3.07% same-image candidate CPU spread.
The guarded report has 4.40% reference CPU spread. The probes and
timings are local diagnostics. The initial summary script looked for
the two cold payloads inside the warm archive. Its correction checks
those payload hashes separately and reads the original warm timings.

[Cold timing](cold-summary.json) · [Warm timing](warm-summary.json) ·
[Trace summary](trace-summary.json) · [Image identity](identity.json) ·
[Warm observations](warm-observations.tar.xz) ·
[Cold observations, source, traces and tests](procedures.tar.xz).

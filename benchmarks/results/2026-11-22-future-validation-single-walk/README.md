# B1421 Future annotation validation in the existing syntax walk

The reverse syntax walk now checks placement of future imports and
assignment expressions in postponed annotations. A shallow module scan
provides the allowed prefix and the same postponed flag used by lexical
preparation. The separate statement walk, its permitted-import set and
the lexical flag rediscovery are gone. The annotation expression walk
keeps the original treatment of lambda defaults and bodies.

Compared `6ac96ea93` and private `fd7a59409` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3 on November 22, 2026 UTC. The measured
source patch is retained as `d22b1fb07`. Other delivery changes are
outside the pair. The 32-file images differ only in the core library.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Postponed nested factory | Compile only | −3.13 to −1.72% | −2.84 to −2.25% |
| Postponed nested factory | Compile to result | −4.28 to −1.47% | −3.16 to −2.73% |
| Eager nested factory | Compile only | −1.80 to +0.11% | −1.71 to +0.19% |
| Generated inherited dataclass configuration | Compile only | −0.12 to +0.70% | −1.75 to +0.80% |
| Sparse generated validator | Compile only | −1.57 to +0.34% | −2.91 to +2.07% |
| Generated read-only branches | Compile only | −1.27 to +2.61% | −1.29 to +1.82% |

Ranges cover all pairings of medians from two independent workers per
source. Workers warm for eight seconds and complete seven rotated
batches per lane. Compile includes owner disposal. Compile to result
adds execution, output verification and prior-result cleanup. Wider CPU
covers the worker request. Startup and final teardown are outside the
elapsed clocks. The generated branch control remains inconclusive with
3.60% candidate worker spread. No external timing ratio was refreshed.

Local Windows current-thread compile allocation drops from 65,568 to
64,120 bytes on the postponed factory, 2.21%. The other six allocations
move by at most 0.21% at the final sampling point. Compiled and transient
owners return their allowance. The 1,200-source matrix produces identical
ordered diagnostics, spans and retained balances. It combines placement,
parenthesized docstrings, nested suites, multiple diagnostics and lambda
defaults. Both images match seven complete CPython goldens. Windows
.NET 11 and Ubuntu .NET 10 each pass 99 white-box and 1,686 public
focused Release checks.

All 168 timed batches and both quiet gates pass. Workers join, guest
fixtures are removed and the raw archive verifies under the lease.

[Sources and images](source.json) · [Timings](summary.json) ·
[Compilation allocation](local-allocation.json) · [Diagnostics](diagnostic-comparison.json) ·
[Observations](observations.tgz) · [Checks and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).

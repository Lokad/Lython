# B1980 Defer private starmap generator frames

`starmap` can take the admitted argument slots for a same-command
generator call when the source yields. The shared executable prefix
transfers completed yields into the callback's funded slots. Dynamic
operations and excess yields transfer the live state to the existing
generator driver. The call retains its original admission point.

The 2,048-row iterable CSV script saves 835,480 CLR bytes in `Run`
and 835,592 in `RunAsync`, about 816 KiB per command. Both APIs keep
the same 4,304,384 B peak execution admission, 6,179 growth requests
and compiled charge. The other nine scripts differ by at most 402 CLR
bytes and keep the same admission. All allowances return to baseline,
and the retained-alias witness allocates the same bytes.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 760 public
and 53 white-box focused Release checks. The new regression checks
prefetched aliases, default arguments, a suspending read and an excess
yield. An existing white-box test now exercises the ordinary `starmap`
pull and confirms the source still yields a public generator on its
next direct pull. It retains the stack and allowance assertions.
The first white-box build found its stale internal entry point before
the test was updated. All ten CPython 3.13.16 output and file goldens
match for both images locally and on the VM.

## Quiet warm async comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
The images differ only in the core DLL. The reference uses the
`9d8d0629b` source with the exact B1978 core from `e78a95b1b`. The
candidate uses the archived production patch. Two workers per image
warm for at least five seconds and contribute seven rotated batches
each. Elapsed includes mediated RAM-backed I/O, verification and
previous-result disposal. Process CPU also includes fixture reset
and framing. Both clocks exclude compilation and process launch.
Ranges span pairings of worker medians.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Iterable CSV shipment | −7.11% to −6.82% | −7.45% to −5.84% |
| Ordinary tuple reduction control | −1.73% to +2.43% | −1.02% to +3.23% |
| File-fed map control | −4.97% to +4.28% | −4.13% to +2.82% |
| Explicit loop control | −4.02% to −0.05% | −2.37% to −0.86% |

Both quiet gates and all 112 timed batches verify. The workers join,
the RAM-backed fixtures are removed, and the persistent archive
verifies before the lease ends. The tuple and file-fed control ranges
span zero. The external CPython ratios remain at their measured
revision. [B1978](../2026-11-28-current-mixed-cpython/README.md).

[Timing summary](summary.json) · [Worker identities](identity.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Patch and checks](procedures.tar.xz).

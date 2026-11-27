# B1873 Write complete plain quoted JSON tokens together

The refinement keeps the bounded initial byte search and the single admitted
write for a complete plain token. The scalar escape loop resumes at the
first special byte and does no further byte searches. Long plain strings
continue with bounded cancellation checks. The first variant introduced
repeated searches after each special byte and used more CPU on the escaped
export. Its original results remain available. [B1872].

The frozen reference is `43ef47bba`. Only the core DLL differs between
the two 32-file images. Both match the same three independent CPython
3.13.2 output and file goldens as B1872. Both Windows Release targets and
Ubuntu WSL .NET 10 each pass 1,034 public and 232 white-box checks,
including strict UTF-8, DEL and cross-window rune handling, output refusal,
retained values, callback order and cleanup.

## Allocation and admission

Windows Release .NET 11, median CLR bytes per command. Every command returns
its allowance to the compilation baseline. All three complete-script peaks
match, and managed allocation differs by at most 112 B per invocation.
For a plain 96-byte root string, the complete quoted token saves 256 CLR
bytes and its peak falls from 2,372 to 2,276 B in both APIs. This confirms
the removed growth independently of whole-export timing.

## Local warm timing

Ubuntu WSL and .NET 11 RC3, two workers per image on one logical CPU. Each
worker warms for five seconds and contributes seven rotated batches. The
lanes run in reverse order relative to B1872. Intervals span all pairings
of worker medians. Elapsed includes mediated RAM-backed I/O, output and
file verification and previous-result disposal. Process CPU also covers
fixture reset and framing. Both clocks exclude compilation and startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Default export | Async | −7.86% to +2.19% | −9.82% to +3.57% |
| Escaped export | Async | +3.84% to +17.97% | −4.16% to +5.05% |
| Nested read control | Sync | +2.57% to +7.02% | −2.88% to +2.19% |
| Default export | Sync | −2.76% to +3.48% | −1.75% to +6.41% |

Withdraw the refinement. The plain root string keeps its storage savings.
The default export timing spans zero and loses the first variant's consistent
sync CPU decrease. Escaped CPU also spans zero, but elapsed rises in every
pairing while the nested read control moves in the same direction. The
comparison leaves the cause unresolved and gives no dependable whole-export
benefit. All 112 batches verify, workers join and the archive verifies.
The restored scalar emitter passes 67 focused Windows .NET 11 checks
after a nonincremental Release build.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch, allocation detail and checks](procedures.tar.xz).

[B1872]: ../2026-11-27-json-quoted-token-windows/README.md

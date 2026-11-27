# B1874 Give eager JSON serialization its own traversal

The eager JSON call owns its first frame inline. `dump` and `iterencode`
store the same traversal in their iterator across published chunks. They
all use one emit loop. Eager execution releases traversal scratch before
admitting the string result. The shared loop admits continuation storage
before invoking a callback that may suspend, including dynamic Boolean
options. It releases that allowance after the callback and its users finish.

The frozen reference is `43ef47bba`. Only the core DLL differs between the
32-file images. Both match four independent CPython 3.13.2 goldens, including
file bytes. Windows Release .NET 10 and .NET 11 and Ubuntu WSL .NET 10 pass
1,034 public and 234 white-box checks per target. The focused coverage
includes sorted storage release, cycle handling, cancellation during a
callback, refusal before callback invocation and budget reuse after cleanup.

## Allocation and admission

Windows Release .NET 11, median CLR bytes per warm command. Both 768-record
exports return to their compilation allowance after each command. The default
export allocates 122,874–122,936 fewer bytes (9.36%) with its peak admission
unchanged. The escaped export allocates 122,656–122,880 fewer bytes (6.46–6.47%)
and uses 103 fewer bytes of peak admission. Nested reading allocates the same
amount. The native file-dump control allocates 112 B more in sync and the same
amount in async. All commands return to their compilation allowance.

## Local warm timing

Ubuntu WSL and .NET 11 RC3, two workers per image on one logical CPU. Each
worker warms for five seconds and contributes seven rotated batches. Ranges
span all pairings of worker medians. Elapsed includes mediated RAM-backed
I/O, stdout and file verification and previous-result disposal. Process CPU
also covers fixture reset and framing. Both clocks exclude compilation and
startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Default export | Sync | −10.55% to +1.25% | −10.15% to −0.97% |
| Nested read control | Sync | +1.05% to +5.04% | +1.12% to +7.79% |
| Native sorted file dump | Async | −8.80% to +4.97% | −4.36% to +4.11% |
| Escaped export | Async | −9.09% to −0.48% | −12.39% to −3.52% |
| Default export | Async | −7.88% to −4.82% | −5.63% to −2.22% |

All 140 timing batches verify, workers join and the archive verifies. Keep
the shared traversal and the allocation reduction. The unchanged read
control shows drift. Preserve these timing ranges as diagnostics and repeat
the affected usage at a quiet delivery checkpoint.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch, allocation detail and checks](procedures.tar.xz).

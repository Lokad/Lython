# B1960 Keep the integer subtotal in the private generator operand

The existing operand owns the integer subtotal through the borrowed prefix.
An integer-only reduction can finish without a general accumulator or an
async state machine. Before a dynamic operation or overflow, the same
iterator and live slots transfer to the existing generator frame. The
general accumulator starts with the completed subtotal. The operand admits
its capacity with the other generator storage and keeps it funded through
any suspension and cleanup.

The Windows .NET 11 witness saves 163,840–164,014 CLR bytes and 2,048
allowance growth requests per 2,048-row tuple or range script in either API.
Compiled admission and execution peaks match. The two unchanged controls
keep their request counts, and all executions return allowance to baseline.
Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 415 focused
Release cases covering observable generator identity, numeric promotion,
suspended addition, cancellation, refusal and cleanup.
After combining the subtotal and B1959 binding changes, each of those
targets passes 871 focused Release checks. The procedures include the
combined source diff and logs.

## Matched warm comparison

November 28, 2026 in Ubuntu WSL on .NET 11 RC3, pinned to one logical CPU.
The independently measured candidate patch is based on `6ee258ff8`.
Its reference has the same executable source at `09cecc9ce`.
The comparison isolates subtotal ownership from B1959's binding change.
Both 32-file images verify four CPython 3.13.16 output and file goldens.
Only the core DLL differs.

Two workers per image warm for five seconds each and supply seven rotated
batches. Ranges pair all worker medians. Elapsed includes applicable
RAM-backed mediated I/O, verification and previous-result disposal.
Process CPU adds fixture reset and framing. The measurements exclude
compilation and startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Tuple generator reduction | `RunAsync` | −6.78% to −2.27% | −3.80% to −2.46% |
| Range generator reduction | `RunAsync` | −7.64% to −1.27% | −8.45% to −1.49% |
| File-fed map control | `RunAsync` | −1.14% to +1.81% | −0.69% to +0.39% |

All 84 batches and cleanup pass. Tuple CPU spread stays below 1.22% on
each image. Range CPU has 3.46% reference spread and 4.00% candidate spread.
Timing is local and diagnostic. The external comparison remains at the
earlier [B1958 revision](../2026-11-28-post-generator-cpython/README.md).

[Timing](summary.json) · [Allocation](allocation.json) · [Admission](admission.json) ·
[Images](identity.json) · [Observations](observations.tar.xz) ·
[Source and procedures](procedures.tar.xz).

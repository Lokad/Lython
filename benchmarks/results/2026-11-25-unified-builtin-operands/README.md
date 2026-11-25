# B1714 Bind short builtins directly from executable operands

The private candidate replaces the short intermediate argument packet
with the direct binder already used for wide builtin and JSON calls.
The one-positional-value JSON defaults still skip option binding. Both
hosting APIs retain one call entry through completion. Bindings needing
major storage reserve before allocation. Other dynamic callables and
prepared constructors keep their existing argument packets.

The reference is `60ecf0d3b`. The two 32-file images differ only in the
core DLL and verify six complete CPython 3.13.2 goldens. On November 25,
2026 UTC, the Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Two workers
per source warm for five full-request seconds and contribute seven
rotated batches each. Ranges span the four pairings of worker medians.
Elapsed includes applicable mediated RAM-backed I/O, output and file
verification, and result cleanup. Wider CPU also covers fixture reset
and request framing.

| Workload and warm phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Default JSON export | −0.88 to +2.71% | −1.67 to +0.21% | 2.30 / 1.29% |
| Three-argument tolerance report | +10.43 to +22.86% | +8.06 to +20.42% | 3.40 / 7.59% |
| Two-option JSON export | +0.59 to +2.83% | +0.88 to +3.57% | 0.78 / 1.43% |
| Two-option JSON export, async | +0.70 to +4.34% | 0.00 to +2.75% | 0.33 / 3.28% |
| Wide-option JSON export | −3.14 to −1.33% | −3.60 to −1.82% | 1.22 / 0.65% |
| Ordinary definitions | −1.61 to +2.59% | −2.89 to +1.67% | 2.50 / 1.72% |

The 4,096-call tolerance report allocates 131,072 B more per execution
in the local sync diagnostic, exactly 32 B per call. The 3,072-row
two-option export allocates 96,838 B more. The extra objects carry
fixed preflight reservations. Earlier admission now covers final
bound options even if the caller supplied three or fewer operands.
Keep the prototype private and compare the same six phases with that
reservation carried by the existing call frame.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,276 white-box and 2,828
public focused Release checks. The initial numeric-hook fixtures hit
binding admission before the parser boundary they target. The corrected
fixtures bind decoder options before the gate. A separate regression
shows that an unfunded short binding previously reached the callback
on both hosting APIs. Deferred JSON callbacks, cancellation, retained
value identity and allowance recovery also pass. All 168 timed batches
and both quiet gates pass. Workers join, guest fixtures are removed and
the archive verifies under the lease.

[Wide predecessor](../2026-11-21-direct-builtin-operands/README.md) ·
[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz).

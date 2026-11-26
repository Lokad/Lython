# B1759 Complete native field-conversion helpers directly

The compiler recognizes a four-instruction helper that calls a unary
builtin with a string-keyed dictionary field and immediately returns the
result. The runtime resolves the live callee first. It borrows established
field-cache storage from the definition and uses the existing builtin
entry. The first cache admission still executes the ordinary body with its
callee already captured. Dynamic callees, custom subscripts and missing
fields keep ordinary execution.

| Workload | Records | Native helper bodies, reference → candidate | Direct field returns |
| --- | ---: | ---: | ---: |
| File-fed integer quantity map | 2,048 | 2,048 → 1 | 2,047 |
| Float CSV meter report | 4,096 | 4,096 → 1 | 4,095 |
| Shipment callback control | 2,048 | 0 → 0 | 0 |

The separately instrumented counts match in both hosting modes. The
reference map already borrowed three iterator slots for its callback.
The removed work is dispatch of the helper body. The meter report uses
the existing explicit-float fixture and independent output and file
goldens. It reads dictionary rows and applies a small `energy(row)`
conversion helper. Both complete 32-file images match CPython on all
three workloads. Local Windows .NET 11 CLR allocation and peak shared
admission match exactly. All allowances return to baseline.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Float CSV meter report | Sync | −8.31 to −4.45% | −9.01 to −2.60% |
| Shipment callback control | Async | +0.99 to +2.36% | +0.13 to +1.96% |
| File-fed integer quantity map | Async | −13.68 to −11.14% | −12.36 to −9.06% |

The reference is `e554c19a5`, whose runtime matches `37df68bfe`. The
November 26, 2026 UTC block uses the quiet Azure F4as_v7 on Ubuntu 24.04.4
and .NET 11 RC3. Two workers per source warm for at least seven seconds
and supply seven rotated batches. Ranges span the four pairings of
worker medians. Warm execution includes mediated RAM-backed I/O,
verification and result disposal. Process CPU also covers fixture reset
and framing. All 84 batches complete with joined workers, both quiet
gates and a verified persistent archive.

Windows .NET 11 and Ubuntu .NET 10 each pass 182 white-box and 659 public
focused Release runs. The new checks cover callee rebinding during first
cache admission, terminal refusal, live field replacement, missing keys,
custom indexing and delayed host I/O. The initially rejected catalog and
the verified duplicate staging copies used to restore VM headroom are
recorded with the procedures.

Retain the path for the two affected scripts. The shipment control's
adverse movement remains open. Its scripts never use the new field return,
and the timings alone do not identify a cause.

[Timing](summary.json) · [Routes](routes.json) · [Allocation and admission](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and checks](procedures.tar.xz).

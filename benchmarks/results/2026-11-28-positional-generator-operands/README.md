# B1924 Bind complete positional generator calls in the operand entry

The candidate over `80b64ed6a` uses the original evaluated receiver and
operand slots for an exact generator call. It fills the existing admitted
argument workspace with the same positional copy used by the general
binder, then invokes the generator factory. The operand call keeps the
interpreter guard, translation and CLR async context. Calls requiring names,
defaults or expansions retain general binding. The generator still owns
its suspension, exception and cleanup state.

A focused scenario combines exact direct and saved receiver calls with
omitted defaults and named arguments. CPython 3.13.16 and both Lython APIs
agree on receiver and value identity after mutation. Focused Windows .NET
10 and 11 and Ubuntu WSL .NET 11 Release checks each pass 539 public and
112 white-box cases. Both 32-file images match four independent shipment
output and file goldens. All eight allocation witnesses restore allowance
and have identical admitted peaks and compiled reservations. CLR allocation
varies by at most 174 B per run.

## Local timing

November 28, 2026, Ubuntu 24.04 WSL and .NET 11 RC3, one pinned logical CPU.
Two workers per image each warm for at least five seconds and contribute
seven rotated batches per lane. Ranges span pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and request framing.
Compilation and startup are outside the clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | Async | −3.85% to −0.65% | −3.48% to −2.11% |
| File-fed map control | Async | −2.90% to +7.62% | −3.20% to +6.99% |
| Bound shipment | Async | −3.12% to +1.83% | −2.45% to +1.83% |
| Iterable shipment | Sync | −4.67% to −0.83% | −4.61% to −0.97% |
| Shipment with ordinary helper and generator reduction | Async | −2.42% to +4.11% | −1.50% to +4.21% |

The repeat uses the same frozen image bytes, three lanes and reversed worker
placement. The initial file-fed control motivates this confirmation.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Async | −3.19% to +1.56% | −0.11% to +3.15% |
| Iterable shipment | Sync | −2.44% to −0.82% | −3.82% to +0.74% |
| Iterable shipment | Async | −4.64% to −4.25% | −6.49% to −2.63% |

All 140 initial and 84 repeat batches verify. Keep the direct operand
entry. Async iterable shipment uses less elapsed time and process CPU on
both runs. Sync elapsed also falls on both runs,
while repeat CPU spans zero. The repeat file-fed control spans zero with
a steadier reference, and the ordinary and bound rows have mixed initial
timings. The admitted peaks and CLR allocation provide no material
storage improvement. These local results apply to the tested scripts.

[Initial timing](summary.json) · [Repeat timing](repeat/summary.json) ·
[Allocation and admission](allocation-summary.json) · [Image identity](identity.json) ·
[Initial observations](observations.tar.xz) · [Repeat observations](repeat/observations.tar.xz) ·
[Source, focused checks and CPython control](procedures.tar.xz).

# B1306 explicit UTF-8 ownership

String construction no longer infers ownership from whether a memory
slice covers its entire array. A caller copies borrowed memory or uses
an explicit owned-buffer operation. Immutable guest bytes can retain
their backing during decoding. A different owner funds the full
immutable array when the result uses it in full. JSON numeric hooks
and textwrap predicates can retain a same-owner UTF-8 range through
the existing string window operation.

The local Windows .NET 11 allocation diagnostic uses `3a6accc97`
and the exact recorded source patch, with both frozen images checking
against independent CPython goldens. The guest receives the same text
through each callback. The two callback scripts retain every value
for the duration of execution. The third script uses the ordinary
numeric decoder.

| 768 tokens or lines per execution | Reference CLR bytes | Candidate CLR bytes | Accounted peak |
| --- | ---: | ---: | ---: |
| JSON with a retaining numeric hook | 822,040 | 785,176 | 618,485 → 604,693 B |
| Textwrap with a retaining predicate | 289,592 | 228,152 | 324,821 → 290,136 B |
| JSON with native numeric conversion | 373,168 | 373,168 | 363,108 B |

All allowances return to baseline. Allocation falls 4.48% and 21.22%
on the callback workloads. No elapsed or process CPU comparison was
collected. Windows .NET 11 and Ubuntu .NET 10 each pass 501 white-box
and 2,397 public focused Release checks, including the original
immutable-byte sharing test and callback refusal, suspension and cleanup.

The standalone host-input snapshot had broken the independent
immutable-byte sharing assertion in CI. The exact reviewed variant
restores that path through the typed bytes decoder. An earlier local
test run used the old copy estimate for floating hook text. The
updated gate still refuses before the callback, using its admitted
window shell and backing-header estimate. The initial local variant,
its observations and the final patch are preserved with the checks.
[Earlier CI](https://github.com/Lokad/Lython/actions/runs/44958490322).

[Allocation](allocation-summary.json) ·
[Exact source, goldens and checks](checks-and-procedures.tar.xz).

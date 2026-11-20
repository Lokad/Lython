# B1227 Private lexical capture and callable admission

Compares `46ccc2efa` with private `db20a4369` on the quiet Azure
F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.
The reference image comes from source-equivalent `eaf4d65c1`. Two
processes per revision warm for eight seconds and run five rotated
rounds of roughly 0.4-second batches. Ranges cover all four process
median pairings. Negative changes favor the candidate.

| Script | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −2.94% to +2.15% | −3.14% to +2.16% | 2.15% / 3.02% |
| Postponed nested factories | Warm | +2.23% to +6.98% | +0.13% to +5.95% | 3.88% / 0.73% |
| Retained nonlocal siblings | Warm | −11.39% to −1.15% | −12.36% to −1.06% | 2.62% / 8.71% |
| Eager nested factories | Compile to result | −2.20% to +1.34% | −0.74% to 0.00% | 1.92% / 1.67% |
| CSV invoice | Warm | −0.07% to +1.49% | +1.67% to +3.28% | 0.66% / 0.91% |

Lexical definitions reserve for the callable, eager annotation backing
and private captures together after evaluating defaults and annotations.
They construct the definition before exposing new cells or transferring
already-funded frame charges. The general namespace-backed path keeps
its existing capture order. The borrowed inputs carry no new owner or
heap allocation.

The separate admission observer counts 10,265 → 8,217 positive resizes
on each nested factory script and 1,221 → 981 on retained siblings in both
execution modes. Each factory instance makes one fewer resize. Retained
charges, accounted peaks and allowance reuse match. Managed allocation
is effectively flat. In a Windows diagnostic with tiering disabled, the
shared callable constructor grows from 896 to 1,061 machine-code bytes.
The diagnostic gives no timing attribution.

Keep this prototype private. The longer [B1228 repeat](confirmation/README.md)
confirms the sibling improvement and the postponed regression. Invoice
CPU spans zero on repeat. A source refinement can isolate capture funding
from the ordinary constructor while retaining the shared allocation kernel.

Warm includes mediated RAM-backed I/O, result checks and previous-result
cleanup. Compile to result adds compilation and disposal of the compiled
owner. Fixture reset, startup and final teardown sit outside the guest
clock. Process CPU covers the wider request.

Windows .NET 11 and Ubuntu .NET 10 each passed 151 white-box and 731
public focused Release checks. Both 32-file images passed four whole-script
CPython goldens and ten independent probes in both modes. The original
probe with a tuple under an `int` return annotation records the expected
Lython compile-time rejection. Both quiet checks and all 100 timed batches
passed. Persistent archives verified, workers joined and guest fixtures
were removed. Two superseded exact VM DLL copies were retired after
verifying the committed immutable images and matching raw archives.

[Timings](summary.json) · [Admission counts](admission-counts.json) ·
[Allocation](allocation.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](source-and-checks.tar.xz).

# B1515 Carry admitted defaults directly into closed definitions

`09fc2c0a5` retains the production source measured at `655674b33`.
Closed computed factories use the same positional tuple and keyword
storage as executable definitions. They carry these owners directly
into callable construction. General executable definitions use the
shared allocation step and tag their operands for suspension and unwind.
Admission and evaluation keep their original boundaries. On an aborted
closed definition, `FunctionDefaults` refunds its unpublished owners.

The comparison uses retained `b7fdb4fec` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3, on November 23, 2026 UTC. Only the core
DLL differs across the two verified 32-file images. Two independent
workers per image supply seven rotated batches after eight seconds of
warmup. The ranges pair worker medians. Warm includes applicable mediated
RAM-backed I/O, golden verification and prior-result disposal. Compile to
result adds compilation and code-owner disposal. Wider process CPU also
covers worker resets and transport. Startup and final teardown sit
outside elapsed.

| Complete script | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −7.74 to −1.44% | −7.29 to −0.34% | 5.85 / 0.93% |
| Postponed nested factories | Warm | −6.71 to −3.07% | −6.03 to −1.81% | 2.44 / 1.42% |
| Factories observing defaults | Warm | −6.84 to −5.02% | −8.67 to −4.41% | 1.92 / 0.03% |
| Fully supplied arithmetic | Warm | +0.35 to +0.79% | −0.23 to +0.23% | 0.19 / 0.25% |
| Quote-heavy invoice | Warm | +0.41 to +1.31% | +1.75 to +2.50% | 0.43 / 0.46% |
| Wide native tuple helper | Warm | −4.29 to +3.13% | −3.42 to +1.05% | 1.71 / 5.94% |
| Postponed nested factories | Compile to result | −4.69 to −1.42% | −4.68 to −2.97% | 2.13 / 1.27% |
| Quote-heavy invoice, repeat | Warm | −2.30 to −1.01% | −1.75 to −1.03% | 0.57 / 0.74% |
| Wide native tuple helper, repeat | Warm | −0.74 to +2.22% | −2.83 to +2.25% | 0.12 / 2.86% |
| Factories observing defaults, repeat | Warm | −6.50 to +0.74% | −7.12 to +1.67% | 6.55 / 1.12% |
| Postponed nested factories, repeat | Compile to result | −8.05 to −2.51% | −8.81 to −2.13% | 5.93 / 0.10% |

Retain the direct owner transfer. Postponed first use improves in both
collections with supporting CPU. Warm factory gains and reduced transport
are established by the first run and source comparison. The observer's
repeat spans zero with an unstable reference, the wide result spans zero
twice, and the quote-heavy control changes direction. Its emitted tape
has no evaluated default construction. Local Windows JIT listings confirm
the change on the three factory workloads and report equal sizes for the
two inspected invoice call methods. This leaves the invoice variation
unexplained. All six scripts have equal local CLR allocation and admitted
peaks. All owners return their allowance.

Windows .NET 11 and Ubuntu .NET 10 each pass 362 public and 68 white-box
focused Release checks. Both images match six CPython workload goldens
and twenty emitted code bodies. Regression **ab8bca0c6** tests repeated
caught eager annotation failures while a prior result stays live. It
passes through both APIs on each platform with both runtimes. A local
negative control omitting the refund retains 32,760 more admitted bytes
across 63 additional failures and fails both cases. The original test
printed a variable-width count, then used only one annotation, leaving
the closed route untested. Both fixture corrections and the original
observations are archived.

Both quiet gates, all 196 initial and 112 confirmation batches, worker
joins and fixture cleanup pass. Both archives verify under the lease.
The first confirmation stage stops at a persistent-storage floor before
creating its work directory. The retry reuses the frozen cores and keeps
two MiB of receipt space. Local summarization corrects the expected
per-worker sample count against the already verified archive.

[Identity](identity.json) · [Timing](summary.json) ·
[Confirmation](repeat-summary.json) · [Allocation](allocation-summary-six.json) ·
[Selected bodies](selected-returns.json) · [Observations](observations.tar.xz) ·
[Repeat observations](repeat-observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Repeat receipt](repeat-terminal-status.json) ·
[Checks and procedures](procedures.tar.xz).

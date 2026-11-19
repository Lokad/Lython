# B1149 and B1150 compiler-owned keys for function definitions

Compared `fad73f098` with prototype `249d5522b` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. Each complete
32-file image matched eight CPython goldens before staging.

The code object owns the fixed key tables for executable keyword defaults and
annotations. This removes per-function cache slots, first-use admission and
table construction, and repeated hashing for distinct factory instances.
Each definition still creates its own mutable dictionaries and evaluates
default values and annotations in Python order. Compilation or the importing
execution funds the keys for the loaded code's lifetime. Runs fund value
storage and replacement overlap before mutation. Projected results have
their own owner.

| Workload | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Nested annotated factories | Warm | −17.26% to −11.24% | −18.15% to −11.86% | 4.56 / 2.61% |
| Annotated factory | Warm | −0.14% to +6.21% | +0.18% to +3.94% | 4.40 / 1.87% |
| Read every annotation dictionary | Warm | −2.41% to +0.90% | −1.49% to +2.70% | 0.73 / 2.64% |
| CSV invoice | Warm control | −1.13% to +5.39% | −0.65% to +6.25% | 1.67 / 4.84% |
| Captured rules | Warm control | −1.32% to +1.42% | −1.51% to +0.49% | 1.21 / 1.55% |
| Annotated factory | Compilation | −1.65% to +2.36% | −0.68% to +4.71% | 1.37 / 2.67% |
| Annotated factory | Compile-to-result | −6.30% to +7.91% | −5.19% to +8.44% | 3.47 / 11.31% |
| B1150 annotated factory | Warm | −6.07% to −2.11% | −5.75% to −1.85% | 1.53 / 2.64% |
| B1150 CSV invoice | Warm control | −4.04% to +0.26% | −4.44% to +2.00% | 3.97 / 0.49% |
| B1150 annotated factory | Compile-to-result | −3.64% to +1.60% | −4.05% to +0.91% | 5.17 / 0.26% |

Ranges cover all four cross-source pairs of process medians. Nested
factories improve with lower CPU. B1149 finds higher warm CPU for the
ordinary annotated factory and moving candidate medians for the invoice
and compile-to-result. B1150 repeats these three lanes with unchanged
images and reversed worker order. Warm annotated execution improves in
that repeat. The direction across both runs stays uncertain. Invoice and
compile-to-result still span zero. Retained at `f8fd828ac` for the simpler
ownership and the nested-factory gains. Preserve the initial adverse
results as controls for future comparisons.

Local nested-factory allocation fell from 3,028,504 to 1,898,008 bytes
per warm invocation (37.33%). Accounted peak fell from 5,098,577 to
2,968,657 bytes. The other warm allocation changes were at most 0.16%
in magnitude. The three annotated scripts allocate 0.55–0.64% more in
compilation. All measured allowance balances returned to baseline.

Windows .NET 11 and Ubuntu .NET 10 each passed 872 public and 69 white-box
Release checks, plus two result lifetime checks. They cover distinct
factory instances, mutable defaults and annotations, local imports,
terminal refusal, cancellation, and result funding after the code owner
closes. The first sparse checkout omitted the two representative CSV
scripts. Both passed when the fixture sources were included.

All 140 initial batches completed over 377.79 seconds. The repeat completed
60 batches over 165.62 seconds. Quiet gates passed in both runs,
workers joined, guest fixtures were removed and the VM verified the
persistent archive. The first disk preflight launched no workers. Staging
then retired only two redundant cores and their hard links after checking
the exact binaries already archived with B1146.

Warm includes mediated I/O, output and file verification and prior-result
cleanup. Compilation includes compiled-owner disposal. Compile-to-result
measures both. Startup, fixture reset and final teardown are outside guest
elapsed. Process CPU uses the wider request boundary.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Validation and procedures](checks-and-procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) ·
[Repeat measurements](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tgz).

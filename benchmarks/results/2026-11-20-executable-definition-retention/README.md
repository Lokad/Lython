# B1187 executable function declaration retention

Private measured source `fc1f75c0b` is retained at `fb690cc0e`. The
function creation operation owns the parameter layout, child code and the
remaining inputs. It removes the separate binding record, duplicated
name and span references, and the lowered annotations already emitted as
operand instructions. Definitions that evaluate annotations in a type
scope retain those expressions.

| Workload | Reference live bytes | Candidate live bytes | Change |
| --- | ---: | ---: | ---: |
| Eager annotated rules | 10,872 | 10,320 | −5.08% |
| Postponed annotated rules | 11,448 | 10,696 | −6.57% |
| Batch of annotation callbacks | 16,896 | 14,536 | −13.97% |
| 128 nested annotated definitions | 498,328 | 444,048 | −10.89% |
| File-backed CSV invoice | 22,336 | 22,256 | −0.36% |

The medians repeated in two separate Windows .NET 11 processes per
revision. Each process warmed eight compilations and measured five
groups of 64 live compiled scripts, with explicit collection in the
standalone diagnostic to estimate live managed bytes. Total allocation
during compilation falls 0.05–0.14%. Warm allocation and accounted peak
match exactly for all five workloads. This comparison measures storage.

The complete reference image `848cbf783` matches `8fafe1a5a` in tracked
runtime, tests and benchmark workers. Both source images matched five
CPython script goldens and returned the borrowed allowance to baseline
after compilation and warm runs. Windows .NET 11 and Ubuntu .NET 10 each
passed 989 focused Release checks for the isolated source, including
decoration, type scopes, generators, cancellation and admission. The retained
revision also includes the independent regex cancellation change. The two
integrated builds passed 1,091 focused checks per platform.

[Retention](retention-summary.json) · [Warm allocation](allocation-summary.json) ·
[Images and checks](checks-and-procedures.tar.xz).

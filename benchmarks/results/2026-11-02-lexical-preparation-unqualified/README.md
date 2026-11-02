# Module lexical preparation — unqualified, 2 November 2026

**The entire production prototype is removed.** The sole bounded comparison times
out during its final warm control. Completed compilation prefixes also miss the
frozen 7.5% requirement. No qualified speedup, retiming or retained fragment.

Source gate B82 distinguishes immutable lexical facts from mutable annotation and
abstract-analysis state. The candidate removes the standalone ModuleUsesSuper
walker, derives its decision during existing lexical preparation, and reuses
completed module binding facts at executable compilation. Module globals remain
separate from lexical enclosing cells; eligible super calls are marked only after
module-wide reads/bindings/global declarations are known. No skipped analysis,
new cache table, alternative engine, public option or ownership framework.

B80's sampled StatementSyntax enumerators total 13.11 MB across callers; only
2.24 MB of stored origins directly identify ModuleUsesSuper. Shared traversal
families and pass count do not establish removable CPU fractions. The two frontend
analyses retain their different branch/loop/refinement semantics.

The [pre-code-plan.md](pre-code-plan.md) freezes hypothesis, deletion, semantic
acceptance and comparison before code. Baseline runtime source is delivered
4cae38f4 on evidence-only 909c0e7f; exact baseline/candidate core and auxiliary
hashes are recorded separately. Ordinary adapter remains 0.4.0. One process uses
three isolated images with shared CoreLib/hash witnesses and identical auxiliary
bits, ordinary GC/tiering and no forced collections. Informational build metadata
may differ; neither producer nor target-framework assets are substituted.

Compilation-only CSV/text helper cases use 1,024 interleaved warmups/image and
seven 4,096-job rotating batches. Fresh config-edit uses 128 warmups and 512 jobs;
warm text control uses 128 warmups and 32 jobs. Two seconds settling per case.
Compilation includes compiled-owner disposal; fresh includes compilation, full
public Run, mediated I/O, complete output/files, result/compiled disposal. Warm
excludes compilation. Startup is excluded. No CPython timing or external ratio.

| Completed prefix only | Before batch ms | Candidate batch ms | Identical-before batch ms |
| --- | ---: | ---: | ---: |
| Compile CSV helper | 348.841 | 324.048 | 348.922 |
| Compile text helper | 269.619 | 251.333 | 271.411 |
| Fresh full config edit | 1,427.192 | 1,447.153 | 1,433.992 |

These incomplete-run medians are not qualified improvements. The compilation
observations are about 6.78–7.40% faster, still below 7.5% versus both originals.
The overall warm control, final quiet gate and natural final cleanup are missing.
Systemd reports **timeout**, 60.106 seconds, and terminates the entire control
group. The shell's EXIT trap happens to write zero; that is explicitly not success
proof. Raw partial samples, empty aggregate output and timeout journal are kept.

Candidate focused Release checks pass 441 internal/754 public per actual .NET 11
and .NET 10. Eight independent CPython cases distinguish eager function/lambda
defaults, deferred lambda/generator bodies, eager generator inputs/comprehension
bindings, class-suite reads and nested global declarations. CPython 3.13.2 Windows
and 3.13.16 VM outputs agree after public stdout LF normalization. Initial fixture
failures were CRLF expectations only; the failed log is preserved. The cases test
both Lython invocation APIs without a regex provider.

All three production files are byte-for-byte restored, followed by a fresh
nonincremental solution build and the same 441/754 checks on each actual runtime.
The eight independent public cases remain; no architecture-specific tests remain.
[restoration.json](restoration.json) and logs record source hashes and checks.

The timed-out driver is verified absent, service MainPID=0/control group empty.
Only its three uniquely named fixtures, created within this service and containing
the known current input/output layout, are validated and removed after all users
stop. This external cleanup is separate from natural workload cleanup; final zero
allowance is not claimed for the unfinished case. Completed prefix cases retain
their own goldens, zero balances and normal fixture-removal proof.

[evidence.tar.gz](evidence.tar.gz) contains the exact removed source patch, private
driver, catalog, goldens, reference checks, pre-code contract, identities, partial
samples, process state and external cleanup evidence. [assessment.json](assessment.json)
labels the result unqualified. No unchanged platform/stress/package qualification
or new publication. This does not settle the remaining whole-workload gap.

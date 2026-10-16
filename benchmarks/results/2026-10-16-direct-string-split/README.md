# Direct zero-argument string split — 2026-10-16

Ordinary exact-string `split()` calls now reuse the governed whitespace kernel
from executable frame storage under Run and RunAsync. Eligible calls avoid
per-receiver bound callables and general binding. Exposed methods, arguments,
keywords, expansion and custom receivers retain the general Python route. No
new binder, callable abstraction, memory owner or split kernel was introduced.

| Engine | Measured revision | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 4717904d4d8b8fd6a455c55557a86d17d62f5c1f | 7.018 ms | 10.70% |
| Lython after | 8b0f32fb30f9a53a43cab9eaf91ca0a702fb9747 | 6.405 ms | 10.08% |
| CPython | 3.13.16 | 1.3426 ms | 0.87% |

The observed 8.7% lower Lython median is within the baseline's 10.7% spread:
**no speedup is established**. This single diagnostic supplies no qualified
workload ratio, confidence interval or overall-gap attribution. These historical
measurements identify the revisions above, not later HEAD. The earlier text-summary
capture at 7caf4833 is a separate source pair, not a sample in this comparison.

The unchanged `scripts.text-summary.large` job reads 3,072 lines / 35,840 UTF-8
bytes, lowercases and splits each line, counts 6,144 words with a dictionary,
sorts four distinct words, writes `report.txt` and prints exactly `6144\n`.
The 353-byte [script](../../scripts/text-summary.py) is unchanged between roles.
Independent goldens check exact stdout and output-file bytes at 0, 16 and 1,024
fixture repetitions for both Lython and CPython before timing.

Boundary: warm public `Run`, reused compiled code with fresh invocation state,
input/output disk staging, exact file checks, execution/projection and prior-result
cleanup timed. The final result stays owned through the response. Compilation,
startup, protocol transport and full output hashing are excluded. Both engines
include the same file-job boundaries; the comparison host supplies efficient
ranged reads/appends and a shared unbounded allowance. Admission/scope accounting
and ordinary public limits remain active, optional fuel unset. CPython has no
equivalent in-process containment. Fresh compile-to-result, startup and
embedding-process latency remain unmeasured.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, SDK 10.0.401/runtime 10.0.12. Frozen Release Lython workers retain
workstation GC, Interactive latency and ordinary tiering. CPython 3.13.16 uses
normal GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Only supervisor tiering is disabled.
Seven rounds span 8.094 s under a 30 s systemd process-group cap; one capture,
no repeat, retuning, new producer or full lane. All 524 production sources per
role match frozen manifests, with identical comparison tooling. Seven workers
and their fixtures are gone, relevant services inactive and exclusive VM lease
free. Scripts were LF-validated before upload.

Preparation first selected the `full` catalog, which excludes representative
scripts, and stopped before correctness/timing. Its [exit receipt](prepare-exit-code.txt)
is retained. Corrected preparation reused the successful baseline build and used
the existing `representative` profile, then completed with an explicit
[zero exit receipt](prepare-corrected-exit-code.txt). There was one timing capture.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json)
and eight [CPython lifecycle/semantic goldens](oracles.json) preserve evidence.
Frozen archives, scripts and local build/test receipts remain in private notes.

All 2,681 ordinary white-box tests, 58 focused runtime checks and 300 public
checks pass, including the seven complete CSV/text/JSON/file jobs. New controls
cover Unicode whitespace, empty input, fresh owned result containers, distinct
receivers without bound-method caches, general/custom calls, unchanged fuel/spans,
before-allocation refusal and cleanup returning allowance. A genuinely suspended
receiver executes once, retains its owner until completion and preserves
cancellation and terminal first-admission failure. The test always awaits that
operation even if an assertion fails; its nine cases pass after tightening the
failure cleanup. Original stress scales/timeouts and containment assertions are
unchanged. No Stress, full-lane, host-latency or current-HEAD timing claim follows.

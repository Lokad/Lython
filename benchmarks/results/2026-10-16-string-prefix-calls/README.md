# Direct string prefix calls — 2026-10-16

One unchanged complete file-search job, warm synchronous execution:

| Engine | Revision/version | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython baseline | cdf959615e4093c7fb35f7294ca46c88390e59eb | 5.667 ms | 57.4% |
| Lython candidate | 473aa3966802a68c155efebecffa9e816abf763b | 3.223 ms | 63.7% |
| CPython | 3.13.16 | 0.2845 ms | 1.3% |

The observed 43.1% lower candidate median is smaller than Lython's spreads.
**No improvement is established.** All samples are retained; no repeat, tuning,
new producer or long lane. This short diagnostic supplies no qualified ratio,
confidence interval or host-impact qualification. It measures the complete job
and cannot attribute the observed difference to prefix-call transport alone.

Both roles include the prerequisite correctness fixes in cdf95961: builtin
startswith/endswith use the existing positional-only binder, and static prefix
validation defers uncertain tuple entries to runtime. Invalid first entries still
produce definite diagnostics. Runtime tuple scanning already short-circuited.
The original control failures and an additional unknown-first-entry regression
are preserved in [local validation](local-validation.json).

The candidate changes six production files. Ordinary one-positional-argument
exact PyString startswith uses the existing receiver/argument operand slots and
shared method marker, avoiding per-receiver bound callable/member-cache creation,
CallArgumentValue packaging and a second builtin binding array. The same existing
Unicode/tuple kernel runs in both paths. Call frame admission, two operation
checkpoints, fuel/spans, cancellation and retained receiver funding are preserved.
Custom objects, exposed/unbound/expanded calls, other arities and keywords retain
normal lookup/binding. There is no new binder, owner, frame or cache framework.

The unchanged scripts.file-search.large has 319 source bytes, 2,049 input lines /
42,010 UTF-8 input bytes. A helper calls line.startswith('NEEDLE:') and breaks from
a protected file loop after line 1,025, writes the exact match file and prints the
scan count. Both revisions pass independent stdout/file goldens at all three
existing sizes; see [baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json). The measured case is not a new producer.

Timers include fresh invocation state, helper definitions, disk reset/staging,
file iteration, protected early exit, output file/byte checks, captured output/
projection, cheap goldens and prior-result cleanup. Compilation, process startup,
transport and full hashing are excluded. This is warm Run against ordinary warm
CPython and does not measure compile-to-result or async host I/O.

Measured on the dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD
EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16,
ordinary GIL/GC and -I -S. Normal Lython GC/tiering, cooperative allowance,
preflights, cancellation and managed stack admission remain enabled; fuel unset.
CPython has no equivalent in-process containment. These are diagnostic timings.

[Raw timing](warm.json) retains all attempts, role-specific binaries and policies;
[declaration](comparison-declaration.json) freezes six source differences.
[Cleanup/source proof](cleanup-source-proof.json) verifies identical shared
comparison/catalog/Python sources, seven workers gone, seven file-fixture directories
removed, inactive services and an available exclusive VM lease. The owned timing
command finished in 8.736 s including cleanup.

[Validation](local-validation.json): 2,537 ordinary white-box, 1,226 focused public
and 320 governed/representative/shared-consumer tests pass; groups overlap and each
owned group finishes below 30 s. Ten new native controls prove distinct receiver
reuse, untouched argument pools, actual suspension/cancellation, ordinary custom
lookup before arguments and the existing nine-step fuel/span boundary in both
APIs. Forty-five new public cases include 17 independent [CPython programs](cpython-goldens.json),
24 actual host success/cancellation paths, retained aliases across another run and
caller diagnostics. No timeout, containment or admission assertion was relaxed;
no new Stress run is claimed. Optional-bound async conversion remains a separate
gate; no general string-protocol or binder redesign is claimed.

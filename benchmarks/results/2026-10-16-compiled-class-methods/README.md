# Prepared ordinary class methods — 2026-10-16

**Rejected after cross-platform qualification.** [CI38922855547](https://github.com/Lokad/Lython/actions/runs/38922855547)
passed all 2,691 Ubuntu white-box tests, then the public test host fatally overflowed
during ordinary recursion. Removing duplicate executable-body depth counts had
not preserved Linux unwind safety. The prototype and its added callback policy
are withdrawn; the validated admission counts and lowered class routes are
restored. Independent retention, call-span and safe rethrow fixes are retained.
No later HEAD performance or containment claim follows from this candidate.

The rejected prototype made eligible direct class-suite functions share executable bodies prepared once
at compilation, using the existing function compiler/factory. Definitions retain
private function values/defaults and live lexical/class cells. Selected methods
no longer executed lowered bodies or repeated method scope analysis. Generic,
conditional and other required lowered routes remain. General bound-method
argument repackaging is still present; the prototype established executable-body
coverage, not a direct method calling convention.

| Engine | Measured revision | Fresh compile/run median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 4484f64ab4166ea74e9ac46572448d7efa2934d7 | 9.325 ms | 17.12% |
| Lython after | c3db3ca26694a100af163b863856de13932fc77a | 11.494 ms | 10.60% |
| CPython | 3.13.16 | 0.9783 ms | 2.47% |

**No improvement established: the candidate median is 23.3% higher.** This is
one unqualified diagnostic with substantial spread, not proof of a regression's
magnitude or a qualified CPython ratio. It provides no basis for a speedup claim.
The added preparation is included in the timed boundary; compile-only attribution
is unmeasured. The next ordinary-call work must account for this result. These
measurements identify the revisions above, not later HEAD. The older 58aece73
warm method capture has another input size and boundary; keep it separate.

The actual unchanged `calls.method.large` program constructs an accumulator,
performs 16,384 method calls updating a field and prints exactly `134258688\n`.
It has 208 UTF-8 source bytes. Both engines pass the catalog's independent sum
formula and exact stdout checks at 256, 2,048 and 16,384 calls before timing.

Boundary: fresh compilation plus synchronous public `Run`/ordinary Python module
execution in persistent warm workers. Fresh invocation state, default options,
captured/projected output, cheap golden checking and resource cleanup are timed.
Worker startup, protocol transport and full output hashes are outside the timer.
This is neither warm-only execution nor a separate startup or compile-only result.
Lython admission/explicit ownership, cancellation checkpoints and normal public
limits remain enabled; optional fuel is unset. CPython has no equivalent
in-process containment. No embedding-process latency claim follows.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, SDK 10.0.401/runtime 10.0.12. Release workers use ordinary GC/tiering;
CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Only supervisor
tiering is disabled. Seven rounds span 7.937 s under a 30 s systemd process-group
cap. One capture, no repetition, retuning, new producer or full lane. All 524
production sources per role match frozen Git manifests, comparison tooling is
identical, seven workers/fixtures are gone, services inactive and VM lease free.

**Setup discrepancy:** the original declaration requested `calls.method.medium`,
but the copied script retained `.large`. The receipt and actual source prove the
larger workload was used for all engines. Preserve the declaration and this
[post-capture erratum](comparison-erratum.json); no replacement capture was run.
The source/lane proof did not check this case identity before capture. This
limits the planned medium-case coverage, not the identity of the reported sample.

[Samples](compile-run.json), [original declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json)
and 18 [independent CPython semantic goldens](oracles.json) preserve evidence.
Frozen archives, LF scripts and local build/test receipts remain in private notes.

All 2,691 ordinary white-box tests, 258 public class/scope/super checks, 145 public
protocol/ownership/terminal-refusal checks and 16 isolated stack/trace controls
pass locally. New controls exercise real class sites, retained defaults/aliases,
nonlocal/class-cell preflights, actual host suspension/cancellation, finite
same-object copy hooks, recursive callback cleanup and counter restoration.
Failed initial stack probes are preserved: compiled methods exposed fatal
copy-hook recursion after real I/O. The correction removes duplicate logical
body counts, rethrows through handler-free frames and shares existing depth
state under a conservative 64-unit structural callback budget. Deep callbacks
can now reject earlier; ordinary/pure-data limits and original stress scales
are unchanged. No unsafe/native APIs, owner layer or collector was introduced.
This evidence does not claim Stress or platform/package qualification; those
are delivery checks recorded separately.

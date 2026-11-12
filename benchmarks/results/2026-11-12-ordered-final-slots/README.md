# Ordered execution over final slots — R289

2026-11-12. Active prototype based on source commit4cc057f0; qualified9bca1337
production/e4cc65b2 is the unchanged reference. Neither prototype is a release.
Publication remains postponed. The complete-script performance objective is open.

## First form: B643

Replace the native expression graph, preparation, whole-body guard and recursive
evaluator with a small ordered managed span over final executable slots/PC. Actual
operands are inspected at each operation. Unknown protocols transfer live values
and the exact PC into the existing ordinary frame/trampoline without rebinding or
replaying earlier effects. Python tuples remain real tuples. The migration removes
287 production lines;571 production files include567 C# files. No public policy,
budget, unsafe code, controller, background operation or third backend was added.

Both targets build.3260 focused checks pass after correcting a short-circuit Dup
slot bug;30 additional closure/refusal checks pass after fixing a test's invalid
assumption that a cached frame always requires fresh admission. The corrected
fixture explicitly refuses a callback allocation after a closure write, through
direct/map calls and both APIs. Three new public cases preserve retained inputs
through late protocol I/O, cancellation, awaited cleanup and budget reuse. Local
CPython3.13.2 independently produces their golden. All seven existing scripts pass
warm sync/async, compile and compile+run output, zero-balance and fixture checks.

Warm allocation is essentially flat. Compilation saves0.14–1.03% allocation;
desktop elapsed is diagnostic, including adverse observations and moving controls.
The initial compilation error about the generator default hook survives in tool
output but its first raw build log was overwritten; later build/test failures and
all timing captures are preserved. No missing raw log is reconstructed.

| Complete script / API | Elapsed change across both controls | Wider process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| CSV / Run | −4.01..−1.64% | −5.33..−1.09% | 2.07/0.35% |
| CSV / RunAsync | −8.49..−2.40% | −8.33..−2.84% | 2.37/3.96% |
| Native search / Run | +11.71..+13.71% | +10.12..+11.80% | 1.26/0.52% |
| Guarded search / Run | +3.79..+8.47% | +3.12..+5.50% | 3.93/0.55% |
| Captured text / Run | +3.84..+8.12% | +4.69..+6.81% | 0.99/3.00% |
| UTF8 report control / Run | +0.91..+3.25% | +0.00..+0.61% | 0.52/1.75% |

Negative means shorter. Ranges cover four process-median pairings, not confidence
intervals. The CSV benefit does not justify retaining the native/captured debt
without a decision. One bounded completion refinement removes redundant clearing
from the short-lived16-reference span, preserves deterministic private-temporary
retirement, clears before ordinary transfer, and reuses the existing definition-
owned record-key cache. It also deletes obsolete argument/proof-shape metadata.
This is a testable hypothesis, not an attribution or a predicted improvement.

Quiet Azure F4as_v7/Ubuntu24.04.4, stock.NET11RC3 GC/tiering, fixed fec79 worker and
adapter0.4. Four independent Lython processes,5s warmup (12s captured,8s report),
seven rotated500ms batches. Warm includes mediated RAM I/O, complete output/file
validation and prior-result cleanup; excludes compilation, startup, reset/protocol
and final caches. Compilation/fresh desktop observations remain separate. No
CPython timing or RSS/pending-I/O/embedding-tail claim is made here.

Six jobs finish280.101s under420s owner/30s stop/30s margin. All actual image/
auxiliary/source identities, goldens, balances, before/after quiet, exit/join,
RAM unmount and persistent/local archive SHA checks pass. Terminal is cached.
The first disk preflight refused before mutation; exact committed archived
duplicates were retired under fresh idle lease/path/UID/SHA/inode/all-link proof.
OS240MiB/evidence12MiB floors remain unchanged. Final free266645504B; no users or
RAM fixtures remain. Original adverse conditions and all controls are retained.

[Raw source/checks](first-form/source-checks.tar.xz),
[controlled summary](first-form/summary.json),
[raw VM observations](first-form/observations.tgz).

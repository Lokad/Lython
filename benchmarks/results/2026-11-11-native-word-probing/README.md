# Native UTF-8 word probing — 2026-11-11

**Active prototype; not a qualified delivery.** The exact candidate is the
five-file patch over `a2abcac1`, whose production source is qualified `8f8628a4`.
Frozen source archives and loaded core hashes identify this comparison; it does
not describe a later HEAD. Publication remains postponed.

The existing private histogram segment maps short cased words into inline UTF-8
and probes canonical dictionary keys before constructing Python strings. New keys
and observable final bindings still receive independent immutable values. The
ordinary object call/return ABI, cased iterator and ownership remain unchanged.
One canonical span probe serves record-name lookup too. Existing eager admission
still funds all eventual words: fewer CLR allocations do not imply smaller budget
reservations. Unicode, wide words, protocols and escaping aliases retain their
ordinary behavior; there is no new guest limit, cache or reclamation layer.

Complete captured text allocation falls **20.45% Run / 20.43% RunAsync**;
ordinary text falls **22.44% / 22.42%**. Other scripts and compilation allocation
are effectively flat. These figures come from the existing fixed-worker source
pair: six unchanged complete scripts, both APIs, separate compilation, 256
warmups and five rotating rounds of sixteen invocations sharing CLR/GC/hashing.
Desktop clocks are diagnostic. Async search elapsed +9.31..+9.91% with 0.54%
unchanged control, and CSV +8.02..+10.41% with 2.21% control, remain preserved.

B595 uses the existing quiet F4as_v7 Ubuntu/.NET11RC3 VM, fixed `fec79` worker
and adapter 0.4. Ten short jobs finish in **278.387 seconds**. Each job has two
independent processes per image, five-second warmups and seven rotated 150 ms
rounds, with normal GC/tiering.

| Complete script / API | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Captured text / Run | −1.63..+3.32% | −2.25..+3.53% |
| Captured text / RunAsync | −4.08..−0.57% | −9.68..+6.25% |
| Ordinary text / Run | −6.91..−3.58% | −7.09..−4.00% |
| Ordinary text / RunAsync | −10.75..−8.05% | −11.02..−8.66% |
| Search / Run | −8.62..+0.30% | −8.68..+5.02% |
| Search / RunAsync | **+6.97..+11.60%** | **+9.32..+17.04%** |
| CSV / Run | −0.07..+4.54% | +2.94..+9.90% |
| CSV / RunAsync | −0.29..+3.31% | −3.23..+3.33% |
| JSON-lines / Run | −3.51..+3.49% | −4.44..+2.38% |
| Report / RunAsync | −1.72..+0.91% | −3.96..+4.17% |

Negative means lower time. Ranges cover all four independent process-median
pairings, not confidence intervals. Captured CPU remains uncertain; ordinary
text has supporting CPU improvement. Async search confirms a regression; CSV
Run CPU is adverse too. These results establish neither general CPU neutrality
nor new CPython ratios. Full controls/IQRs remain in the [summary](quiet/summary.json).

Warm VM clocks include mediated RAM-file I/O, complete output/file validation and
prior-result cleanup. Compilation, startup, fixture reset/staging, protocol and
final caches are excluded; external process CPU additionally includes reset and
protocol. Desktop complete-job clocks include reset and immediate result disposal.
Do not combine those clocks or infer CPU gains from allocation.

Focused Release checks pass 1,642 internal and 3,807 public cases per actual
.NET11/10, followed by final 69- and 70-case fixture runs per target. Six isolated
CPython references agree. Coverage includes Unicode expansion/sigma/whitespace,
key/final-binding identity, empty sources, promotion, captured/mirrored locals,
callback observations, terminal first refusal, actual suspending protocol I/O,
active cancellation, retained results, joined cleanup and budget reuse. The first
62 failures were an unsupported generic-dictionary fixture input; the corrected
object-valued fixture passes. An original one-character identity reference
conflicted with CPython interning and was replaced with multi-character words.
Both original conditions remain in the evidence.

The follow-up build inventory records matching Release debug policy, target and
SDK. After eliding compiler-generated closure/state-machine reference ordinals,
24,894 common method bodies match structurally; only dictionary accumulation,
the native dictionary loop and record-name lookup differ. Generated methods whose
names changed are listed separately. This is not a proof of executed paths or
native-code equivalence. The unnormalized inventory is retained too. The shared
search call/dispatch body has no intentional source change; the cause of its
regression remains unresolved, not attributed to a particular JIT mechanism.

All VM identity, output/file, allowance, quiet-gate, joined-user, RAM cleanup and
under-lease archive checks pass; independent retrieval verifies SHA256. B595 is
terminal/cached with no remaining users or fixtures. Initial inventory refused
the wrong duplicate-catalog identity before mutation; the committed canonical
archive entry corrected it. Only terminal duplicates/withdrawn-image aliases
were retired after fresh path/UID/inode/all-alias proofs against exact committed
Git bytes. Reference and current candidate images remain protected.

The allocation benefit and ordinary-text CPU improvement justify investigation,
not automatic promotion or withdrawal by percentage. Close the control-regression
gate before delivery, recording an explicit decision on remaining debt. No full
prototype platform/package qualification has been run. Qualified `8f8628a4` and
its cached delivery remain the reference. [Current CPython account](../../../docs/BENCHMARKS.md),
[source/core identity](diagnostic-identity.json), [exact candidate sources](sources-candidate.tar.xz),
[raw quiet observations](quiet/observations.tgz) and [decision](decision.json)
preserve the boundaries and outstanding work.

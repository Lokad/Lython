# Native list construction and sequence state

Runtime producer **d1a8823c**, production tree
**9f8880844b766af4095fa6d2bfc3a51d89a400b8**, combines published list construction
(`6c7f0316`) and explicit sequence wrapper state (`d1a8823c`). **Live-value logical
reclamation and forced-GC relief are unchanged.** Other native operations,
private construction, upstream iterator captures, conditional identity and
derived funded workspace remain required for complete ownership.

List conversion populates its actual governed owner while retaining current
inputs on the existing active caller chain. Async conversion's temporary CLR
list and final copy disappear. Known counts preflight capacity; numeric slots
are charged as populated. Failure, cleanup or registration denial clears and
refunds only the unpublished list. Held element aliases retain their owners.
The shared native operation still preflights its 128-byte shell. SPEC documents
that tiny budgets and collection limits can reject earlier during consumption.

Named checked and async enumerators replace four compiler-generated forwarding
helpers. Their actual source, current item, upstream and context are available
to the existing funded storage walk without getters or allocation. Retirement
clears those references after upstream cleanup, including failure paths.
Unknown upstream shapes still stop completion. No second root registry,
per-instruction hook, scalar identity tier or protection switch was introduced.

## Validation

Four original list-prefix probes fail before their fix. Fourteen final list
cases cover small/promoted backing, sync and real async reentry, construction
and cleanup failures, held aliases and denied registration. **118 internal and
679 public checks pass** at `6c7f0316`; **52 complete CSV/text/JSON/file and
protocol controls pass** separately. An earlier combined representative/alias/
file diagnostic reached its 28.935-second cutoff and is preserved as incomplete.
Its fixtures and assertions were not weakened. The full checkpoint below covers
the original cases at their existing scales.

Two original owned-enumerator probes fail on `6c7f0316`; seven behavior/opaque/
cancellation controls pass. Twelve final sequence cases cover allocation-free
walking, genuine async suspension, current/cleanup failures, retirement,
acquisition/re-enumeration and dictionary creation-time version capture.
**87 internal checks pass in 1.170 seconds** and **684 public checks pass in
5.705 seconds**, including cleanup. All four cancellation modes produce exactly
128 values when cancellation starts at 90. Range's existing post-yield check
remains unchanged; both routes retain their 64-item checkpoint cadence.

Full ordinary Release, original stress and explicit Release package consumer
results, elapsed times and package/DLL identities are in
[qualification.json](qualification.json). Full Windows/Ubuntu CI is a separate
delivery gate at the documentation descendant. Suite durations establish
correctness observations, not runtime speed or embedding latency.

The original focused assessments remain in [list-focused.json](list-focused.json)
and [enumeration-focused.json](enumeration-focused.json). Their "focused only"
labels describe those original checkpoints, with the combined gates recorded
separately here. Private setup failures and process receipts remain under
`.git/agent-notes/enumerable-state-20261014/` and
`.git/agent-notes/native-aggregate-20261013/`.

## Short cost observations

Each distinct diagnostic ran once on the shared Windows 11 x64 development
workstation: i7-14700KF, 28 logical processors, 64 GiB; SDK
10.0.300-preview.0.26177.108, runtime .NET 10.0.13, CPython 3.13.2 with ordinary
GIL/cyclic GC and `-I -S`. Baseline sources were archived and built separately.
Candidates were built before commit; their old informational metadata must be
interpreted with the frozen source and loaded-DLL identities.

| Workload | Preceding Lython | Candidate Lython | CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Materialize 50,000 integers and sum | 3.690 ms | 4.371 ms (`6c7f0316` source) | 0.782 ms | Inconclusive; IQR/median 27.54% / 52.15% / 78.47% |
| Drain and print 16,384 generated values | 8.568 ms | 8.728 ms (`d1a8823c` source) | 1.011 ms | Inconclusive; IQR/median 29.71% / 30.48% / 0.94% |

The owned groups take 12.239 and 8.144 seconds. Warm synchronous public
invocations include fresh state, captured/projected output and independent
golden checks; compilation, startup and transport remain outside the timer.
The first adapts `AsyncMaterializationBenchmarks` to shared printed output;
the second uses the existing comparison catalog's generator/drain case.
Raw attempts and identities are in [list-micro.json](list-micro.json) and
[enumeration-micro.json](enumeration-micro.json).

Neither observation qualifies a performance change, CPython multiplier or
embedding-latency claim. Neither was repeated. They do not measure the cost
of genuinely suspended async pulls. No ten-minute comparative lane ran at this
correctness checkpoint. See [BENCHMARKS.md](../../../BENCHMARKS.md) for the primary
performance account and remaining complete-script uncertainty.

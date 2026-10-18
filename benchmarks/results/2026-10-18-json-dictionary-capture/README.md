# JSON dictionary helper capture, 2026-10-18 UTC

The general JSON value serializer previously allocated an 80-byte capture object
even for scalar values. Its dictionary local function captured nine existing
inputs. An ordinary static helper now receives those inputs explicitly. The
helper body is unchanged apart from indentation; three dictionary call sites
pass the same values. No new parser, encoder, dispatcher, cache or owner.

Baseline: `15b4bc031d5c7d2c474d448968f4c20c1a00cca8`; candidate: that revision
plus the [one-file patch](candidate.patch). Release [baseline IL](baseline-il.json)
places capture construction before common checks and scalar dispatch;
[candidate metadata](candidate-il.json) confirms the generated capture and its
value-state-machine reference disappear. This inspection is diagnostic evidence,
not a permanent test of the compiler's internal architecture.

| Native warm shape | Baseline sync / async | Candidate sync / async | Allocation saved per invocation |
| --- | ---: | ---: | ---: |
| Scalar | 968 / 992 B | 888 / 912 B | 80 B |
| 64-value sequence | 7,768 / 7,792 B | 2,568 / 2,592 B | 5,200 B |
| 64 small dictionaries | 32,016 / 32,040 B | 16,576 / 16,600 B | 15,440 B |
| Sorted dictionaries | 54,248 / 54,272 B | 38,808 / 38,832 B | 15,440 B |
| Default callback replacements | 74,280 / 78,400 B | 58,840 / 62,960 B | 15,440 B |
| JSONEncoder.encode | 32,976 / 32,968 B | 17,536 / 17,528 B | 15,440 B |
| Incremental encoding / parsing controls | See comparison | Identical raw arrays | 0 B |

One successful frozen pair uses two warmups, eight samples and eight invocations
per sample. One baseline async-default sample adds 64 bytes per eight invocations;
the raw evidence stays visible and medians are reported. There is no recapture or
post-gate source tuning. All output hashes match six independent CPython programs,
native step arrays match and frame/temp/shared-root cleanup passes. All 70 guard
records keep identical semantic fields; 32 affected refusal cases allocate 80
bytes less each, while other guard allocations remain identical.

This is an **allocation-only diagnostic**. Elapsed time was not measured. It
excludes compilation, context/payload setup, scheduling, public projection,
process startup and host I/O. Pure async values complete on the measured thread.
No latency, compile-to-result, CPython or representative-job speedup is claimed.
The catalog's json.dumps and JSONEncoder.encode use this serializer; complete
config/JSON/manifest scripts use incremental json.dump and remain controls.
[Environment](environment.json), [scope](measurement-scope.json) and
[source screen](source-screen.json) record the boundaries.

All 465 distinct focused Release checks pass, including 58 complete CSV/text/JSON/
file jobs. Two new public dictionary-default lifetime cases pass before/after:
real host I/O succeeds or pauses for cancellation, another execution uses the same
root, retained output stays funded, and cleanup returns to baseline/zero. Existing
cycles, recursion limits, sort-before-skip rules, Unicode, subclass/options/hooks,
refusal-before-allocation, terminal refusal, caches and managed stack checks pass.
Output/scratch estimates, awaited cleanup and public contracts remain unchanged.
Original Stress scales, assertions and timeouts are preserved; regex stays planning-only.

Initial fixture failures preceded runtime edits: a disposed-result reread in the
cancellation test and unpublished lexical setup values in the diagnostic. The
corrected fixtures retain their original checks. Original failed receipts and
sources are preserved in the private delivery notes; they establish no runtime
bug or performance result.

[Diagnostic](diagnostic.cs), [raw baseline](baseline.trx), [raw candidate](candidate.trx),
[baseline guards](baseline-guards.trx), [candidate guards](candidate-guards.trx),
[comparison](comparison.json), [goldens](goldens.json) and [validation](validation.json)
preserve evidence. Temporary diagnostic removed; measured source is final.
Platform/Release qualification lives in PLAN. Stop at this capture removal;
recursive JSON and async execution redesigns are separate work.

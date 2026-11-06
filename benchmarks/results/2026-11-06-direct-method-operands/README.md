# Ordinary method operands — 2026-11-06

Retain a general calling-convention simplification: immediate instance-method
calls carry their receiver and selected function through existing operand storage,
without constructing then unpacking a PyBoundMethod or its display name. Default
attribute lookup, an ordinary non-data function and no own override are proven
before arguments. Mutable lookup remains live, without a new cache. Existing
native builtin eligibility, binder, frame runner and dynamic descriptor protocols
remain; exposed method values still materialize their Python object.

[Design](contract.md), [validation](validation.json), [independent Python goldens](independent-goldens.json),
[whole-workload golden](workload-golden.json), [correctness logs/original failures](correctness-history.tgz).
Full local Release4210 internal/16088 public checks pass per actual.NET11RC3 and
.NET10.0.8, focused428/3047, zero build warnings/errors. New tests include actual
pending argument reads, cancellation, terminal refusal through guest suppression/
finally returns, no later effects and reusable shared allowance. Two expired
HasReceiver=false checks now require the native shortcut opcode to be absent;
keyword calls still use the general binder. No public/Python contract changes.
Full Windows/Ubuntu, original stress and installed-consumer delivery is **GREEN**
on exact1134d6e8, cached terminal42158371981/all8. [Delivery evidence](delivery-ci.json)
excludes the later B01 CSV prototype; never requery this terminal run.

## Whole-script observations

F4as_v7/Ubuntu24.04.4, .NET11RC3, CPython3.13.16, frozen b94 worker/protocol2,
normal GC/tiering. Core520b3eb0 candidate/control versus F04/core0050f188.
CSV method helper is the existing4096-record ordinary CSV script, with the same
accumulator implemented as an instance method; fixtures and output/report bytes
are unchanged. Five guest warmup seconds/role, seven rotated150ms rounds.

| Script/boundary | Candidate / identical control | F04 reference | CPython | Candidate / control delta versus F04 |
| --- | ---: | ---: | ---: | ---: |
| Method CSV warm |4.917 /4.869 ms|5.104 ms|3.237 ms|-3.67% /-4.61%|
| Method CSV compile-to-result |5.191 /5.241 ms|5.297 ms|3.434 ms|-1.99% /-1.04%|
| Ordinary CSV warm control |4.721 /4.675 ms|4.739 ms|3.237 ms|-0.38% /-1.35%|
| Text warm control |2.275 /2.293 ms|2.358 ms|1.160 ms|-3.52% /-2.76%|

B317 completed113.04s/four jobs, all quiet/funding/golden/join/fixture gates pass.
Method-role spread0.97%, larger role IQR1.72%; fresh spread0.96%/IQR1.46%.
Controls also improve, so these diagnostic changes cannot all be attributed to
wrapper removal. No causal guest-only CPU or broad primary-workload gain claimed.
Warm includes invocation, mediated RAM I/O, full validation and prior-result
cleanup; fresh adds compilation/program disposal. Startup, staging/reset,
transport and final teardown are excluded. CPython retains imports while Lython
isolates them. [Raw observations](observations.tgz), [summary](summary.json).

B319 separately measures whole-process allocation in one complete warm method CSV
job:5,864,450 /5,865,629B original roles versus5,438,448B candidate, **-7.26% /
-7.28%**. Elapsed4.858ms versus5.067/5.125ms (-4.13% /-5.20%) is diagnostic and
includes current-result cleanup, distinct from B317. Three isolated Lython images
share CoreLib/process hashes; same normal GC/tiering,6s guest warmup/image,
9rotated200ms rounds. Full golden/balance/quiet/join/fixture checks pass. Allocation
includes fixture/reset/reflection/background work, not just guest objects.
[Allocation evidence](allocation/observations.tgz), [summary](allocation/summary.json).

Original B318 stopped before observations: the copied private driver pinned the
ordinary CSV case id. Preserve its [source/binary/failure](allocation/failed-observations.tgz).
B319 changes only that guard/purpose label to the method workload, rebuilding
without shared compilation/build servers before settling; metrics and checks stay
unchanged. No source/runtime/worker/golden changes or rejected samples.

The representative four-script CPython table is refreshed separately at this
coherent call/frame checkpoint. Publication remains postponed. Further substantial
whole-script performance work stays active.

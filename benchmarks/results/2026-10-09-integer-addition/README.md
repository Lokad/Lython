# Short loop improvement experiments, 2026-10-09

Guarded exact integer addition reduced the basic loop's median time by
**10.7–14.1%** against the accepted synchronous dispatcher in two collections of
about **seven seconds** each. A separate loop-name assignment candidate showed
no repeatable improvement and remains isolated. Both decisions retain all
measurements; no full comparison lane ran for either experiment.

These are diagnostic observations, without milestone qualification or confidence
intervals. The [short comparison contract](../../COMPARISON.md#short-improvement-rounds)
uses the same precompiled 16,384-iteration reduction, fresh public invocations,
complete independent golden-output checks, normal budgets/GC/tiering, one-second
worker warmups, two seconds for background compilation to settle, and seven
rotating/reversed-order batches calibrated to at least 25 ms. All workers and
services completed successfully within their 30-second external bounds. Builds,
tests and transfers finished before each collection; input hashes stayed unchanged.

The reference for both experiments is clean Release
`891798a6ffa9a35494b6bd32e86ba0038d2b3425`, the accepted
[synchronous dispatch change](../2026-10-09-sync-dispatch/README.md).
The same VM/toolchains are pinned: four AMD EPYC 9V45 vCPUs, Ubuntu 24.04.4,
.NET 10.0.12/SDK 10.0.401 and optimized CPython 3.13.16. Timed worker and Python
helper implementations are unchanged. There is no idle/noise qualification gate
in these short runs. CPython remains substantially faster on this job.

## Accepted: exact integer addition

Measured candidate: `e97cd4dab458fe03577c25d26780beb894c6f938`.
Only pairs of exact boxed `BigInteger` values take the shared fast path for
ordinary and augmented addition. Custom objects and mixed values retain general
dispatch. Results keep arbitrary precision, the existing overflow translation,
one output box and normal heap ownership/reclamation. Instruction checkpoints
and public thread/stack containment remain enabled.

| Collection | Baseline Lython | Candidate Lython | CPython context | Median time reduction |
| --- | ---: | ---: | ---: | ---: |
| 1 | 3,161.528 µs | 2,714.206 µs | 616.302 µs | 14.1% |
| 2 | 3,302.348 µs | 2,947.700 µs | 628.832 µs | 10.7% |

IQR/median was **5.1% / 0.4% / 0.2%** for baseline/candidate/CPython in
collection 1, and **5.7% / 0.9% / 0.4%** in collection 2. Receipt collection
clock spans were **7.00 / 7.08 seconds**. The measured candidate passed 402 Linux
Release public checks and 441 local focused Debug checks. Local Release also
passed the same relevant coverage, including all 16 new exactness, promotion,
mixed/custom operand and retained-charge regressions.

## Rejected: direct loop-name storage

Isolated candidate: `02feb170889007def68acd7b92cb88f41986a10e`.
The synchronous name target called the existing `StoreName` directly instead of
entering the async loop-assignment state machine; complex targets stayed shared.

| Collection | Baseline Lython | Candidate Lython | CPython context |
| --- | ---: | ---: | ---: |
| 1 | 3,179.510 µs | 3,083.098 µs | 664.056 µs |
| 2 | 3,106.049 µs | 3,111.132 µs | 582.933 µs |

The first median suggested 3.0% less time; the second showed 0.16% more time.
Baseline IQR/median was **3.7% / 5.1%**, candidate **0.5% / 2.5%**.
Collection took **6.97 / 7.09 seconds**. This does not establish a repeatable
benefit, so the production change was not merged. Both local configurations
passed 1,373 relevant checks and Linux Release passed 1,200 public checks.

## Separate compatibility correction

The safety review found pre-existing operator fallback defects, independently
reproduced through the public probe on reference delivery `561a3227` and CPython.
Declining `__iadd__` leaked `NotImplemented`; `int +=` rejected a custom reflected
operand; ordinary numeric dispatch also missed subclass reflected precedence.
Commit `af0fb29d` corrects these paths, same-type suppression and single attempts
after a subclass declines, while preserving augmented error tokens and exceptions
from in-place methods. It passed 1,115 focused Release checks, 139 focused Debug
checks and the original independent public probe now matches CPython.
The exact-integer guard used by the measured loop is unchanged by this correction.
The table identifies the measured performance candidate, rather than claiming
a separate timing measurement of the compatibility commit.

## Evidence

Raw responses, loaded identities, before/after hashes, service configuration and
terminal proofs, preparation logs and test receipts remain in ignored storage
under `.git/agent-notes/simple-loop-assignment-20261009/`,
`exact-integer-addition-20261009/` and `augmented-protocol-fallback-20261009/`.

| Artifact | SHA-256 |
| --- | --- |
| Integer addition receipt 1 | `31effdf566b2a6d674d3e7f59ec3d274e80d30349c6d442e8291a7c48fbe8175` |
| Integer addition receipt 2 | `10668be6bcae899be1a9be0b957150b43cffa5f07c44a87c2b9c80b2ccf36659` |
| Loop-name receipt 1 | `db212e3ae9eafc2da125593f1cd11d153e698cf259a9903b03e484df8087fe88` |
| Loop-name receipt 2 | `e189867f890ed316d0c94ba7ec4718536928bfddd12ed2cb8b4c800ee71c91e9` |

# One compiler instruction store

R424/B820, 2026-11-16 UTC. Retain **9d26d408** versus committed
**e6500781 / eee74a10**. The original staging record says reference full CI pending;
later qualification does not rewrite that history. Only the core differs between
copied, fully hashed images; workers, released 0.4 adapter and auxiliaries are unchanged.

Each single-use compiler block now keeps its original instruction list through
target normalization, call-result preparation, operand forwarding and physical-slot
layout. Forwarding compacts the list and removes its dead suffix in place. Only
final linearization allocates the retained instruction tape. Deletes `BasicBlockBuilder`,
`ExecutableBasicBlock`, normalized instruction arrays and shortened replacement arrays.
No marshal/unsafe access, new storage framework, runtime ABI, public API or policy.
The rough compilation admission estimate stays conservative and unchanged.

Final instruction backing fields (including original spans), targets, exception
regions, entry/operand slots, cold read spans, local/closure and call metadata agree
with baseline for all four complete sources and recursively defined functions.
That diagnostic establishes equal known code, not arbitrary Python equivalence or
a CPU speedup. Existing externally observable tests remain the semantic authority.

## Complete compilation and execution

Quiet Azure F4as_v7/Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap;
.NET 11 RC3, ordinary tiering and natural workstation GC. Existing checked workers,
two processes/source, full-request warmups (8 seconds compilation, 5 otherwise),
five rotated approximately 0.5-second batches. Four jobs complete in 162.446 seconds.

The existing six-helper file manifest is representative. The generated 96-field,
23,531-byte configuration normalizer is an explicit compilation diagnostic, not
evidence that agents routinely produce that shape. Existing invoice/JSON controls
and independent fixtures/goldens are reused without modification.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | ---: |
| Manifest / compilation | −3.53..+2.58% | −2.61..+5.41% | 2.15/4.10% |
| Generated normalizer / compilation | −3.75..−1.28% | −4.18..−2.00% | 1.84/0.72% |
| Generated normalizer / compile-to-result | −2.81..+0.82% | −3.77..+1.70% | 3.11/0.60% |
| Ordinary invoice / warm control | −2.28..−0.33% | −1.33..−0.72% | 0.95/1.05% |

All four process-median pairings; positive means slower, not confidence intervals.
Generated compilation has modest supporting CPU evidence. Manifest and fresh
timings are inconclusive, including the adverse pairings; accept that debt for
the simpler storage. Runtime instructions agree, so the favorable invoice control
is not attributed to this source change. No substantial whole-script CPU gain,
causal isolated share or recovery of historical regressions is claimed.

Short local natural-GC counters independently show **3.35–4.74% less allocation
per complete compilation**: manifest 435,312 → 417,304 B; generated normalizer
4,768,776 → 4,568,064 B; invoice 234,632 → 226,776 B; JSON 130,496 → 124,304 B.
Complete compile-to-result allocations fall 3.13%, 3.88%, 0.26% and 0.29% respectively.
These counters establish allocation, not live heap/RSS or latency.

Compilation timing includes public compilation and compiled-owner disposal, no
guest I/O; complete execution goldens are checked separately. Compile-to-result
includes compilation, one run, mediated RAM I/O, stdout/file validation and prior-
result/compiled-owner cleanup. Warm excludes compilation/startup, reset/protocol
and final cache teardown. Wider CPU includes reset/protocol. Local fresh allocation
also includes reset/goldens/result cleanup. No startup, pending-I/O, contention,
embedding-tail or CPython timing comparison is established by this source pair.

## Validation and delivery

Nonincremental Release solution build passes. Focused **151 internal / 1,011 public**
checks pass on actual .NET 11 and 10: lowering/control flow, store/exception spans,
annotations, lexical scopes, generators, protected helpers, real async cleanup,
cancellation, terminal refusal and compiled/result ownership. No architecture-only
replacement tests, test hooks or runtime counters. All four complete source/output/
file goldens pass on baseline and candidate; compilation and fresh owners return
to zero. Existing independent fixture/CPython evidence is reused rather than rerun.

All image/golden/budget/quiet/Closed/exit/join/archive gates pass. Persistent raw
archive is hash-verified under the lease before teardown; no remaining users/RAM.
Only two exact archived 8f468f0a replicas were retired after committed archive
custody and fresh UID/path/hash/link/all-alias proof. Current eee/9d images and
toolchains remain; 254/252 MiB stage/archive floors unchanged. The original first
build missed one old member access and failed explicitly; corrected source then
received the full nonincremental build/checks/freeze. Original condition retained.

**Full platform/stress/installed-package qualification pending; publication held.**
The broader performance objective remains active; this is a storage simplification
with modest diagnostic CPU evidence. [All medians](summary.json),
[allocation](allocation-summary.json), [code comparison](equivalence.json),
[focused checks](focused-checks.json), [identity](identity.json),
[raw](observations.tgz), [source/binaries/procedures](source-checks-and-procedure.tar.xz).

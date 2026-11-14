# Synchronous helper activation entry — withdrawn version

R364/B750, 2026-11-14 UTC. Frozen `3b7487cc` versus committed `fa40d674` /
frozen `73a1b233`, identical algorithms, one production input of582 changed.
The [representative source gate](../2026-11-14-representative-call-cost/README.md)
selects a concrete activation stage: combine fixed values/binding with execution
and transfer cleanup, delete separate StartInline and nullable bound-descriptor
handoff. One entry uses a supplied workspace or its fixed managed buffer. No new
controller, pool, representation, compiler pass, owner or public API.

666 focused checks actual .NET11/10 pass, including binding/default mutation,
protocols/receivers, aliases, actual pending I/O, first refusal, cancellation,
ownership cleanup and managed stack.100 isolated CPython3.13.2 controls match
across both APIs.18 fixed-worker goldens and budget reset/close checks pass.
Separate fresh-process allocation is effectively unchanged for search, invoice
and observed helpers. Architecture simplification alone does not establish speed.

## Complete-script comparison

Quiet F4as_v7 /Ubuntu24.04.4 /four EPYC9V45 /16GiB/no swap /.NET11RC3,
normal workstation GC/tiering, fixed workers/adapter0.4. Four fresh processes/job,
five-second full-request warmups (12text), five rotating0.5-second batches.
B750 completes eight jobs in301.846s/all image/catalog/golden/file/budget/quiet/
awaited-cleanup/persistent-archive gates. No remaining users or fixtures.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Early-exit file search |−3.79..+11.88%|−6.02..+9.71%|
| CSV invoice, warm |+1.93..+4.21%|+1.09..+3.08%|
| CSV invoice, compile-to-result |+1.51..+6.76%|+2.00..+8.47%|
| Captured text |−0.20..+3.69%|+1.46..+4.13%|
| JSON helper aggregation |+1.30..+2.97%|+2.16..+3.48%|
| 100,000-add counter |−0.04..+0.52%|−0.80..+2.00%|
| Short/zero helpers |−2.91..+3.73%|−2.51..+4.40%|
| Helpers exposing intermediate values |+0.12..+2.73%|−1.25..+5.31%|

All process-median pairings, not confidence intervals. Search unchanged reference
spread6.54% and candidate9.15% make it inconclusive; fresh invoice candidate5.07%,
short reference3.73%, text candidate3.19% also limit magnitude precision. Stable
warm invoice and JSON adverse CPU/elapsed directions remain. Keep every point.
Warm includes mediated RAM I/O, stdout/file validation and result cleanup;
excludes compile/startup, reset/protocol and final cache disposal. Fresh adds
compile/disposal; wider CPU includes reset/protocol. No external/cumulative gain.

## Decision and removed prototype

Withdraw this complete version: stable representative adversity, no allocation
saving or demonstrated CPU gain do not earn its simpler source shape. No rigid
percentage veto, permanent ban on activation consolidation or inference that
unstable search means a failed architecture. One source refinement would need
new evidence, not another unchanged retime. The combined method adds a fixed
values buffer to owned entries and widens the entry ABI; these are source-level
tradeoffs, **not demonstrated causal CPU attribution**. The old split boundary
already separates stack-backed binding from large owned execution effectively.

Restore all582 original production inputs, force nonincremental builds before
focused restored checks:74 actual11/10 executions pass (23internal+14public each).
The entire one-entry prototype and altered signature are removed. No dormant
backend/option, new compatibility scaffolding or architecture-only test survives.
The fully qualified fa40/73a range source remains current; no unchanged full
qualification is repeated. Future native operand/ownership redesign remains open
with alias/identity proof and explicit completion/debt criteria. Publication held.

RawSHA256 `17a8cf3241a7b7fe0e1222c2a1b36ca186ed942d77ea0c601d9846e51b74a6b8`.
Evidence is independently verified under the owned collection lease; current
images/catalog/toolchains and254/252MiBfloors preserved, no deletions. Early local
observation guard refuses before any remote query; original conditions survive.

[Timing](timing-summary.json), [allocation](allocation-summary.json),
[identity](candidate-identity.json), [focused checks](focused-checks.json),
[restoration proof](restoration-source-proof.json), [restored checks](restoration-checks.json),
[all sources/binary/raw/tools/conditions](sources-checks-and-observations.tar.xz).

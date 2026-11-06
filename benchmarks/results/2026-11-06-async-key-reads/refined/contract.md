# Q02 completion-path refinement after B334; before source edits

B33486.01s/three complete jobs/all gates is terminal, archived/hash-verified:
cc711a7369a1e45f62a7defa0b7fd17f8272276957320ce1892e9a2f9b6682a0.
Async text2.813/2.733ms versus2.647ms: +6.29/+3.25%,2.86% identical-role spread.
Async CSV5.563/6.412 versus5.501ms: +1.12/+16.56%,15.27% role spread; inconclusive.
Sync text2.334/2.311 versus2.320ms: +0.57/-0.40%, roughly flat. Preserve all raw
conditions. No allocation/causalCPU claim; no primary-table refresh.

The prototype's async query passes completed native hashes through a separate
mixed-hash ValueTask adapter, completed/pending wrapper, prepared-read dispatcher
and contextual helper. Actual source demonstrates redundant completion transport;
timing does not isolate that as the cause. Consolidate into TryGetKeyAsync using
one ordinary async ValueTask method. Inline the mixing and native/bridge/contextual
branches; delete PrepareQueryKeyAsync, AwaitPreparedKeyAsync, ReadPreparedKeyAsync
and ReadContextualKeyAsync. No new native-type whitelist, policy, cache, dispatch
framework or option. Keep direct synchronous Q01 walk untouched, ordered async
preparation, current-entry revalidation, token/first-refusal checks and funded
candidate snapshots through actual users/cleanup.

Focused key semantics and actual pending/cancellation/refusal/retained-lifetime
checks on both runtimes before freezing. Reuse exactly the same three complete
jobs, two identical roles/reference51db4209, ordinaryGC/tiering/full goldens,
quiet/lease/Closed/finalfunding/joins/fixtures and130s owned supervisor. One bounded
confirmation; preserve B334 alongside it. Decide remaining regression debt
explicitly against the usable async capability and removed detours. No claim that
new hashing support fixes the synchronous CPython gap or covers mutations/caches.

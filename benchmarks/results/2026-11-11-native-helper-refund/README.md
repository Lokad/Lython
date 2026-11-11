# Native helper cleanup funding — 2026-11-11

Correction `8314c02d`, above fully qualified `fe6648b2`. A real compiled
`value.lower()` helper admits its result while the host reenters and leaves a
smaller idle workspace. Returning the outer workspace evicts that idle storage;
if the refund callback refuses admission and throws, the previous catch returns
the outer workspace again. Both APIs refund 160 bytes still retained by the cache:
the regression expects 8,737 accounted bytes and observes 8,577 before the fix.

One `finally` per API now relinquishes argument storage once. Frame cleanup and
workspace return retain their own `finally` ordering; native success, entry
failure, actual async suspension and body exceptions keep their existing behavior.
The change removes 49 production lines and adds 30. No public API, new owner,
configuration or performance claim.

The [original failure](before.log), focused internal/public logs on both targets,
[source/test patch](source.patch.gz) and [outcome](outcome.json) preserve the proof.
All 960 focused checks pass across actual .NET 11/10, including live/cache funding,
first refusal, retained input, frames, defaults, exception context, cancellation,
awaited cleanup and shared-budget reuse. Subsequent combined runtime `b5849b04`
passes full Windows/Ubuntu, actual .NET 11/10, original stress and installed
Release consumers in [run 43033830025](https://github.com/Lokad/Lython/actions/runs/43033830025).
The [recorded result](delivery-ci-status.json) covers this correction; original
outcome/logs preserve the earlier collection-time qualification status.

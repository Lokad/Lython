# Defer unused module-cache construction, 2026-10-19 UTC

The existing module member dictionary is initialized on its first ordinary lookup
through a managed BCL initializer. Script modules and builtin `__name__` lookups
already bypass this dictionary; they no longer allocate an unused table. The
small name value remains eager. StringComparer.Ordinal, per-module cache identity
and concurrent publication remain. No cache framework, owner or option is added.

Baseline: `026e8bbd18434c14f9daa4e4c52a3b3788dc0cbd`. Candidate: that revision plus
[the one-file patch](candidate.patch). These are frozen source snapshots, not a
measurement of whichever commit is currently HEAD. Production sources compile
into a private diagnostic assembly as they do in the existing white-box suite.

| Operation | Before allocation | After allocation | Saved |
| --- | ---: | ---: | ---: |
| Script-module construction | 1,648 B | 200 B | 1,448 B |
| Builtin construction + name only | 1,600 B | 152 B | 1,448 B |
| Builtin construction + first ordinary member | 1,672 B | 1,672 B | 0 B |
| Warm builtin cached-member lookup | 0 B | 0 B | 0 B |

Eight samples, 64 calls each, after 16 warmup calls. The context and builtin warm
control are prepared outside the counters. Each row ends its shared owner and
verifies zero root reservations. These are current-thread cumulative managed
allocation measurements; they establish neither retained heap/RSS nor a new
runtime accounting policy. Coarse module ownership and admission remain unchanged.
No forced GC is used. Full raw byte/tick arrays are preserved; the tiny elapsed
samples are unqualified and establish no latency, embedding tail-latency or
CPython gain. Startup, compilation, public projection and host I/O are excluded.

Windows10.0.26200 X64, i7-14700KF / 28 logical processors; .NET10.0.13,
SDK10.0.300-preview.0.26177.108. Existing concurrent identity, import namespace,
cache ownership, admission/refusal and complete-job regressions are reused.
Small follow-up changes are grouped before full delivery qualification; exact
qualification status is recorded in the private plan.

[Scope](measurement-scope.json), [before](before.json), [after](after.json),
[summary](summary.json), [diagnostic](diagnostic.cs), [project](diagnostic.csproj),
[baseline identity](baseline-source.json), [candidate identity](candidate-source.json).

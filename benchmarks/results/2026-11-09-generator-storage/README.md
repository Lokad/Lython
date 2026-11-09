# Shared generator compilation and retained state

R142/B500, 2026-11-09. Fresh committed `7aa637bf` core `d1c93196` versus
working core `2ce8dbc1`. Identical frozen worker/adapter/dependencies on both sides.
The original identity guard refused differing rebuilt auxiliaries before sampling;
that refusal and all rebuilt hashes remain in preparation evidence.

Generator definitions now use enclosing executable compilation, carrying known
lexical captures and globals. Remove their eager isolated code object, blanket
mirroring of complete scopes, argument-dictionary-to-slots phase and generator-only
locals/cell initializer. Retained arguments copy directly into admitted private
storage before the binding array returns. Class suites prepare ordinary/generator
methods once and discard their unused lowered bodies. Incomplete class/type scopes,
lowered class construction and generator lambdas retain their needed bridges.
No new execution backend, carrier, cache, memory policy or public option.

This exposed cell mutations that depended on mirrored namespaces: nonlocal writes
and deletion after the defining workspace detached. Capture delete-only nonlocals
with known lexical parents; update their actual cells and synchronize live defining
slots. Class construction still requests its required namespace bridge. Unsupported
generator lowering keeps its Python-shaped diagnostic at the later compilation phase.

Three complete CSV/text/JSON generator-pipeline variants use existing representative
inputs and output goldens. Unchanged report/search scripts are controls. All five
stdout/file results are independently checked with isolated CPython 3.13; these are
same-script Lython source pairs, not CPython speed ratios or shape comparisons.

| Complete warm script | Actual allocation, both APIs | Quiet elapsed | Wider process CPU |
| --- | ---: | ---: | ---: |
| CSV generator pipeline | approximately unchanged | −5.82–10.95% | −4.37–12.95% |
| Text generator per input line | −15.11–15.12% | −11.60–12.08% | −11.11% |
| JSON-lines generator pipeline | approximately unchanged | −6.17–6.28% | −5.56% |
| Unchanged report control | approximately unchanged | **+1.17–4.54%** | **+0.39–4.44%** |

Four independent roles, five-second warmups, seven rotated 150ms rounds, normal
GC/tiering on the quiet F4as_v7/.NET 11 RC3. Four jobs/114.323s. Ranges span both
candidate process medians against both references, not confidence intervals.
CSV reference spread 5.24% prevents a stable improvement magnitude; all affected
elapsed/CPU pairs favor the candidate. Report reference spread 1.31% and candidate
1.94% do not establish neutrality. Retain that unresolved regression debt.

The desktop diagnostic includes adverse JSON elapsed +6.39–8.03% and report/search
control drift up 17.8%. Quiet JSON does not reproduce that regression; its original
conditions remain evidence. Compile allocation **increases 1.31–1.64%** for the
affected variants; compile CPU/startup remain unmeasured. Retain the coherent phase
deletion and affected benefits, accepting this explicit debt rather than claiming
universal improvement. No unchanged confirmation replay is selected.

Warm clocks include mediated RAM-file I/O, full output validation and prior-result
cleanup; exclude compilation, startup, fixture reset, protocol and final cache
teardown. Wider process CPU is separate. Quiet comparisons use synchronous execution;
local completed-I/O async allocation does not qualify pending-I/O latency.

After freezing the measured candidate, delivery restores the original intermediate
compile cancellation/refusal checks and removes the now-unused binding initializer
with only white-box callers. Exact changed source hashes are recorded. This does not
claim timings for the final binary. Final focused checks per actual .NET 11/10:
184 internal, 628 public, and a separate 37 compiler-cancellation/diagnostic checks.
Eight new independent Python goldens cover live/deleted/transitive captures,
suspended locals, argument/default lifetimes, class barriers and generator lambdas.
Existing genuinely pending protocols, first refusal/cancellation, send/throw/close,
awaited cleanup, alias funding, managed depths and shared-budget reuse remain.
Raw execution fixtures now enter the executable engine and its native terminal
cleanup boundary; semantic/ownership assertions are retained.

All identity/golden/quiet/Closed/join/RAM-unmount/archive-under-lease/local-SHA gates
pass. Archive SHA256 `b95996262de325066c09a689790e705a5ea9a16b5cbea76e0a6ef5a69260e759`.
Terminal cached; no remaining users/fixtures. No disk-space recovery or floor change.
Full delivery platform/Stress/package checks batch after commit; publication postponed.

[Quiet summary](summary.json), [raw observations and exact cores/tooling](observations.tgz),
[desktop adversity](local-summary.json), [compile allocation](compile-allocation.json),
[delivery source differences and focused checks](delivery-source.json),
[independent pipeline goldens](pipeline-goldens.json), [identities](identity.json),
[terminal receipt](terminal.json), [preparation, failures, patches and logs](preparation.tar.xz).

# Native specified-string formatting: unqualified and withdrawn

2026-11-02, source base `c3a0eb1a`, runtime baseline `4cae38f4`. **No runtime
prototype or claimed speedup remains.** The entire formatting/shared-padding/
completion-guard change was restored, including the casing helper's original
name and behavior. Forty independent semantic/lifetime regressions remain.

The candidate replaced the subject UTF-8 → CLR string → truncation/padding → UTF-8
pipeline with a native scalar-precision/padding kernel shared by the existing
formatting entry points. Numeric/date/custom formatting and parser/error order
stayed. Existing native string padding shared the governed builder and final
unpublished-output check. This removed the CLR string branch but retained native
builder working storage and its final output copy.

## Frozen comparison outcome

Use the maintained micro command once per job: complete warm report rendering
and an unchanged JSON-helper control, with CPython context. Dedicated Ubuntu
24.04.4 Azure F4as_v7 / .NET 11 RC3 / CPython 3.13.16; ordinary worker GC/tiering.
The benchmark/adapter/auxiliary files are byte-identical; only core differs.
Workers run in separate processes, so process hash seeds are not shared. All
stdout/file goldens, input identities, four quiet gates, joined exits and fixture
cleanup pass. Services finish normally in **9.604 / 9.491 seconds**, below their
30-second bounds. No profiler, rerun or extra timing campaign.

| Complete warm job | Original | Candidate | CPython context | Original / candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| Report rendering, 3,072 records | 10.485 ms | 9.364 ms | 2.709 ms | 28.75% / 18.14% |
| JSON helpers, unchanged control | 7.681 ms | 7.903 ms | 4.316 ms | 5.10% / 3.29% |

Report's observed 10.69% lower point median exceeds the frozen 7.5% requirement,
but both Lython spreads exceed its 15% limit. The +2.89% JSON point change meets
the 5% control limit. **The trial fails its declared decision and is not retained.**
These short diagnostics do not establish a repeatable gain or cause of the spread.
Do not rescue the result by dropping rounds, retiming or relaxing its gate.

Warm timing includes fixture reset/staging, mediated file I/O, complete output
checks and prior-result cleanup. Startup/compilation are excluded. The final
result and cached compilation end at joined worker shutdown. The receipts prove
process/fixture cleanup; finite allowance reuse and terminal cancellation/refusal
are checked separately by the focused suites, not inferred from these clocks.

## Semantics and restoration

Candidate and restored source both pass **167 internal / 683 public focused
Release cases per actual Windows .NET 11 RC3 / .NET 10.0.8 runtime**. Thirty-six
independent CPython goldens cover Unicode precision/fill/alignment, odd centering,
zero padding, errors and format()/str.format/f-strings; they pass on the VM's
CPython too. Four native execution cases preserve target/input aliases and stop
post-format effects on final-funding refusal/cancellation, join cleanup and reuse
the whole allowance. Existing async protocol, ownership and casing controls stay.

Restoration is byte-for-byte, followed by a fresh nonincremental solution build
and the focused checks above. No architecture-only tests remain. Initial build
setup used the wrong solution filename, then a new fixture's dictionary had a
nullability mismatch; both failure logs are preserved. Runtime assertions,
budgets and timeouts were not relaxed.

[Frozen design/decision](pre-code-plan.md), [exact images](candidate-identity.json),
[all observations and failed gate](assessment.json),
[raw receipts, exact candidate patch and collector](evidence.tar.gz),
[independent goldens](semantic-goldens.json), [restoration](restoration.json),
[candidate/restored validation and setup failures](validation-logs.tar.gz).

The native builder still constructs working storage and copies it into a second
result buffer. A future direct-output design must remove that distinct allocation
stage and measure its whole-job allocation effect with controls. It is a larger
ownership/construction change, not an unchanged replay of this conversion trial.

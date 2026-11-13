# Original metadata delivery failure

Committed157a338f/frozen7ee65002 fails
[CI43443394333](https://github.com/Lokad/Lython/actions/runs/43443394333) on both
Windows and Ubuntu. Per platform,4681white-box cases pass; public17500/17501pass.
The same existing `SharedFixedLabelBehaviorTests.FactoryNewSlots` assertion fails:
`partialmethod.__new__` is missing. Subsequent.NET10/stress/package-consumer steps
do not run. This source is **not fully qualified**.

A fourth inherited-slot consumer still requires a descriptor after removing
the builtin-new wrapper. Correct it through the existing canonical binder;
preserve the assertion and qualify the corrected source. The earlier focused
selection missed this existing integration case. All573frozen production inputs
match the exact failed commit with explicit newline normalization. Earlier7ee
timings remain their own observations, not corrected-source qualification.

[Terminal](ci-terminal.json),[full logs](ci-logs.zip),[failure excerpts](failures.json),
[test counts](test-counts.json),[source proof](source-proof.json).

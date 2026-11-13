# Managed native-call handoff delivery

Runtime **63389c9d**, measured frozen core **2e74ef4f**, is fully qualified by
[CI43378817838](https://github.com/Lokad/Lython/actions/runs/43378817838): Windows
and Ubuntu actual.NET11/.NET10 ordinary tests, original stress scales and installed
Release package consumers pass. Terminal status, full logs and16individual-step
count lines are cached. All571frozen production inputs match the committed source
with explicit checkout normalization. Repository-generated metadata can change
binary hashes; CI binary byte identity is not inferred from source identity.

The correction hands deeper native calls to the existing linear runner before CLR
recursion exhausts a managed continuation stack. It uses existing logical depth,
adds no guest limit/counter or native telemetry, and preserves shallow composition.
The controlled256KiB continuation reproduces2f34a48f's fatal overflow against the
qualified6b468bf1 reference. Corrected2e74ef4f recovers, supports finite100-depth
direct/mutual calls and returns shared funding after cleanup. Existing attribution,
cancellation, ownership and stack checks remain intact.

Older R303 CPU/allocation measurements retain their original source labels. This
correction claims no speedup; the bounded current-source external checkpoint is
next. No unchanged qualification replay is required. Publication/tagging remain
postponed and the broader performance roadmap remains active.

[Terminal](ci-terminal.json), [full logs](ci-logs.zip),
[step counts](test-counts.json), [source proof](source-proof.json).

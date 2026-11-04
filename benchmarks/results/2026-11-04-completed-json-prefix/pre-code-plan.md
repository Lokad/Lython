# D13 shared completed JSON prefix

Before source changes,2026-11-04. Baseline runtime5c827e68/frozen21cddae7;
current doc8c63fe63. D12 actual report JSON31.5% inclusive/container state-machine
5.72% leaf. Magnitude is unproven; no fixed percentage gate. B137/B176 explicit
parent arrays/continuation workspaces measured adverse/flat and remain withdrawn.

Hypothesis: keep the existing recursive grammar and dict/list/key-snapshot owners,
but run its ordinary completed loop in a non-async method. Materialize async
continuation state only for an actually incomplete child/hook. Delete the ordinary
ReadJsonContainerAsync generated state machine/awaiter/builder initialization on
every object/array. No new parser, explicit parent array, owner, cache, quota,
backend, mode or eligibility special case. A tiny completion helper resumes the
same loop after the single pending child has really finished. All options/keys/
containers stay on the same execution owner; expected-name snapshots are immutable.
Never retry work or refund a retained/pending user.

Acceptance: existing full/raw-prefix/error/Unicode/duplicate/order tests; object/
pairs/numeric/custom hooks including real async/reentry, nested containers, retained
aliases, cancellation during parsing/callbacks, first refusal through suppression,
awaited cleanup/shared-budget baseline on both APIs/actual11/10. Keep64-level data
errors and managed stack policy. Reuse focused classes, no architecture-only tests.
Run the existing affected JSON semantics/ownership families and native parsing tests.

Freeze a nonincremental Release build after focused checks and verify all loaded
identities. One bounded complete-script pair: JSON-lines/report/config both APIs,
ordinary text control and deep/hooked controls if source review reveals a distinct
risk. Compilation unchanged; allocation/peaks separate. Existing rotated old/control
roles, usable warmup and short batches/VMquiet gates/goldens/joined cleanup. Retain
all adverse data; a concrete source/measurement defect can justify bounded refinement.
Finish by retaining/removing displaced source or restoring it, decide regression
debt explicitly, then batch full qualification only at the next coherent boundary.
No CPython rerun for this individual prototype. Publication remains held.

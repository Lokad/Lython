# R71 managed binding-descriptor transport correction

B425 ordinary CSV elapsed +0.07–5.09% / coarse CPU +6.25–11.70% remains open.
B426 unchanged-source paired attribution has 1,124 guest samples per role / 5.65
and 5.66 CPU seconds; source/actualReady/golden/quiet/Closed/join/archive gates pass.
Inclusive samples overlap and do not establish saved CPU. Windows Tier1 elides the
suspected positional descriptor copy. Actual Linux Tier1 does not: all three
BeginBoundCall call sites copy the 48-byte BoundCallArguments value to outgoing
stack storage using reference stores/movsq. This new transport was absent in R70.

Selected correction: BeginBoundCall takes its existing readonly descriptor by in
reference. Every caller owns a local descriptor through this synchronous method;
only the async binder continuation awaits, before it calls this method. The method
retains values/owners in the existing work/frame fields, never retains the reference.
No unsafe API, new storage, binding path or persistent field; no cancellation check
removed. Remove this proven added copy, not a speculative representation redesign.
No causal attribution of the entire elapsed/CPU gap or predicted percentage.

Focused final call/default/lifetime/native/recursion/refusal/cancellation and function
checks on actual11/10. Existing new sequence semantic/lifetime tests are still on
unchanged construction runtime; no sequence optimization in this source pair.
Use existing five-script diagnostics for allocation; one controlled B427 four-job
pair against qualified R70, same returned-record sync/async and ordinary CSV/text,
original B424/B425/B426 preserved. Linux generated-code acceptance: outgoing
BeginBoundCall descriptor copy disappears. Finish with explicit residual-debt
assessment, not a repeated unchanged retiming. Batch full delivery at retained
checkpoint; R71 CI42426106105 qualifies f88191f7 only and is still pending.

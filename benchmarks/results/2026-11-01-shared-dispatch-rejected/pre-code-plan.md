**B51 — one ordinary instruction loop (candidate, criteria frozen before code).**
Hypothesis: the distinct async opcode loop copies common instructions through a
large state machine and duplicates normal block/PC/checkpoint/abrupt routing.
CSV's demonstrated API gap and largest named self leaf justify testing this
transport deletion; samples do not predict a removable fraction or accepted gain.
Affected: async CSV helpers; synchronous CSV/text are unaffected controls.

Deletion/replacement:
- Delete the separate ExecuteUntilCallAsync instruction loop. Both APIs use one
  ordinary nonasync loop over existing executable code, ref-read instructions,
  same current-token/admission checkpoints and shared abrupt routing.
- Keep small API entry/cleanup wrappers. The async wrapper awaits an incomplete
  existing operation and resumes its exact PC; never execute a later instruction
  or release operands/owners before that user and required disposal finish.
- Keep genuine await-capable operation helpers, compiled caller chain, member/
  protocol resolution, native scalar paths and managed stack policy. Helpers that
  need branch completion return the existing ValueTask<bool> shape. No task queues,
  callback framework, alternate backend, options, pool or new ownership machinery.
- Share Python exception/return/control routing; synchronous and asynchronous
  resource cleanup remain appropriately different. Cleanup failures retain their
  existing routing boundary. Preserve first refusal through guest suppression.

Semantic acceptance before timing: sync/async values and Python evaluation/error
order, closures/recursion/generators/yield-from, instruction continuation after
real host/protocol suspension, pending async iterator disposal before handlers or
later guest effects, current cancellation and first refusal, retained aliases,
funded pending operations and shared budget reuse. Keep borrowed operands alive
until completion; no abandoned work. Existing focused Release regressions plus
independent continuation/lifetime cases where coverage is missing, both actual
TFMs; fresh nonincremental build and equal core/worker/test/probe copies.

One before/candidate/identical-before collection, same frozen scripts/inputs,
auxiliary workers and runtime/tiering. Nine processes: warm-async CSV, warm CSV,
warm text per role;4warmups/7batches64 complete jobs, <=60s total/30s per child.
Quiet/full goldens/balances/joins; CPU batches>100ms, <=5% CPU and elapsed /0.2%
allocation control drift per case. Retain only >=7.5% complete CPU AND >=3%
elapsed improvement on async CSV against BOTH controls, with <=5% CPU/elapsed
regression on each synchronous control against both. No allocation-only speedup,
phase subtraction, retiming, tuning or relaxed bounds. If failed/inconclusive,
remove prototype, preserve evidence and independent useful tests. Bounded design
stops after this candidate/one comparison. Full platform/stress/package only for
retained production code at the next coherent delivery boundary.

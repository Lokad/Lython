# One justified B106 refinement

The first prototype retains all semantics/ownership checks and reduces complete text-helper CPU8.17-10.65%/elapsed6.82-7.84% across both originals in two blocks. Uppercase CPU consistently rises2.61-4.52%, about16-19 microseconds/job; elapsed is noisy/adverse in the second block. JSON control drifts around the originals. All first samples/patch/frozen conditions stay untouched.

Source identifies unnecessary prefix/following Span construction and a larger argument packet on EVERY rune in the new shared mapper. Only Sigma needs those windows. Pass the already-borrowed whole subject plus scalar byte offset to AppendMappedRune, and slice only inside its Sigma branch. This removes work inherent in the consolidated design without adding a special upper/lower backend or mode. UTF-8 Sigma width is obtained from the decoded Rune. Same cancellation/builder/output/refund/Unicode contracts, no new ownership.

One refinement, same focused tests and two-block whole-script comparison using unchanged baseline/auxiliary bits/driver/catalog on the quiet VM. Preserve original uppercase regression debt. If the residual cost remains small, explicitly accept it for the43-line net simplification and whole text gain, or remove the candidate if broader regressions undermine it. No open tuning campaign or automatic threshold veto.

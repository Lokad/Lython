# Final D53 construction contract, before collection

B279 first block140.32s/all gates is text-control limited3.60% spread. B280
refinement113.00s/all gates has mixedUnicode−4.74/−6.50% but actual unfused-control
+3.69/+2.76%, with.90% current spread. Preserve all results, including unchanged
CSV+2.15/+.71% and standard-text−.84/+4.38% (5.27% current spread). This does not
establish a general gain or permanently reject one-pass cased construction.

Final revision follows the real contracts: ordinary forward/reverse splitting
has its original scalar loops and bounded maxsplit semantics. The already
existing cased split is unbounded and gets a collector that derives conservative
capacity from decoded widths, without optional-casing/maxsplit work per scalar.
Range storage, final output/admission and all Unicode writers remain shared.
One representation guard serves standalone and fused sizing. This introduces
no guest fast path, mode, cache, owner, string fields or execution backend.
The cased/uncased transitions share a little scan structure; that source
duplication is the explicit tradeoff for keeping ordinary scalar work unchanged.

Final<=120s three-job same-reference/current-control/CPython decision: standard
text, mixedUnicode text, unfused split control. Seven~150ms rotated samples after
5s engine/job warmup, all prior identity/quiet/golden/Closed/finalzero/join gates.
Both earlier blocks and precollection disk guard/verified duplicate-upload cleanup
remain. Decide retain/withdraw for this contract, with explicit residual debt.
No further isolated refinement/retiming is scheduled; materially new evidence
can motivate a later complete milestone. No fixed percentage veto or claim that
elapsed proves allocated bytes, causalCPU, portable speedup or embedding latency.

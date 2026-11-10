# Confirmed instruction source gate

The maintained CSV update is call39 -> binary40(Add) -> store41(StoreSubscript).
Callresult16, binaryfirst16/secondlocal10(amount), storeresult/live16, receiverlocal2
(totals), keylocal9(kind). Text is call28 -> binary29(Add) -> store30(StoreSubscript):
callresult13, binaryfirst13/secondconstant−3(1), storeresult/live13, receiverlocal1
(counts), keylocal7(word). Both calls have native receiver and2positional arguments
(key,fallback0). Complete JSON instruction captures retained; no permanent architecture
test. Initial capture read guarded ThirdInput on Import and failed locally; correct
opcode-specific getters, rerun passes1inspection case. No runtime defect or semantic
assertion change. Tool source/failed and corrected logs retained outside production.

This is an actual adjacent existing-engine region, not an assumed compiler shape.
Prove same protected exception region, source/result/live positions, pure local/
constant increment and store receiver/key before annotating the callsite. Runtime
must check actual ReceiverMethods.DictionaryGet, exact native string/dictionary
shape, no protocol keys, same funded owner/capacity and small exact integer result;
check reference identity of original receiver/key against assignment locals. Do not
read a missing increment before original Python evaluation; guard without effects
and let the existing path report its original span. No major admission hook may run
between speculative/native query and publication. Check actual AddExactIntegers
preflight before selecting it. Growth/wide/dynamic/default/method shadows remain
on the existing path before effects. Retire only plain call inputs, preserve live
outer iterators and async disposal/store continuation. Keep call admission and
explicit call/arithmetic/store cancellation/failure spans. No second query on
success; false must have no guest callback, mutation or admission.

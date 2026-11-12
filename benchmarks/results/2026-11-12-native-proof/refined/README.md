# Linear native proof refinement — withdrawn

2026-11-12. Same qualified reference `2ec9d67f` / frozen `c9829194` as the first
form. The refinement removes the per-node CopyInputs runtime call, index/shape
packing vectors and loops. It reads actual operands into local enum shapes;
compiler postorder validation and the bounded runtime shape stack remain.

**1,646 internal/333 public focused checks pass per actual .NET 11/10**. All six
unchanged scripts through both APIs and separate compilation pass complete stdout,
files, balances and joined cleanup. Allocation is flat. No public API/ownership/
cancellation or stack policy change is selected.

Desktop capturedRun elapsed **+7.90..+9.45%**, unchanged control1.44%; capturedAsync
**+3.08..+3.56%**, control0.47%. Search/report/other controls are variable. Search
compile+31.90..+40.95% with6.42%control is also preserved; the unchanged compiler
implementation does not establish a cause. All raw phases and allocation results
remain separate from CPU conclusions. No quiet refinement comparison is claimed.

Withdraw this form and the initial linear proof. There is no per-call allocation
benefit, while compiler validation/capacity facts and runtime shape-stack machinery
add complexity. Available adverse evidence does not justify retaining that tradeoff
or another VM collection. A timeout or fixed threshold does not decide it: the
first form's collector failure remains an incomplete measurement, not a failed
design label. [Explicit decision](../decision.json). Future materially different,
source-backed designs remain open; this is not a permanent native-proof veto.

The exact qualified source is restored, removing CopyInputs, GuardStackCapacity,
the new order validator and shape-stack evaluator. Existing recursive guard and
format helpers remain. Restoration requires nonincremental builds before no-build
checks; its focused results will be recorded separately. No repeat full unchanged-
source delivery cycle or publication is started.

[Identity](diagnostic-identity.json), [before](sources-before.tar.xz),
[candidate](sources-candidate.tar.xz), [candidate images](candidate-images.tar.xz),
[checks](focused-checks.tar.xz), [preparation](preparation.tar.xz),
[archive identity](archive-identity.json). Earlier first-form adversity, incomplete
quiet data, original collector/receipt mistakes and joined recovery remain intact.

# B203 direct list backing - design before code

Parent0e516365 production matchesffc01134, frozen71879656. B200/B201 full delivery
41788586642 running independently. B202 CPython checkpoint complete277.633s:
five full scripts warm/fresh plus CSV/report compile, exact I/O/cleanup boundary,
goldens/gates/balances/joins. Text fresh2.49x/warm variable2.85x remains the largest
gap. Do not repeat unchanged comparisons or infer CPU shares from allocation.

Current source: PyList -> IPyListStorage -> SmallPyListStorage -> object[8], or
PyList -> ArrayPyListStorage -> List<object> -> object[]. Both storage classes
own count/capacity/funding and duplicate mutation operations. Every list index,
count and append traverses the interface. The small class already uses an array;
it is not an inline optimization. Sorting is the only external production user
of a detached storage identity. Existing profiles include list construction,
iteration, copies and dispatch, but do not establish all of it as a CPU share.

Selected coherent redesign: PyList owns object[], count and committed capacity
directly. Delete IPyListStorage, both implementations and the promotion/factory
dispatcher (entire PyListStorage.cs). Move the rough capacity estimate to PyList.
No new storage backend, pool, per-item ledger, owner layer or public switch.
For ordinary lists remove one allocation, plus CLR List<object> for larger lists;
eliminate interface dispatch and duplicate list mutation/capacity implementations.
Do not replace removed machinery with another universal collection framework.

Use one initial capacity8 and checked geometric growth. Known large capacity
admits actual requested storage, unknown sources grow with overlap preflight.
Tradeoff: first growth8->16 replaces the former promotion8->9 for a single append;
document conservative capacity/earlier refusal. Tiny identical thresholds are not
a contract; live backing and replacement peaks remain funded. Preserve all
externally observable failure/side-effect guarantees rather than loosen tests.

Detach sort backing as a small value snapshot (array/count/charge), funded until
restoration. Existing sort consumes an ArraySegment rather than a new backend;
callbacks still see empty list, appends/removes detected by retained capacity,
finally restores the full permutation even after cancellation/refusal. No ordinary
mutation counters or owner identity registry. Genuine async sort awaits all users.

Preserve Python live iteration/self-extension/slices, alias identity, constructor
abort refund, failed live extension's successful prefix, clear/replacement
preflight before mutation, shared-budget reuse/retained results and first refusal.
No backing capacity growth outside the common preflight. Cancellation checkpoints
in long copies/iteration remain at least as effective as current managed bulk
operations. Fully managed safety/stack/host capabilities unchanged.

Focused Release actual11/10: list capacity/construction/extension/storage,
slice/repetition/mutation, sort callbacks/reentry/real suspension, cancellation,
shared allowance/refusal/aliases/cleanup and representative streaming. Translate
architecture-only tests to PyList semantic assertions; retain independent failure
coverage. Add tests only for actual gaps. Nonincremental build before capture.

One short whole-script pair with immutable old/candidate/old, current workers,
text and config lists plus CSV/report controls, both APIs, compilation separately
where affected; separate allocation/elapsed/admitted peaks. Existing quiet VM
protocol, credible warmup and retained adverse samples; bounded correction if
identified uncertainty justifies it. No fixed percentage veto. Decide retained
simplification/regression debt at completion; batch full delivery there. Do not
claim that this milestone finishes the overall performance objective.


Capture refinement before measurement:4 scripts/text+config+CSV+report both APIs,
8 jobs,3 physical images old/candidate/old,6s warmup,9 rotated~200ms samples.
Existing8-case three-image allocation driver both APIs covers JSON as well.
No frontend changed; compile/startup excluded rather than repeating unaffected
compilation qualification. Existing RAM guest fixtures/persistent evidence,
420s owned service bound, normal GC/tiering. B201 full delivery running separately;
no claim of current prototype full qualification. Actual410 internal/1701 public
each.NET11/10 PASS; original pilot410/648 pass. No new failures/timeouts relaxed.
Odd-period9003-element in-place repetition golden independently CPython3.13.2;
prefix fill copies in bounded chunks with active context forwarded for cancellation.

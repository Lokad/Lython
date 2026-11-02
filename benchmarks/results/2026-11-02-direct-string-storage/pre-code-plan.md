# B94 admitted final string storage — before code

Source base aa16828b; exact fresh restored baseline in baseline-identity.json.
B93 is closed and its timing/variance outcome is not retested or relaxed. This
larger design removes a distinct allocation stage: padded/truncated native string
results are built in their exact final array, not a governed builder working array
followed by ToPyString copying. The same primitive serves specified string formats
and str.ljust/rjust/center. Source retains validated parser/error/protocol behavior,
numeric/complex/date CLR algorithms and existing ownership/stack/cancellation.

Expected deletion: subject AsString and CLR precision/padding/re-encoding branch;
Pad's builder construction/release, incremental AppendRepeated calls and final
copy/working-capacity lifetime. Allocate ONE exact byte array and its PyString
wrapper under one existing ReserveTemporary estimate; transfer that reservation
with existing FromOwnedUtf8. Do not introduce owners, registries, tracing, pools,
new options or a second formatter/parser. Aliases returned without construction
are never refunded. New output stays private/funded through cancellation checks.

Check actual Array.MaxLength/checked byte arithmetic before allocation; reserve
128+payload using existing conservative PyString estimate, retaining input funding.
Observe context first-refusal/cancellation after host admission and before array
creation; fill in bounded native runs and check before/after publication. Coarse
estimates become smaller because intermediate storage disappears: no public
threshold promise, hidden cap or standalone allowance changes. Preserve Unicode
scalar precision, UTF-8 multibyte/NUL fill, format^ versus str.center odd parity,
default/zero spec behavior, conversions/nested specs, errors and real async hooks.

Focused existing Release format/protocol/ownership/cancellation/casing checks on
actual .NET11/10, including retained36 Python goldens/four terminal lifetime cases.
Add only independently useful native padding/retention checks for this new path;
no exact checkpoint-count tests or relaxed containment/timeouts.

ONE bounded before/candidate/identical-before allocation diagnostic uses B92's
existing phase driver and complete report-render.large source/fixtures/goldens,
32 warmups/eight observations per reflected/public-warm/public-fresh lane. Same
current core/benchmark/adapter/auxiliary bits except candidate core, ordinary
GC/tiering, shared allowance returned to0 and actual users/fixtures gone. Use
one owned45s service; build/test/transfer before it, stop owned SDK servers, quiet
gates. Record current-thread reflected and process-wide public allocations
separately, direct compile/warm/fresh/nested host/cleanup clocks, no subtraction
of independent medians or CPU-share claims. No new profiler/framework/campaign.

Retention requires structural deletion and all semantic/ownership gates, >=5%
WHOLE public-warm managed allocation reduction against BOTH originals, <=0.5%
original allocation drift and <=1% public-warm allocation IQR/median. This is a
measured allocation benefit, not a throughput prediction. The source-directed
pipeline removal, not small numeric threshold tuning, defines the redesign.

ONE maintained micro observation of report and ONE JSON-helper.large unaffected
control,15s collections/30s owned services. Complete output/input identities and
joined cleanup, quiet gates, normal workers. Timings are diagnostic; a >10%
regression with both Lython IQR<=15% rejects the candidate. Noisy observations
cannot establish a speedup or replace the allocation gate. No retiming, changed
samples or post-result relaxation. Closed B93's elapsed gate remains closed.

On failure restore the whole production change, rebuild nonincrementally, retain
independent semantic/lifetime cases and all adverse evidence. If retained, document
construction/estimate changes and run established full Windows/Ubuntu ordinary
both targets/originalStressboth/installed Release consumers at this runtime boundary.
Do not publish/tag or change the optional adapter0.4 pin/core dependency boundary.

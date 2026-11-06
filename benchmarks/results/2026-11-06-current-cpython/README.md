# Current CPython comparison — R20/R21 checkpoint

2026-11-06. Runtime source4fad63c5, evidence/package source799895ea; measured
corec3e65ad3. Normal0.4 optional adapter/producer and b94fa98c worker remain
unchanged. Private dev46 qualification is outside these images. This refresh
measures the combined runtime; it is not a causal comparison of individual changes.

Existing five complete scripts, separate compilation/disposal, warm execution,
and compile-to-result. CPython3.13.16 plus two independent identical current roles,
normal GC/tiering, five-second warmups and seven rotated150ms rounds. All15jobs
complete312.23s. Full output/file goldens, terminal balances, Closed/joins, quiet
endpoints, fixture cleanup and persistent under-lease archive verification pass.
No SSH during collection; all users are terminal. Exact identities, scripts,
host/worker protocol, raw rounds and command history remain in the archives.

Fresh Lython/CPython: CSV1.25–1.26x, text1.77–1.79x, JSON1.08–1.11x,
report1.50–1.53x, config1.21–1.22x. Warm:1.31–1.32/1.88–1.90/1.08–1.09/
1.32–1.37/1.28–1.29x. JSON fresh has2.13% identical-role spread; report warm3.47%
spread/3.95%IQR. Compilation remains44–73us. Keep the full ranges and adverse
conditions. Do not subtract independent fresh/warm medians or interpret changes
from the earlier checkpoint as isolated source speedups. CPython report fresh
is below its warm block; the distinct observations are retained unchanged.

Includes mediated RAM-file I/O, full validation and prior-result cleanup; fresh
adds compilation/program disposal. Excludes startup, fixture staging/reset,
transport and final teardown. CPython retains modules; Lython isolates imports.
Startup, RSS, genuine pending-I/O and embedding-tail latency remain unmeasured.
External process CPU ticks are preserved but provide no guest-only attribution.

Local full ordinary/focused Release and installed consumers pass both actual
runtimes. Delivery CI42302690579 fails Ubuntu.NET10 on the discovery allocation
test:168736B versus131072B measured through diagnostic construction after refusal.
The test already has a first-refusal observation. Correct its endpoint to that
preflight boundary, retaining the original limit, refusal origin and zero final
balance.23focused admission/cleanup tests pass per actual11/10. The original
failure/log remains here; updated platform/stress qualification is still pending.
Do not label this source fully platform-qualified until those gates pass.

The previous f2ce53db runtime remains fully qualified under GREEN42294911136.
Publication/tagging postponed. The performance objective remains active: the text
gap and earlier source-pair debt are not explained by this aggregate refresh.

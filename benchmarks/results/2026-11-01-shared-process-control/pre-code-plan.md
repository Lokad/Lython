**B55 — one private same-process identical-image control (next method decision).**
B54's independent workers differ9.78% with identical bits/normal settings. Source
PyString uses process-local System.HashCode; differing bucket distributions are a
possible confound, alongside JIT/PGO/scheduling/I/O. This is NOT the measured cause.
Do not change production hashing/seed, weaken defaults or retry closed experiments.

Smallest bounded development diagnostic: adapt the EXISTING private complete-job
reflection driver to load two identical frozen Lython/host images into separate
managed AssemblyLoadContexts in ONE owned process, sharing System.Private.CoreLib.
No new benchmark framework, public/runtime/CLI options, dependency or native/unsafe
code. This private control is not a CPython comparison or replacement external guide.
Keep the existing host fixture, complete stdout/file goldens, result disposal and
budget balance checks. Each image owns its engine/compiled program/budget; all
actual users and result/script cleanup finish before process exit. No forced GC
or invented unloading guarantee for idle startup workers; external service owns
and joins the process.

Before timing, verify distinct Lython assembly/type identities, SAME CoreLib and
identical core/host hashes; run an untimed public-API hash witness for the actual
CSV header/category strings in both images and require identical results. This
checks one process-dependent variable without writing private runtime state.
If image isolation/hashes/goldens/funding do not verify, stop before timing.

ONE maintained CSV-helper warm case only. Normal worker GC/tiering throughout;
no flags to make a control pass.128 fixed warmup jobs/image,2s settle, seven
alternating paired32-job batches, <=30s owned service including setup/cleanup.
Full quiet/golden/balance/exit gates, every elapsed batch>100ms, <=5% median elapsed
difference and <=10% relative IQR. Preserve exact source/input/image identities,
all samples, disposal and limitations: coexistence changes memory/JIT/GC conditions
and cannot establish causality or a production throughput/CPython ratio. No retiming
if it fails. Stop after this ONE method decision, not a new qualification campaign.

Only if verified should a subsequent coherent runtime redesign use this private
internal comparison, alongside existing external-baseline tooling. Before any
candidate, name demonstrated transport/state/conversions to delete, full Python/
cancellation/admission/alias/cleanup acceptance, affected/unaffected scripts and
material elapsed/control criteria. Do not reopen B51/B53 or force local tweaks.
Independent correctness work and final released-provider qualification continue.

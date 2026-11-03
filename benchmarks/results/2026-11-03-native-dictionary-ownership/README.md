# Native dictionary ownership — B186

2026-11-03. **RETAINED; full delivery qualification pending.** Native table and
capacity/commit accounting now belong to the actual PyDict. Delete the separate
OrderedPyDictStorage object, static PyDictStorage factory/capacity forwarders and
unused Clone contract/implementations. CSV/dataclass known-capacity construction
creates the final owner directly. No new owner/pool/value carrier/dispatch framework.

Live globals, object namespaces and os.environ retain their real storage bridges.
Raw storage enumeration is explicit and distinct from Python mutation-checked
iteration. Native copy/attach admits a private destination, prepares protocol
metadata, transfers fields only after success and invalidates private staging.
Capacity estimates and replacement-overlap admission are unchanged, including
conservative retention and first-refusal behavior. Rare live-view dictionaries carry
unused native fields; native storage operations gain forwarding/kind checks.

Focused Release **206 internal/2,021 public per actual .NET11/.NET10 PASS**, builds
zero warnings/errors. Covers native/protocol/order/mutation, live namespaces and
host-mediated environment writes, copy/attach refusal/cancellation rollback, alias
visibility, CSV/JSON/dataclasses, suspended callbacks, retained results and shared
allowance reuse. Existing attach regression now also checks original data through
refusal and live values through successful ownership transfer. No architecture-only
suite or relaxed threshold/timeout. Full Windows/Ubuntu, original Stress and
installed-package consumers are a separate exact-source checkpoint, pending.

## Bounded comparison and decision

Qualified B183 source7a3a8afe versus 7b63896e+B186 source patch; frozen candidate
**4a64c429c3590bc29bb08a42c829fdee354604a8d50c7673bb0843d78b2c8371**,
baseline **0f143a6c68393a6149202e6238e8e6e4d62e61e775b545bd2868bc4d38aa47a9**.
Actual precommit binaries remain identified; later SourceLink revision metadata may
change rebuilt hashes without changing production source.

Existing protocol2 on the quiet Azure F4as_v7 Ubuntu24.04.4, .NET11 RC3. Persistent
catalogs/workers/evidence, tmpfs guest fixtures, normal GC/tiering. Complete CSV/text/
JSON/report/config, both invocation APIs; CSV/report compilation separately.
Three workers/job old/candidate/unchanged old,1.5s warmup, seven rotated ~150ms
batches,.5s settle. Owned141.386s; collection140.762s, exit0.
All identities/goldens/quiet gates/balances/fixture removals/worker joins PASS.

Warm clocks include invocation, actual mediated tmpfs I/O, full output/file checks
and previous-result cleanup. Fixture staging/reset, final cached teardown and startup
are outside clocks; final users/cleanup are joined. Compilation has its own boundary.
No actual suspended host-I/O or CPython retime. Systemd memory peak is not process RSS.
Positive means slower; candidate versus BOTH old workers, control secondold/firstold:

| Lane | Complete script | New/old1, new/old2 | Old control |
| --- | --- | --- | --- |
|warm|csv-summary-helpers|-2.75% / +0.57%|-3.30%|
|warm|text-summary-helpers|-1.94% / +1.91%|-3.78%|
|warm|json-summary-helpers|+0.15% / +0.95%|-0.79%|
|warm|report-render|-4.54% / -8.27%|+4.07%|
|warm|config-edit|-10.56% / +11.44%|-19.73%|
|warm-async|csv-summary-helpers|-3.80% / -1.73%|-2.10%|
|warm-async|text-summary-helpers|-0.94% / -3.50%|+2.66%|
|warm-async|json-summary-helpers|-6.76% / -2.30%|-4.56%|
|warm-async|report-render|-3.69% / -4.41%|+0.75%|
|warm-async|config-edit|+5.65% / +13.48%|-6.90%|
|compile|csv-summary-helpers|-11.64% / -0.69%|-11.03%|
|compile|report-render|+14.04% / +32.12%|-13.69%|

Allocation diagnostic uses unchanged three-isolated-image driver,32 warmups and
five16-job batches, both APIs, additionally three unchanged reader controls. CSV
−1.7–1.8%, JSON−2.4%, report−1.1%; text/config near flat. Accounted peaks retain
existing conservative estimates. A local constructor allocation-only control with
4,096 held dictionaries measures **192→168 B empty /328→304 B with two entries**,
a24B saving; warmed empty candidate has128B incidental total overhead. No timing,
RSS/GC-latency or CPU attribution follows from that control.

Initial report is lower in both APIs; CSV/text/JSON near moving controls or lower.
Initial config and compilation controls vary7–20%, and report compile has an adverse
+14–32% point. Preserve it. This justified one confirmation with the SAME binaries,
**SOURCE ROLES SWAPPED**, and longer stated warmup—not a failed-design verdict.

## Same-bit confirmation

Two NEW controls versus OLD candidate. Six targeted jobs: report/config warm through
both APIs and CSV/report compile. Three-second warmup,nine rotated ~200ms batches,
one-second settle. Owned95.577s; collection94.966s, exit0; all
identity/golden/gate/cleanup/join checks PASS. Do not pool with initial short sampling.
Normalized NEW versus OLD (two roles); control secondnew/firstnew:

| Lane | Complete script | New1/old, new2/old | New control |
| --- | --- | --- | --- |
|warm|report-render|-5.02% / -4.16%|+0.91%|
|warm-async|report-render|-1.64% / -2.11%|-0.48%|
|warm|config-edit|-1.42% / -2.39%|-0.98%|
|warm-async|config-edit|+0.89% / -0.90%|-1.78%|
|compile|csv-summary-helpers|+1.00% / +2.03%|+1.02%|
|compile|report-render|+0.12% / +1.56%|+1.44%|

Report sync−4.2–5.0%, async−1.6–2.1% (async IQR1.5–2.8%, modest evidence).
Config sync−1.4–2.4%; async straddles with2.7–7.1% IQR: VARIABLE, no gain/failure
claim. Initial huge compile penalty does not reproduce: CSV+1.0–2.0%, report
+0.1–1.6%, controls1.0–1.4%; treat as near-control/slight accepted compilation cost,
not a compilation gain or an explanation of the original point. No phase subtraction
or general whole-script CPU claim. Preserve all original failed preparation and
adverse conditions. CPU causes of differing unchanged workers remain unproven.

**Retain** removal of the native wrapper and duplicate factory layer, established
allocation reduction and report elapsed improvement. CSV original+1–2% debt remains
accepted (current CSV pair straddles). Fresh JSON+1.8–1.9% debt stays accepted/open;
this warm-only pair cannot close it. Current CPython baseline remains B184. No
independent percentage addition or relabeling historical data as current HEAD.
Publication postponed; substantial text/report gap and whole-script objective OPEN.

[Initial summary](summary.json), [raw observations](observations.tgz),
[confirmation](confirmation/summary.json), [confirmation raw](confirmation/observations.tgz),
[identity](identity.json), [source snapshot](source-snapshot.tgz), [patch](source.patch.gz),
[constructor diagnostic](constructor-diagnostic/measurements.json).
Initial archive SHA256:c5f6429b2821a67c89475006107ccfe5e5778c898cd7b0f73a2b3bb969de9001.
Confirmation archive SHA256:767da3b0f2bd712142a6abd6dde7452424af9149d85bda9e78352875e42b243a.

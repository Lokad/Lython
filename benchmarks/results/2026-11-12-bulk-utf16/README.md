# R276 — bulk managed UTF16 encoding, refined source retained

2026-11-12 over qualified1011ed0b (568production sources match measured R274).
[Cost source gate](../2026-11-12-config-report-costs/README.md) identifies the
manual per-scalar UTF8 decode/surrogate/unit writer. Replace it with bounded
System.Text.Unicode.Utf8.ToUtf16 calls into the already-admitted destination.
Managed span views allocate no intermediate character buffer. Explicit requested
byte order, BOM and newline handling remain. Active tokens are checked between
scalar-aligned1024-byte chunks. No unsafe code/API, pointer, PInvoke, native probe,
dependency, pool, cache, policy, option or public API is added.

Delete the manual scalar writer and three unused ReserveUtf16Output/
Utf16EncodedByteCount helpers. The genuine sizing preflight remains. The retained
refinement changes exactly one production file; common TextCodec and text-file
writer/count files are restored byte-for-byte to baseline. Character-count/sizing
fusion was withdrawn rather than adding another selector or compatibility layer.

686 focused checks pass per form across actual.NET11/10 (200internal/143public
each): Python goldens, UTF16 native/LE/BE, BMP/supplementary/chunk boundaries,
newlines/BOM, byte encode and text write counts, buffered prefix/refusal, retained
results/budget reuse and genuinely pending I/O cancellation/awaited cleanup. Three
new public theories cover48encoding/newline/boundary combinations through both
APIs with two retained results and exact CLR-constructed bytes. Seven existing
complete search/CSV/text/JSON/UTF8-report/UTF16-report fixtures pass both APIs and
compile-plus-one-run. First-form separate compilation/allocation observations are
kept; unchanged frontend compilation is not retimed for the refinement.

## First form and adverse result

First form combined sizing/character counts with bulk encoding. Allocation flat;
desktop timing mixed, including UTF8 Run+3.91..+7.22%. B631 three jobs153.377s:
UTF16 Run elapsed−8.81..−3.42%, CPU−9.33..−2.82%; Async−9.19..−4.77%,
CPU−7.41..−4.59%. UTF8 control elapsed+1.94..+5.35%, CPU−1.22..+3.75%.
Preserve every observation. The new common-path encoding branch is a plausible
source lead, not a demonstrated cause. Do not promote this broader form.

The bounded refinement removes that added branch/count-result plumbing entirely.
Exact common-path restoration,686focused checks, seven scripts/both APIs/fresh
pass again. Allocation flat. Desktop remains moving: UTF8 Run+0.54..+8.41% with
7.83%control, Async+2.74..+3.32% with0.57%control; UTF16 Run−0.67..+7.63%,
Async−2.81..−0.71%. Fresh CSV unchanged-control spread64.52% is preserved, not
promoted or silently removed. No desktop CPU or compilation speedup claim.

## B632 bounded confirmation

Same fec79 worker/adapter0.4, normalGC/tiering, F4as_v7/Ubuntu24.04.4/.NET11RC3.
Same reference/jobs/warmup/rotated7x250msprotocol as B631: two processes per image,
12swarmup for UTF16,5sfor UTF8. Three jobs finish153.958s inside240sowner/30sstop/
30smargin. All identities/goldens/shared balances/quiet/join/unmount/archive/localSHA
pass. Both terminal states cached; no live users or sole RAM evidence remain.

| Complete script / phase | Elapsed change | Wider processCPU change | Ref / candidate spread |
| --- | --- | --- | --- |
| UTF16 report / Run | −7.70..−1.16% | −9.72..+1.00% | 4.74/2.19% |
| UTF16 report / RunAsync | −4.95..−2.00% | −7.14..−0.91% | 1.47/1.59% |
| UTF8 report control / Run | −0.36..+2.11% | +0.00..+1.25% | 1.18/1.26% |

Ranges span all four process-median pairings, not confidence intervals. Retain the
smaller native encoder and observed target elapsed improvement, with supporting
AsyncCPU. Moving sync references leave CPU attribution uncertain. The remaining
UTF8 control range is accepted small regression/measurement debt, not an equivalence
claim or a proven fix of the first result. No further unchanged timing is needed
for this decision. No substantial general CPU claim or inferred CPython ratio.

Warm includes mediated RAM I/O/full output checks/prior-result cleanup; excludes
compile/startup/reset/protocol/finalcache. Wider CPU includes reset/protocol/JIT/GC.
Frozen before/reference source equality and different generated metadata remain
explicit; no DLL is relabelled as a CI-built image. Full platform/stress/installed
consumer qualification [passes on41bd869b](delivery/README.md) at this retained-source boundary. Publication stays
postponed; broader whole-script objective remains open. No benchmark evidence/image
retirement or reduced OS/evidence floor. Final B632free350,363,648B.

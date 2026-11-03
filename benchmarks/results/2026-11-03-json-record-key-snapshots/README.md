# B166: immutable previous-record JSON keys

2026-11-03. **Retain the refined version.** Retained source **2323ccebc8d526e5a5e0214d85d2de9feba5bff2**, frozen before its commit; baseline **433f7e36b769999dafaa08a5440082328e5ccdb6** is fully
qualified by cached 41663811073. Candidate core
**3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc**.
Focused Release passes; full Windows/Ubuntu, both-framework, original stress and
installed-package delivery **41672373384 is active**. Publication postponed.

The runtime no longer maintains an execution-wide UTF-8 name dictionary, its
custom comparer, capacity admission/growth or unique-name inventory. Instead, one
immutable sorted snapshot contains only independently owned keys from a completed
native JSON object. Incoming UTF-8 uses bounded cancellable binary lookup. Matching
key sets reuse the snapshot; changed sets decode normally and admit a replacement.
The existing parser and dictionary storage remain. No new schema backend, owner
graph, refcounts, pool, limits, fallback or collector machinery.

Snapshots remain funded through nested/reentrant/suspended callbacks. Replacement
metadata is admitted before allocation and conservatively stays batch-owned until
execution cleanup; returned strings/aliases keep their existing owner. This can
reject changing schemas earlier than exact reclamation. It does not accumulate an
index of all unique names. Input storage is never retained as a key. Equal decoded
strings have no interning/cross-decode identity contract; dictionary replacement
still retains its original equal key and last value. First refusal stays terminal.

## Decision and controls

| Complete warm job | Refined elapsed % / unchanged control | Refined allocation | Accounted execution peak change |
| --- | ---: | ---: | ---: |
| Unique names, sync | −35.15 / −2.44 | −14.64% | −834,737 B |
| Unique names, async | −31.72 / +0.27 | −14.64% | −834,737 B |
| Unknown repeated schema, sync / async | −0.91 / −0.50; +0.60 / +0.49 | essentially flat | −432 B |
| Reordered schema, sync / async | −0.51 / −0.33; +0.04 / +2.50 | essentially flat | −432 B |
| Report, sync / async | +0.13 / −0.29; −1.45 / −0.80 | essentially flat | −440 B |
| JSON lines, sync / async | −1.77 / +3.42; −2.27 / +0.17 | essentially flat | −448 B |
| Config, sync / async | +2.15 / +1.94; +0.46 / +1.81 | essentially flat | −376 B |
| Text control, sync / async | +0.02 / +0.85; −2.41 / −1.40 | flat | unchanged |

Unique-name CPU decreases **37.05% sync /31.99% async**, controls −1.75%/+1.34%.
Its repeated samples remain variable (6–9% baseline IQR), but the magnitude,
allocation and peak evidence support the selected replacement. Other scripts do
not establish a general gain. Async config baseline IQR is 19.62%; its timing is
inconclusive. JSON sync CPU +1.90%, control +3.91%, is not proof of a CPU gain.
Full per-image medians, IQRs, controls, peaks and all adverse conditions are retained.

The initial ordered hint was **not promoted**: unique elapsed −32–37% and allocation
−14.64%, but reordered records +37–40% elapsed/+46.6% allocation, and unknown
repeated schemas +10–16% elapsed. Those bits and observations remain in `ordered/`.
The refined sorted lookup removes positional tracking and restores reuse across
field order. This is a changed design, not an unchanged favorable retime.

Retain the refined architecture and measured unique-name improvement. The original
B138 index regression is superseded by removing that index; these points do not
remeasure the original pre-B138 runtime or prove every workload recovers its old
timing. Whole-script CPython gaps remain open; the external guide keeps measured
b087 points separate. Next investigate complete native object construction rather
than another name hash or lookup tweak.

## Conditions and validation

Same quiet Azure Standard_F4as_v7, Ubuntu 24.04.4, .NET 11 RC3; ordinary GC/tiering,
three isolated images sharing CoreLib/process hash state: before, candidate and
identical-before control. Existing complete-job driver, actual mediated RAM-file
I/O, full stdout/file goldens and result cleanup. Timer includes fixture reset,
invocation and validation; compilation/startup/final cached teardown excluded.
Both APIs use synchronous-completion file capabilities here; genuine suspension
is qualified separately. No new benchmark framework or CPython timing campaign.

Seven complete-script cases at the existing large size (1,024): report/JSON-lines/
config/text and the 1,024-record unique-name control, plus unknown repeated and
rotating-order four-field fixtures using the
same generic summation script/full independent output. Source/fixture hashes and
complete inputs are retained. Ordered: 128 warmups, nine rotated 32-job batches;
some sub-100 ms samples have up to 17% IQR. Refined: 256 warmups and nine 64-job
batches to obtain more useful observations, same scripts/control/API boundaries.
Each case settles two seconds. All goldens, zero-balance checks, image identities,
before/after quiet gates, owned fixture removal and joins pass. Services exit zero
in **102.259 s ordered /155.070 s refined**; shared lease released. No extra replay.

Release **767 public/181 white-box per actual .NET 11/.NET 10** pass for both
variants. Includes scanner cancellation, both hooks with real suspension and
cancellation, retained aliases, terminal admission refusal and bounded host batches
returning allowance. New independent semantic cases cover reordered/empty/duplicate/
escaped keys and nested numeric/object/pairs callbacks that reenter JSON parsing
and suspend on host I/O. Metadata refusal preserves the prior funded snapshot.
Only cross-decode global-pool identity assertions are replaced, with byte ownership,
mutable-record independence and cleanup assertions preserved. One initial new-test
host-type compilation error was corrected before any test run or freeze; no runtime
measurement is derived from it.

[All comparison tables](comparison-tables.md),
[ordered adverse source/observations](ordered/identity.json),
[retained source/observations](refined/identity.json),
[refined assessment](refined/assessment.json),
[original global-index evidence](../2026-11-03-shared-json-property-names/README.md).
Source snapshots include the replacement file; patches alone omit initially
untracked files. Candidate was built before its source commit. No process RSS or
embedding-latency claim; accounted peaks are cooperative estimates.

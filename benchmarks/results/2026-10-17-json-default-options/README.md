# Shared immutable JSON defaults — 2026-10-17 UTC

| Engine | Measured source | Warm RunAsync job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | Two frozen runtime files on f728636a | 12.993 ms | 2.23% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 4.520 ms | 1.02% |

Nominal Lython / CPython: **2.875×, unqualified**. Earlier Lython f728636a measured
13.007 ms with 2.39% spread. The candidate median is 0.11% lower, within spread:
**no whole-job elapsed improvement is established**. The earlier identical
[BX controls](../2026-10-17-json-helper-warm-async/README.md) are separate data.

One immutable default `JsonLoadOptions` record now serves the two existing binders,
holding only null callbacks and native True. Validation runs in its existing
order first. Callbacks, False, None, integers and custom strict objects retain
their values and fresh records; selection never invokes truth protocols. Writable
decoder attributes stay per instance. No parser, execution mode, guest-object
cache, pool, scope, allowance or public API is added. This fixed runtime metadata
uses existing control headroom and retains no guest or execution state.

Separate warmed native allocation diagnostic, **128 bindings**:

| Binding | Earlier bytes | Candidate bytes |
| --- | ---: | ---: |
| Module default / constructor default | 11,264 / 11,264 | 0 / 0 |
| Module explicit True / constructor explicit True | 8,192 / 11,264 | 0 / 0 |
| Module False / constructor False | 8,192 / 11,264 | 8,192 / 8,192 |
| Module custom strict / constructor custom strict | 8,192 / 11,264 | 8,192 / 8,192 |

This removes default records and unnecessary True boxing. Arguments, warmup and
static initialization are outside counts. It is not whole-script allocation,
live memory, RSS or timing evidence. Native diagnostics used Windows 11,
i7-14700KF, SDK 10.0.300-preview.0.26177.108 / runtime 10.0.13, Release, and
`GC.GetAllocatedBytesForCurrentThread` without forced GC. Raw
[before](native-baseline.json)/[after](native-candidate.json), process/TRX receipts
and [diagnostic source](allocation-diagnostic.cs) are retained; the temporary
probe is removed from supported tests and their final assemblies.

The complete job and boundary match BX: existing JSON helper script, **4,096
records / 110,592 UTF-8 bytes**, a sorted 25-byte report and stdout `3072\n`.
Warm reused compilation, fresh state, completed-host public `RunAsync`, file
reset/staging, previous-result cleanup and full output checks are timed.
Compilation/startup/transport/hashing, real suspended I/O and embedding latency
are outside. No new or retuned fixture is used.

Same dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, Linux
6.17.0-1022-azure, four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap.
Both Release builds use SDK 10.0.401 / runtime 10.0.12. CPython is GCC 13.3.0
PGO/LTO, ordinary GIL/GC, no JIT/debug/free-threaded mode, with `-I -S -X utf8`.
Lython keeps ordinary limits/checks and explicit owners under an unbounded shared
allowance. There is no denial or finite-budget/RSS qualification. Worker overrides
are null; the launcher removes supervisor tiering overrides. No costs subtracted.

The working two-file candidate is on immutable
f728636a5283dcbeb21344629802c2468c5c0525, not an immutable candidate binary or a
measurement of later HEADs. [Source hashes](frozen-source.json),
[patch](candidate.patch), [preparation](preparation.json), [build](build.log) and
canonical sources in the [raw archive](raw-evidence.tar.gz) identify it exactly.
The files match the delivered source; assembly metadata still names the base.
Loaded identities and every response are in [micro.json](micro.json).

One changed-candidate capture: existing 15-second collection / 30-second owned
group cap/shared lease, one-second warmups, two-second settling, seven rotating
25 ms batches. Total **9.945 seconds**; **37 responses / 442 invocations** match
complete goldens/digests, twelve inputs and both sources unchanged. All workers
and fixture directories are gone, service inactive with an empty group, lease
released. See [validation](validation.json), [ownership](ownership.json),
[launch](launch.json) and [terminal](terminal.json). No recapture, variant campaign,
extra profile or long lane.

Local Release: **583 focused checks** pass for binding/errors, decoder state,
subclasses, genuinely suspending hooks, cancellation, Unicode/offsets, ownership,
admission and complete-file goldens. Six independent snippets match CPython
3.13.2 through each API. [Local validation](local-validation.json) verifies
unchanged decoder validation/attributes, frozen source and probe removal. No
permanent tests, limits, scales or assertions changed; Stress is retained, not
rerun. Package/platform qualification is part of the coherent runtime delivery.

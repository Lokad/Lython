# Default JSON encoding: allocation reduction and complete-job diagnostic

Measured **2026-10-18 UTC**. The candidate contains four frozen runtime files on
`22648c5a364877801ff9c11ba6bdc6fb75d631c7`, not an immutable build of today's HEAD.
See [source hashes](frozen-source.json), [source proposal](source-proposal.json),
[patch](candidate.patch), [preparation](preparation.json) and [validation](validation.json).

| Engine | Complete file job median | IQR / median |
| --- | ---: | ---: |
| Earlier Lython, clean 22648c5a | 1.514 ms | 8.54% |
| Lython, frozen default-call change | 1.571 ms | 7.24% |
| CPython 3.13.16 | 0.16484 ms | 1.07% |

Lython / CPython is nominally **9.533×**, unqualified. The 3.80% higher candidate
median is within Lython sample spread. This single short comparison establishes
neither a gain nor a regression. It does not measure compilation, startup,
embedding latency or genuinely suspended I/O. No recapture, profile or full lane
was run. The remaining gap cannot be attributed to one call or mechanism here.

The job reuses `BridgeScriptBenchmarks.JsonRoundTripScript`: read one 42-byte
configuration, parse JSON, edit the items field, then encode and write the entire
46-byte output. Only absolute fixture paths become portable relative paths.
[Source](bridge-source.py), [catalog and independent literal file golden](catalog.json)
are frozen. There is no stdout. Ordinary `json.dumps(data)` is affected; most
other catalog JSON jobs explicitly supply options and would not exercise this
default-call change.

The boundary is **warm public RunAsync with completed host operations**, reusing
compiled code. Invocation setup, prior-result cleanup, file staging/reset,
capture/projection and full-file checks are timed. Compilation/startup, transport
and response hashing are excluded. CPython uses the same fixture and source;
both engines retain their ordinary GC/tiering and run sequentially. Lython keeps
checks and explicit owners, with unbounded shared allowance and no instruction
fuel. Worker identities confirm no runtime override or BenchmarkDotNet loading.

The dedicated Azure Standard_F4as_v7 runs Ubuntu 24.04.4, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs and 16 GiB RAM without swap. Clean builds use .NET SDK
10.0.401 / runtime 10.0.12. CPython 3.13.16 uses GCC 13.3, PGO/LTO, ordinary GIL/GC
and no JIT/debug/free-threaded build. The shared VM lease covers the comparison;
seven measured batches per engine follow warmup. Ownership lasted 7.631 s within
the existing 15 s collection / 30 s process-group bounds.

All **41 responses / 10,839 invocations** match stdout and complete-file goldens.
Twelve input hashes and 55 baseline files are unchanged. All three workers and
their fixture directories are gone; the lease is released. See [ownership](ownership.json),
[launch](launch.json), [terminal proof](terminal.json), [runner](run.py),
[raw log](micro.log) and [unaltered archive](raw-evidence.tar.gz).

Separate native allocation evidence uses 128 scalar calls after 512 warmups,
through both APIs, with existing explicit execution owners and no forced GC:

| Call shape | Earlier bytes/call | Candidate bytes/call | Saved |
| --- | ---: | ---: | ---: |
| Single positional default | 2,328 | 488 | 1,840 |
| Keyword object | 2,328 | 2,264 | 64 |
| Explicit cls=None | 2,328 | 2,264 | 64 |
| Explicit ensure_ascii=True | 2,304 | 2,240 | 64 |

The default call omits ten-slot value/presence binding, forwarding list/backing/
copy, boolean defaults, temporary encoder/options/separators and encode/default
wrappers. It uses existing serialization with immutable default metadata and
the same base-default callback/error. General calls retain their route and save
only stateless default-method capture storage. Exposed encoders remain mutable.
No new parser, owner, cache, pool, mode or unsafe code is introduced. The entry,
constructor, bound encode and encoder-body checkpoints remain equivalent.

These counts measure fixed scalar-call allocations, not complete-job allocation
savings or timing. [Before](native-baseline.json), [after](native-candidate.json),
[allocation proof](allocation-proof.json), [diagnostic source](allocation-diagnostic.cs),
[baseline process](native-baseline-process.json), [candidate process](native-candidate-process.json)
and their [baseline](native-baseline.trx) / [candidate](native-candidate.trx) results
preserve the evidence. The temporary diagnostic was removed from the tests.

**645 focused Release checks** pass. Seven independent isolated CPython controls
per API cover ordinary values/Unicode, binding/aliases, argument effects, cycles,
unsupported types, nonfinite values and mutable explicit encoders. Eight native
cases freeze every fuel/span/effect checkpoint plus successful/error outcomes;
all match the [unchanged baseline](baseline-checkpoints.json). Existing 70 white-box
JSON names/records remain exact. Binding/options/subclasses, actual callback
suspension, cycle/depth limits, retained owners, shared budgets, terminal denial,
cancellation and complete-job goldens pass. See [local validation](local-validation.json).
No timeout, containment assertion or Stress scale was relaxed; full Stress was
not rerun. Small control-headroom thresholds can differ, as documented in MEMORY.md.

# JSON string emission admission, 2026-10-18 UTC

JSON values and keys now admit large transient UTF-16 decoding before allocating
the copy. All 24 refused-admission regressions allocate 168,776–172,752 bytes on
the baseline with only 16 KiB unused. The initial fixed capture allocates
4,640–8,616 bytes before refusal. Input, first structured failure, retained
prefixes and cleanup are checked. **No elapsed or CPython speedup is measured.**

Baseline: `765a09fc95b2402abff6762a84b32417403c1b7b`. Candidate: that revision
plus the [three-file patch](candidate.patch), with source and assembly identities
in [validation](validation.json). This is a diagnostic source identity, not an
unspecified later HEAD.

| Native default dumps / allocated bytes per 128 calls | Baseline | Candidate |
| --- | ---: | ---: |
| ASCII string, 32 UTF-8 bytes | 117,760 | 106,496 |
| ASCII string, 128 UTF-8 bytes | 246,784 | 210,944 |
| ASCII string, 129 UTF-8 bytes | 246,784 | 250,880 |
| 32 astral characters, 128 UTF-8 bytes | 403,456 | 384,000 |
| 16 short string keys / integer values, 230 output characters | 507,904 | 442,368 |
| ASCII string, 4,097 UTF-8 bytes | 4,864,000 | 4,868,096 |
| Integer `123456`, unchanged control | 64,512 | 64,512 |

These are medians. [Before](allocation-before.json) and
[after](allocation-after.json) preserve all eight samples per case, including
one 128-byte baseline integer-control outlier. Larger inputs add the existing
temporary-reservation object, observed here as 32 bytes per operation. Short
inputs avoid their intermediate CLR string; final output allocation and its
ownership remain. Byte counts do not establish complete-script latency,
compilation cost, embedding impact or a CPython ratio.

The [temporary fixture](allocation-diagnostic.cs) invokes an already loaded
native json.dumps callable. Context, governed inputs, arguments and callable
loading stay outside the thread-allocation counter. The counter includes 128
operations, retained output strings and consumption of their UTF-8 lengths.
Full final output and owner cleanup to zero are checked. Four warmups precede
eight samples. This excludes public scheduling/setup, executable dispatch,
compilation, startup and host I/O. No GC was requested. The temporary fixture
was removed before the final Release builds.

The Windows development workstation supplied these allocation samples: Intel
Core i7-14700KF, 20 cores / 28 logical processors, Windows build 26200, x64.
[Toolchain](dotnet-info.txt) records the SDK/runtime. These are local allocation
diagnostics, with the process receipts and binary/source hashes in validation.
No Azure VM, full lane or new measurement framework was used.

Three guest-string emitter call sites now share one overload of the existing
escaper. At most 128 UTF-8 bytes decode into 128 bounded managed characters
through PyString's same strict UTF-8 decoder; its original character loop and
checkpoint locations remain byte-for-byte unchanged. Larger input reserves
64 plus twice UTF-8 byte capacity before the existing CLR decode and holds that
lease through escaping. The source, output builder, sort scratch, final copy,
yielded chunks and returned result keep their original ownership. Private
decoded text never escapes or becomes a cumulatively retained Python string.

Conservative decode/output overlap can reject earlier, particularly for
non-ASCII input where byte capacity exceeds decoded width. Fixed managed
scratch uses bounded control headroom. This does not change the existing
output-growth policy or introduce another decoder algorithm, builder,
serializer, pool, cache, allowance or lifetime system. See [MEMORY](../../../MEMORY.md).

[Baseline refusals](refusal-baseline.trx) and [final checks](guard-candidate.trx)
cover values and keys in dumps, explicit encode and iterencode through both
execution APIs and local/shared limits. Additional checks verify refunds when
decoding admits but output later refuses, strict invalid-input behavior and
retained string funding after iterator completion and another shared owner.
The existing JSON checkpoint/effect/chunk records stay unchanged.

Twenty-two [isolated CPython semantic controls](cpython-oracle.json) cover both
ensure_ascii settings, empty/text/escaping, ASCII 127/128/129-byte boundaries,
Unicode byte boundaries, astral pairs and the long-string checkpoint. All six
value/key encoder forms agree through both public execution APIs. CPython
3.13.2 ran with `-I -S -X utf8`; it supplies semantic goldens, not timings.

Focused checks include complete CSV/text/JSON/file goldens, command-failing
admission, shared allowances, caches/results and managed stack protection.
Counts and receipts are in validation. Platform/package checks are batched at
delivery; no containment assertion, timeout or Stress scale was relaxed.

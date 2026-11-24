# Drain native JSON file output inside the traversal (B1617)

The private prototype on `d7dd3d4bd` writes completed JSON chunks
inside the existing traversal. The exact text-file sink drops the
scalar-count scan and boxed return value. Public iteration and the
custom writer path still produce independent guest chunks. The
source archive identifies the exact measured patch.

An instrumented pair ran the manifest, file-backed JSON summary and
CSV summary in both execution modes. The manifest moves from 99
traversal entries to 2, while all 97 writes and 1,441 bytes stay the
same. The JSON summary moves from 5 entries to 1, with all 4 writes
and 21 bytes unchanged. The admitted benchmark host completed these
writes synchronously, including under `RunAsync`. The CSV summary
uses neither route. These counter images were excluded from timing.

The manifest allocates 3,104 fewer managed bytes per run, from 99,640
to 96,536 bytes. The JSON summary saves 128 bytes. Each lost result
accounts for 32 bytes. Peak admission matches on five scripts.
Windows and Ubuntu each pass 1,338 focused .NET 11 Release checks
covering cancellation, producer ownership, partial writes and
callback order. Both frozen images pass all five CPython goldens.
Seventeen targeted cases pass after the assertion refinement below.

## Quiet comparison

Ubuntu 24.04.4 and .NET 11 RC3 ran on the Azure F4as_v7 on November
24, 2026 UTC. Two workers per source warmed separately for five
seconds before seven rotated batches. The ranges span the four
pairings of worker medians. Negative values favor the prototype.
Elapsed includes warm execution, applicable RAM-backed I/O, result
checks and prior-result disposal. Wider CPU includes fixture reset
and request framing. Both quiet gates, all 168 batches, verified
archival and joined cleanup passed.

| Script | API | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Run | −0.76 to −0.37% | −2.63 to +0.09% |
| Configuration manifest | RunAsync | −2.01 to −0.19% | −2.14 to −0.09% |
| JSON summary helpers | Run | −2.48 to +0.13% | +1.07 to +2.91% |
| Expanded JSON Lines | Run | −1.80 to +1.22% | −0.19 to +3.57% |
| Default JSON Lines | Run | +1.43 to +4.54% | −0.38 to +5.22% |
| Quoted CSV invoice | Run | −2.05 to −0.57% | −3.45 to −2.27% |

Keep the combined prototype private. Manifest gains are small and
default JSON Lines shows an adverse elapsed result. The measured
allocation reduction supports isolating the native file result change
while restoring the original eager traversal. The initial WSL block
had up to 39.65% same-source worker spread. Its raw results remain
available. Before quiet staging, byte-identical archived catalogs were
hard-linked under the comparison lease, preserving paths and contents
and recovering about 35 MB. Storage receipts capture the SHA and the
earlier full-disk state.

## Existing behavior exposed by the probes

The reference and prototype both stage completed values before the
next default callback. CPython 3.13.2 also stages the list separator
before delegating. Its offsets are 4 and 12, with `[1, "kept", ` left
in the file after the second callback raises. Both Lython images
observe offsets 2 and 10 and leave `[1, "kept"`. The targeted
regression checks completed values and the first error while allowing
either separator position. The independent probe preserves the exact
differences.

Repeated exact builtin dumps in one execution also keep the private
iterator's shell charged after completion. Both Lython images use
232,704 bytes of peak execution admission for 1,024 one-byte dumps
in both modes. The same buffered writes without encoding peak at
3,524 bytes. The allowance returns to baseline at execution disposal.
The 224-byte per-call shell is a candidate for operation-scoped
ownership after proving that custom or captured iterators keep their
longer ownership.

[Quiet summary](quiet-summary.json) · [Quiet observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [WSL summary](summary.json) ·
[Images and source patch identity](identity.json) · [Procedures and original evidence](observations.tar.xz).

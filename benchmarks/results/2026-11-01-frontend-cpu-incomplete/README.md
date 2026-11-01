# Incomplete frontend CPU diagnostic (B24)

Source **472d64ef**, dedicated Ubuntu 24.04.4 F4as_v7 VM, four AMD EPYC 9V45
vCPUs / 16 GiB, pinned .NET 11 RC3, 2026-11-01. **Unqualified capture protocol**;
no before/after timing, complete-job gain or new CPython ratio is established.

The existing B01 compilation capture predates B04's traversal replacement and
contains 31 unknown leaves among 137 samples. It cannot attribute today's costs.
The fixed B24 question was current compile-only CPU attribution, followed by one
warm synchronous control, using the maintained CSV helper source/fixture.
The declared limit was 30 seconds capture inside a 180-second owned service.

## What ran and what failed

- Initial launcher failed on CRLF shell syntax before any quiet gate, worker or
  profiler. [Bootstrap note](bootstrap-note.txt) preserves this; the shell script
  was normalized to LF before the single actual capture.
- The actual preflight quiet gate passed. The worker used ordinary GC/tiering;
  only the machine-check supervisor disabled tiering. Before verification passed
  complete stdout/file goldens. Bounded warmup preceded sampling.
- One batch returned `Completed`, **32,768 fresh compilations**, in **5.1364 s**.
  Every compilation validates the result and disposes its predecessor; the final
  compilation remains cached under the existing worker contract. Source/input,
  assembly identities and the full response are in [record.json](record.json).
- Perf recorded cpu-clock at 199 Hz with frame-pointer chains and saved CLR maps.
  The compiler thread is selected by actual LythonEngine/Frontend Compile caller
  chains, not maximum CPU. Exactly one thread has those chains. Compiler CPU is
  5.13 s; other recorded thread deltas are zero at the OS clock's resolution.
- **My collector assigned a regex match to the global name `frame`, overwriting
  its protocol-reader function.** Post-capture verification then raised TypeError.
  The finally path stopped/joined the profiler, waited for the worker and killed/
  joined it when it remained waiting for a request (recorded exit -9). The owned
  systemd service exited and its cgroup/lease ended. No matching or other work was
  left running.
- There is **no post-capture golden, warm-control capture or final quiet gate**.
  The final cached owner's normal cleanup was not qualified by this capture.
  This limitation cannot be repaired by calling the completed batch a qualified
  campaign. No workload was recaptured. Existing focused/full semantic and
  ownership tests remain separate evidence.

Saved profile analysis is diagnostic only. An offline pass holds the existing
shared VM lease, reads the same events and adds no workload execution. The
[archive](evidence.tar.gz) preserves raw perf data, all chains/maps, exact scripts,
catalog, machine-before and records; transient control/ack FIFOs are omitted for
portable extraction. The original collector bug is preserved for explanation,
not offered as a working reusable tool.

## Findings and stopping point

There are **1,022 compiler-thread samples**, **138 unknown leaves (13.5%)**, no
empty/single-frame chains, and one explicitly malformed trailing map record.
Maps and managed/native caller chains can still omit inlined frames. Tiered and
optimized versions are combined below by presence in each chain, never summed
as independent work. These inclusive groups overlap and are not removable shares.

| Source component in the recorded chain | Samples | Share |
| --- | ---: | ---: |
| FormattedStringTokenization.Read / lexical pipeline | 332 | 32.5% |
| Parser.Parse | 220 | 21.5% |
| StaticAbstractInterpreter.Analyze | 158 | 15.5% |
| ExecutableScript.Compile | 107 | 10.5% |

[Assessment](assessment.json), [leaf counters](guest-cpu-leaves.json) and
[caller counters](guest-call-chains.json) retain the counts/limitations. The
generic lexer ReadAllTokensUpTo has 140 self samples; parser, traversal, regex
matching, allocation/native and unknown work remain visible. Lexing itself is
required work: 32.5% is **not** a predicted gain.

Current source first scans/masks Python-specific strings, joined lines and form
feeds, then invokes the generic Lokad.Parsing lexer and reconstructs source-line
metadata where masking occurred. This is a concrete duplicated lexical pipeline,
distinct from a universal interpreter or tagged-value redesign. The inspected
read-only sibling source at 4852d04a shows static rule metadata but mutable reader
indentation state; sharing a reader blindly would be incorrect. That source is
not promoted as proof of the installed 1.1.0 binary's exact implementation.

Stop B24 here with its incomplete controls. A bounded subsequent hypothesis is
one Python-specific lexical pass over the original source, preserving the parser,
diagnostics, scoping, lowering and executable engine. It must prove semantic and
ownership correctness and a material complete-compilation benefit against fixed
controls before replacing anything. This capture does not justify a new compiler,
reader pool, lock, unbounded metadata cache or claims about warm execution.

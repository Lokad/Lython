# B1904 Attribute generator shipment after direct-yield cleanup

The November 27 capture uses production `f1d107f54` on Ubuntu WSL
pinned to CPU 27 with .NET 11 RC3. The 32-file image matches the B1903
candidate. Five independent CPython 3.13.16 output and file goldens
verify. Both traces use the existing worker, four seconds of verified
warmup and a six-second EventPipe managed thread-time window. Both
scripts process 2,048 CSV rows with `RunAsync`.

| Script | Verified invocations | Generator advancement¹ | Generator construction¹ | Argument expansion¹ | Positional binding and entry¹ | Argument buffer allocation¹ |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Iterable-row shipment | 1,216 | 65.9% | 4.3% | 26.0% | 10.4% | 2.2% |
| Per-row generator reduction | 1,408 | 26.0% | 2.8% | — | 0.1% | — |

¹ Inclusive share of sampled thread time with resolved Lython ancestry,
excluding identifiable waits and GC polling. The regions overlap and
include guest work. The binding region includes positional callback
entry. This capture provides attribution and does not measure a
before-and-after speedup.

The iterable script supplies four values from a generator. It drains
them into `CallArgumentValue[]`. The eligible nontrivial callback then
copies the positional values into its native workspace.
`StartPositionalCallbackWorkspace` appears in the iterable trace. A buffer pool
would leave that second representation and callback entry in place.
Test whether the caller can keep one admitted operand storage through
the drain and final binding. The design needs a funded path across
source or callback suspension, ordered overflow into ordinary binding,
and unchanged handling of aliasing, reentry and refusal. Existing
same-storage generator binding and duplicate-copy failures remain
relevant controls.

All 41 sampled batches and both pre/post verifications pass. Both worker
and trace processes join and the persistent archive verifies.

[Attribution](attribution.json) · [Run summary](run-summary.json) ·
[Image identity](identity.json) · [Traces and procedures](observations.tar.xz) ·
[B1903 comparison](../2026-11-27-direct-yield-resume/README.md).

# B1450 Separate direct calls from native stack workspace

Private `7574b0c71` moves workspace construction outside the direct
return. An eight-slot block serves smaller bodies and a sixteen-slot
block serves wider bodies. The measured factory bodies need six or
seven slots, and the dedicated wide control needs sixteen. The core
DLL is the only difference across the 32-file images.

Local x64 .NET 11 Tier 1 disassembly shows the direct entry's stack
subtraction falling from 504 to 168 bytes on Windows and from 440 to
104 bytes on Ubuntu WSL. Eight-slot and sixteen-slot calls then enter
another frame. Their combined stack subtractions on Ubuntu WSL are
360 and 488 bytes respectively, before saved registers and return
addresses. These are generated code measurements. The warm comparison
below measures separate processes on the quiet VM.

Compared `07f2b859d` and `7574b0c71` on November 23, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two fresh workers
per revision warm for eight seconds and supply seven rotated batches
each. Ranges span the four pairings of worker medians. Warm elapsed
includes guest work, applicable mediated RAM-backed I/O, verification
and previous-result disposal. Compile to result adds compilation and
compiled-owner cleanup. Wider process CPU includes resets and request
handling. Startup and final teardown sit outside elapsed.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −0.22 to +2.51% | −0.90 to +2.15% | 1.25 / 1.47% |
| Postponed nested factories | Compile to result | −6.65 to +0.12% | −7.94 to +1.38% | 4.06 / 3.07% |
| Eager nested factories | Warm | −1.86 to +4.88% | −0.75 to +4.51% | 5.20 / 1.58% |
| Direct positional integer calls | Warm | −2.02 to +9.11% | −2.00 to +7.80% | 1.27 / 9.97% |
| Wide native positional calls | Warm | +2.66 to +5.20% | +2.89 to +5.68% | 1.52 / 0.95% |
| Quoted CSV invoice | Warm | −2.11 to −1.78% | −0.65% | 0.20 / 0.14% |
| Ordinary split report | Warm | +5.08 to +5.14% | +3.10 to +4.70% | 0.01 / 0.05% |

Keep the variant private. Both factory elapsed results span zero.
The direct-call candidate's 9.97% worker spread limits attribution.
The wide call and ordinary split take consistently longer, and the
invoice takes less. Elapsed and CPU provide no basis to attribute the
entire range to cleared stack slots. A follow-up can isolate the
direct-return frame from the added compact/wide call dispatch, if
whole-script attribution justifies it. Local allocations are effectively
flat and admitted peaks match.

Both platforms pass 609 white-box and 383 public Release checks.
The first Ubuntu continuation test saw a successful guest run on a
different thread from the expected one. Six isolated runs per revision
pass the strengthened fixture on the required 256 KiB continuation
stack. The new recursive function has a fifteen-slot native layout.
Windows also passes that strengthened case. The archive retains the
thread-handoff failure and the initial Windows probe build conflict.
Eight CPython goldens match, and 25 code objects have identical
executable metadata. All 196 quiet batches verify, workers join and
the persistent archive verifies under the lease.

[Timings](summary.json) · [Generated code](native-jit-summary.json) ·
[Stack fixture metadata](stack-fixture-code.json) · [Images](source.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).

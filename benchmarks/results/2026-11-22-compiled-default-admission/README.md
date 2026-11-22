# B1396 Admit compiled defaults together

The quiet Azure F4as_v7 compared frozen `c097d73db` and `fb5f9dbe7`
on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3.
The exact runtime patch is retained at `b0e35f20b`, which also includes
the independently delivered regex and compiler changes.

One host reservation covers compiled positional and keyword default
capacities before either allocation. Existing operand owners hold each
part through evaluation or suspension, and refund it if the definition
does not publish. The evaluated values, alias identities, and order of
guest effects are preserved. The host observes one combined request at
the same preflight and can refuse it immediately.

| Complete workload | Boundary | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −3.94 to −1.00% | −3.50 to −0.16% | 1.17 / 1.87% |
| Postponed nested factories | Warm | −3.71 to −1.09% | −3.59 to +0.05% | 2.60 / 0.12% |
| Factories that read default tuples | Warm | −5.10 to +0.01% | −5.37 to −0.06% | 2.32 / 2.99% |
| Fully supplied helper | Warm | −1.38 to +0.30% | −2.35 to +0.19% | 0.91 / 0.78% |
| Postponed nested factories | Compile to result | −4.70 to +3.27% | −4.52 to +2.94% | 0.30 / 8.03% |

Two fresh processes per source warm for eight seconds and complete
seven rotated batches each. Ranges span pairings of process medians.
The warm clock includes executing the compiled script, verifying output
and disposing the prior result. Time to result adds compilation and
compiled-owner disposal. The scripts use no guest file I/O. Wider CPU
includes request handling. Startup and final teardown have their own
boundaries.

Local Windows probes observe 8,217 to 6,169 growth requests in each
unobserved factory script and 8,218 to 6,170 for the tuple observer.
Local allocation matches on those three scripts. The dynamic
keyword-default script and fully supplied helper make the same number
of requests on both images. All execution and compile owners refund.
Postponed first-use elapsed and CPU span zero with 8.03% candidate
worker spread. Retain that uncertainty alongside the warm result.

Both platforms pass 1,283 focused Release checks, including terminal
refusal before side effects, cancellation and retained aliases.
Initial isolated test runs lacked a fixture and loaded a different
worker. The corrected runs use the checked fixture and verify the
worker's core identity. Both frozen comparison images match four
CPython goldens. All 140 timed batches and both quiet machine gates
pass, and the archive verifies with all workers joined and the guest
mount removed.

[Source and images](source.json) · [Timing](summary.json) ·
[Admission counts](local-reservations.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).

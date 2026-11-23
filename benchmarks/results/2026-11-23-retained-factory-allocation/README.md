# Allocation of the retained nested factories

The exact `fe081752a` production image ran the postponed and eager
factory scripts locally on Windows x64 with .NET 11 RC3. Each script
warms through the existing comparison worker before a six-second
EventPipe allocation sample. Both scripts match the CPython goldens.
All 286 captured batches verify. This is allocation attribution.

| Sampled type | Postponed weight | Eager weight |
| --- | ---: | ---: |
| Executable function | 48.06% | 48.31% |
| BigInteger | 22.07% | 22.10% |
| Singleton positional default | 15.10% | 14.94% |
| Executable closure cell | 13.09% | 12.98% |

Weights approximate bytes attributable to the triggering sampled type.
The stack inspection places roughly two thirds of integer ticks in
the outer instruction dispatcher and one third in the native prefix,
near closure reads used for defaults. Inlining and IL mapping leave
the precise publication sites in the dispatcher unresolved.

The independent admission probe records 6,169 positive growths for
each factory. Each of 2,048 inputs makes a 256-byte admission request
for the outer callable, a 520-byte request for default storage and a
672-byte request for the inner callable and annotations. The final
request begins in annotation preparation for eager annotations and
in callable construction for postponed annotations. Both probes
verify output and return their allowances after cleanup.

This favors removing callable or capture materialization before
changing admission frequency again. The earlier combined admission
trial moved postponed warm elapsed adversely. [Admission trial](../2026-11-23-single-owner-function-preflight/README.md).

[Image and capture identity](identity.json) · [Allocation and admission](summary.json) ·
[Raw traces and procedures](observations-and-procedures.tar.xz).

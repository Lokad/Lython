# Bulk Unicode lowercasing: withdrawn, unqualified (B76)

2026-11-01. The bounded candidate is removed. No qualified speedup or current
CPython ratio is claimed. The independent final-output cancellation correction
at `4cae38f4` remains in production and was present in every compared image.

## Hypothesis and semantic boundary

Replace the entire scalar decode/map/builder pipeline for ordinary short lower()
calls with managed bulk UTF-8 decode, invariant string lower, then funded UTF-8
encoding. Eligibility was fixed before code: nonempty input at most 4,096 UTF-8
bytes, excluding U+0130 and U+03A3. Larger/context-sensitive inputs and other
casing operations retained their existing cooperative implementation.

The candidate admitted `128 + 4 * inputBytes` for two temporary UTF-16 strings
before allocation. Final UTF-8 construction overlapped that funded scratch; only
an unpublished result could be refunded after cancellation. Higher temporary
allowance and earlier rejection were explicit proposed tradeoffs. No new owner,
pool, cache, policy, dependency or unsafe code was introduced.

An exhaustive gate compares 1,112,062 valid scalars, excluding the two special
cases, both alone and inside `A<scalar>z`. Windows .NET 11 RC3/10.0.8 and Ubuntu
.NET 11 RC3/10.0.12 have zero string/Rune lower differences. This is semantic
equivalence to the existing mapping, not complete Python or timing qualification.
Existing Python expansion/context goldens and the actual casing/ownership paths
pass. Candidate checks: **619 native / 2,823 public per Windows runtime**.

## One controlled complete-workload comparison

Azure Standard_F4as_v7, Ubuntu 24.04, ordinary .NET 11 RC3 GC/tiering. One process,
three isolated library images sharing CoreLib and hash witnesses: original,
candidate, identical original control. Unchanged complete scripts include fixture
reset, mediated I/O, stdout/file goldens, result disposal and budget checks.
Startup and compilation are excluded; no new compilation/startup claim is made.

Frozen controls: 256 interleaved warmups per image, two-second settle per case,
seven rotating batches of 32 jobs. Retention required at least 10% improvement
in **both** text jobs against **both** originals, CSV regression/original drift
at most 3%, IQR at most 10%, batches above 100 ms, and both quiet-machine gates.

| Workload | Original batch ms | Candidate batch ms | Identical original ms | Candidate change vs originals |
| --- | ---: | ---: | ---: | --- |
| Text plain | 183.613 | 164.956 | 183.567 | -10.16% / -10.14% |
| Text helpers | 213.809 | 196.000 | 209.904 | -8.33% / -6.62% |
| CSV helpers, control | 262.190 | 260.495 | 263.648 | -0.65% / -1.20% |

**These observations are unqualified.** Helper text misses the materiality gate.
The final idle sample is 4.48% busy, failing the quiet gate; the service exits 3
after 38.510 seconds. Initial quiet gate, original drift, batch duration and IQR
checks pass. All images verify complete goldens, per-job balances, final zero
allowance and normal fixture removal. The process is joined, its control group
empty and the VM lease free. No retiming, narrower retained fragment or tuning.

A preceding 249 ms launcher attempt failed on CRLF shell-script paths before
starting Lython. Its journal and terminal state are preserved. Only line endings
were corrected before the sole actual comparison; the malformed empty lock file
was validated and removed. It is not another timing sample.

## Removal and provenance

The entire production branch, relaxed linear-allocation assertion and prototype
boundary cases are removed. Original casing source/tests are restored byte for
byte. A fresh nonincremental Release build passes **269 native / 633 public per
Windows .NET 11 RC3/10.0.8**; the original allocation assertion is unchanged.

The original image was built nonincrementally with the exact cancellation-fix
source before its commit; its informational revision is `46ed95e5`. Candidate
and restored builds identify `4cae38f4`. The distinct restored binary hash is
recorded explicitly alongside source restoration proof; it is not claimed to be
the original binary. Every original image includes the independent correction.

[Exact identities, gates, samples and checks](qualification.json),
[withdrawn source](source.patch), [withdrawn test adaptations](tests.patch.gz), and
[raw evidence](comparison-evidence.tar.gz), SHA256
`825298a6513b369a92377ee1905aad41390f165c4af56715ee9b65d408986fae`,
preserve the failed gates and terminal state. This closes the bounded bulk-lower
trial; the higher-peak representation is not retained on these results.

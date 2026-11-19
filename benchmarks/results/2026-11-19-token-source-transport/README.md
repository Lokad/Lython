# B1105 source positions and numeric adjacency during tokenization

R673 prepares physical line indexes and malformed number/identifier
boundaries in the token scan. Ordinary parser spans reuse those indexes,
and the parser reports only recorded adjacency errors before its other
diagnostics. Arbitrary positions and embedded f-string offsets still use
the newline table. Each token grows from 12 to 16 bytes.

The measured candidate is `050e2dc5e`, retained as `27f68744b`. The
reference image `bc65698dd` has the production and test sources of
`80cbf4988`. Windows .NET 11 and Ubuntu .NET 10 each passed 277 focused
Release tests. Both complete 32-file images passed five CPython goldens.
An additional 20 source probes matched the reference through each of
`Run` and `RunAsync`, covering numeric adjacency, line continuations,
multiline strings, embedded fields and diagnostics. The local compilation
diagnostic allocates 1.77% more for the validator and 1.59% more for the
manifest. Accounted execution peaks agree and owners return to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Ranges span all four combinations of duplicate process medians.
Compilation includes compiled-owner disposal. Compile-to-result adds
mediated I/O, result verification and cleanup. Warm uses an already
compiled script. Process CPU covers the wider request.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated validator | Compilation | −11.70…−6.06% | −12.56…−6.98% |
| Configuration manifest | Compilation | −3.44…−1.27% | −2.32…−1.32% |
| Generated validator | Compile-to-result | −7.56…−6.00% | −7.96…−5.27% |
| Configuration manifest | Compile-to-result | −1.14…−0.38% | −1.06…−0.32% |
| Shipment report control | Warm | −2.32…+1.39% | −2.26…+0.78% |

The validator's compilation reference medians differ by 4.69%. Every
worker pairing still improves, and compile-to-result has less than 1%
same-image spread in each role and lower process CPU. Retain the removed
scan and lookup work with the measured allocation cost. The warm control
remains inconclusive.

All 100 timed batches completed in 272.00 seconds. Both quiet gates passed,
workers joined, guest fixtures were removed and the archive was verified
under the lease. The VM retired one redundant B1103 core image after
verifying its exact byte recovery from the committed archive.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).

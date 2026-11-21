# B1258 map-based text reporting against CPython

B1258 measures frozen Lython `8ad55d9d5` against stock CPython 3.13.16
on November 21, 2026 UTC. The Azure F4as_v7 runs Ubuntu 24.04.4 and .NET
11 RC3. The exact worker image comes from the callback change retained
at `d6a360e23`. [Measured source pair](../2026-11-21-native-callback-entry/README.md).

The report reads 1,024 three-line groups from a 35,840-byte file. A Python function
passed to `map` normalizes and splits each line. The script aggregates
6,144 words and writes an ordered report. Both workers validate stdout
and the output file.

| Boundary | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm | 1.539–1.581 ms | 1.188–1.205 ms | 1.277–1.330× |
| Compilation only | 0.02961–0.02966 ms | 0.04158–0.04173 ms | 0.710–0.713× |
| Compile-to-result | 1.622–1.633 ms | 1.311–1.320 ms | 1.229–1.245× |

Each range compares two independently warmed process medians per
engine and phase. The warm Lython control spread is 2.74% and CPython's
is 1.38%. Compile-to-result controls are within 0.68% and compile
controls are within 0.34%. Maximum elapsed IQR is 0.90%.

Warm elapsed covers guest execution, mediated RAM-backed file I/O,
output verification and disposal of the prior result. Compile-to-result
adds compilation and code-owner disposal. Compilation alone excludes
guest execution. Startup, fixture reset and final teardown are outside
the guest clocks. Both engines execute the same input and output task
under their respective runtime containment boundaries.

All 84 timed batches and both quiet checks pass. The full worker
image, CPython executable and protocol hashes, output goldens and
archives verify. All workers join and guest fixtures are removed.

[Summary and controls](summary.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).

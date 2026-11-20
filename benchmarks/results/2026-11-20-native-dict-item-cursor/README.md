# B1251 dictionary traversal in JSON encoder frames

The JSON encoder keeps a versioned dictionary cursor in its admitted
frame. Native unsorted dictionaries no longer allocate two nested
enumerators per object. Ordinary item enumeration shares the same
native scan. Namespace-backed dictionaries keep their existing
storage enumerator. Sorted keys, cycle detection, chunk boundaries
and mutation checks stay on the shared encoder.

B1251 compares `cb84980ca` with the frozen prototype `5901c06bd` on
November 20, 2026 UTC. The quiet Azure F4as_v7 runs Ubuntu 24.04.4 and
.NET 11 RC3. The change is retained at `ea04028b5` alongside the
independent regex module correction.

| Complete warm workload | Elapsed change | Process CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| JSON normalization, 3,072 records | −7.07 to −2.35% | −6.70 to −1.85% | 2.42 / 2.60% |
| Guarded manifest | −1.33 to −0.65% | 0.00 to +0.36% | 0.51 / 0.18% |
| Validator, 96 fields, no output JSON | −1.24 to +2.32% | −0.38 to +2.24% | 1.41 / 2.17% |

Each range compares all pairings of two reference and two candidate
worker medians. Each worker warms for 12 seconds and contributes
seven batches. The JSON normalizer reads and writes 189,441 bytes each.
Both clocks improve across all worker pairings. The manifest shows
slightly lower elapsed and flat to slightly higher CPU. The validator
control spans zero on both clocks. These numbers compare two Lython
revisions with CPython output goldens.

Warm elapsed includes execution, mediated RAM-backed file I/O, output
checks and prior-result disposal. Process CPU covers the wider worker
request. Compilation, process startup and final teardown are outside
the warm clocks. All 84 batches and both quiet checks pass. Workers
join, guest fixtures are removed and the raw archive verifies under
the lease.

Local CLR allocation falls 11.00% on normalization and 0.31% on the
manifest. The validator stays flat. Accounted peaks match in all
three cases and the borrowed allowance returns to baseline. Both
32-file images verify against all three complete-script goldens.
The exact prototype passes 1,008 public and 222 white-box focused
Release checks per platform, Windows .NET 11 and Ubuntu .NET 10.
A CPython probe confirms that an encoder callback can change the
next value and that adding a key invalidates the traversal. The
regression also exercises real host suspension and allowance reuse.

[Summary and controls](summary.json) · [Raw observations](observations.tgz) ·
[Allocation diagnostic](allocation-summary.json) ·
[Source, image verification and procedures](checks-and-procedures.tar.xz).

# B1889 Retain shared JSON layouts across omitted trailing fields

A JSON object whose keys match an initial segment of a shared layout can
keep that layout through publication. The omitted fields have no value in
the object. This avoids building private key storage for the shorter row
and rebuilding the wider layout on the next row. Each record still owns
its values. An unseen tail retains its earlier strict-name proof. Ordinary
dictionary insertion still admits private storage and keeps key order.

The reference image contains `126d551c2`. The candidate adds the archived
patch over `914b8a27b`. Only the core DLL differs between the two 32-file
images. Both match four independent CPython 3.13.16 output and file
goldens. The sparse fixture modifies the existing 4,096-record JSON Lines
input. Half the nested detail objects have two additional trailing
fields. The input is 231,424 bytes including the original blank lines.
The aggregation and output report stay the same.

| Hosting mode | Reference CLR bytes | Candidate CLR bytes | Reference peak admission | Candidate peak admission |
| --- | ---: | ---: | ---: | ---: |
| `Run` | 4,028,518 | 2,357,758 | 8,850,803 | 6,074,527 |
| `RunAsync` | 4,028,734 | 2,358,030 | 8,850,803 | 6,074,527 |

The Windows Release witnesses save about **41.5% allocation** and
**31.4% peak admission**. The dense document, dense Lines and file-fed
map controls match admitted peaks. Their CLR results differ by at most
224 bytes per invocation. Every command restores its shared allowance
baseline.

Regression coverage checks absence of omitted keys, ordered insertion,
separate owned values across rows and strict decoding after keeping a
layout learned in permissive mode. Results survive a later execution
using the same allowance. CPython independently confirms the new
regression's expected result. Windows and Ubuntu WSL each pass 938
public and 261 white-box checks per Release target on .NET 10 and 11.

## Matched local timing

Ubuntu WSL and .NET 11 RC3 use one logical CPU, two workers per image,
at least five seconds of warmup per worker and seven rotated batches.
Warm elapsed includes mediated RAM-backed I/O, verification and prior
result cleanup. Process CPU also covers fixture reset and framing.
Compilation and startup are outside the clocks. Ranges span all four
pairings of worker medians.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested Lines with optional fields | `Run` | −18.74% to −12.97% | −17.32% to −13.57% |
| Dense nested document | `Run` | −5.15% to −0.59% | −3.30% to +2.86% |
| Nested Lines with optional fields | `RunAsync` | −19.52% to −18.67% | −19.36% to −17.82% |
| Dense nested Lines | `RunAsync` | −2.08% to +0.57% | −2.95% to −0.81% |
| File-fed CSV map control | `RunAsync` | −5.30% to −0.68% | −4.43% to +2.68% |

The sparse report improves across both local timing boundaries.
Dense document and CSV CPU span zero. Keep the CSV elapsed movement
with the observations. All 140 batches verify in 187.26 seconds.
Workers join and fixtures are removed before the archive verifies.
The first fixture preparation stopped before verification because it
treated a blank line as a JSON record. The measured fixture preserves
the original blank lines and verifies independently in both images.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Exact source and checks](procedures.tar.xz).

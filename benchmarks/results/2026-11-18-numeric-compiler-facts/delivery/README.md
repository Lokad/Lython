# Qualified shared numeric compiler facts

[Run 44299190817](https://github.com/Lokad/Lython/actions/runs/44299190817) passed
for committed dd4c5165c. Windows and Ubuntu each passed actual .NET 11 and .NET
10, with 4,863 white-box and 18,053 public ordinary tests per target, 14
white-box and 406 public original-scale stress tests, and installed Release
package consumers. All 16 test summaries and 2,391 committed inputs were
verified. All 588 production source files match the measured 29192b95 source
after line-ending normalization. Rebuilt binaries have separate identities.

The retained change removes duplicate parsing across compiler consumers.
Whole-script CPU improvement remains inconclusive. The later regex and JSON
changes have their own delivery boundary. Publication remains postponed.

[Qualification](qualification.json) · [Logs](run-logs.zip) ·
[Committed input hashes](input-hashes.json) · [Source comparison](../comparison/README.md)

# Refund private JSON traversals

A focused probe on the B1618 source repeatedly writes a one-byte
document to the same buffered file. The iterator never reaches
guest code. The reference retains the 224-byte shell after each
dump. An isolated prototype refunds the shell and its remaining
traversal scratch after the exact builtin consumer finishes. Custom
encoder results can escape and retain their existing ownership.

| Dumps in one execution | Reference peak | Prototype peak |
| ---: | ---: | ---: |
| 64 | 16,702 B | 2,572 B |
| 256 | 59,903 B | 2,788 B |
| 1,024 | 232,704 B | 3,940 B |

Both public execution modes give these peaks and return the
allowance to baseline after disposal. Equivalent buffered writes
without encoding peak at 3,524 bytes for 1,024 writes. The same
four focused cases fail on the reference and pass the prototype
under 96 KiB, covering default and explicit builtin options. The
prototype passes 85 focused checks each on Windows and Ubuntu.
These are ownership and correctness observations on a private
source pair. This probe does not report elapsed or CPU changes.

[Admission measurements](ownership-summary.json) · [Exact patches and test logs](evidence.tar.xz).

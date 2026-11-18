# Native string slice admission

R539 started with a 128 KiB unowned ASCII receiver and an exhausted execution
allowance. The contiguous path allocated 394,384 bytes and the strided path
197,800 bytes before reporting the original shared-allowance refusal. Both
first created an unowned UTF-8 result, then decoded it while transferring the
result to the active owner.

The fix passes the active owner, span and cancellation to native slice
construction, including contiguous affix removal. Contiguous output reuses
the existing admitted byte range path. Strided output writes directly into
admitted UTF-8 storage. Cold unowned Unicode inputs can use a temporary
admitted table during scalar slicing. The table stays local to the operation
and refunds on failure or completion. Existing retained backing and already
available indices can be reused. Large contiguous copies check cancellation
between bounded blocks, and output allocation follows admission.

The four refusal regressions cover contiguous and strided ASCII and cold
Unicode. They preserve the failure reason, span, original budget balance
and an allocation bound below one attempted payload. Local Windows passed
778 white-box and 2,527 public focused checks on each actual target.
The final regressions and regex rewind test passed under Ubuntu for both
targets using the same assemblies. A separate probe matched CPython for
1,260 slices and 60 affix results through sync and async on both targets.

This records a correctness and ownership change. Complete-script performance
and full combined qualification remain separate checkpoints.

[Source patch](source.patch) · [Build, failure and parity logs](checks.tar.xz)

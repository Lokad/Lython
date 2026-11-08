R125 before implementation: baseline regression2fails. Active read owns input
buffers and file transport until its full native copy/decode/async I/O use ends.
Release during that borrow is a retirement request, not authority to refund live
storage. Use the existing reader's state and existing file close helpers; no new
ownership abstraction. Defer cleanup to operation completion, preserve first errors
and await real users. Normal close outside a read is unchanged. Reentrant reads
are unsupported and fail explicitly. No string-view/storage-offset prototype yet.
Focused Release and native live-budget tests, existing codec/line/cancellation/
file-lifetime checks, one short existing whole-script comparison; full platform/
stress/package at retained coherent delivery. No CPU speed claim from a safety fix.

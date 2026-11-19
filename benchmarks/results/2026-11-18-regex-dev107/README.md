# Development semantics with Utf8Regex 0.5.0-dev.107

The optional development adapter from dev.106 was exercised with the
immutable Core and PythonRe packages from `7be42c66`. Package and target
assembly SHA-256 values were verified against the producer receipt. The
isolated probe images use Lython source `7a0bc456b`, and their complete
contents were verified before every run.

Seven cases each passed against CPython 3.13 on .NET 10 and .NET 11 with
both execution APIs. They exercise anchored final-character screening,
delimiter windows, bounded repeats, Unicode, captured lookbehind and original-
subject windows. They check capture metadata and matches retained after
`re.purge()`. They also check split and callback replacement. This
qualifies the changed matching paths for development.

The dev.107 source accepts a token per pull and also honors the cursor's
creation token. Retained ranges draw from the cursor allowance. The
producer uses `Unsafe.SizeOf`, and the released adapter pin remains 0.4.0.
Final cancellation, ownership, release and backend timing qualification
remain separate from these semantic checks.

[Verified images and checks](inputs-and-checks.json) · [Exact probes and producer receipt](probes.tar.xz)

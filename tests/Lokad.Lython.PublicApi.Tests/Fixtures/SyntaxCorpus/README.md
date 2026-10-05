# Syntax corpus

See [the grammar inventory](../../../../SUPPORTED_SYNTAX.md) for the mapping to
SPEC and the precise unsupported boundaries. `cases.json` promotes the 125
release-review snippets into durable regressions and adds explicit await,
async-for and async-with exclusions.

`status` describes Lython's contract: `supported`, `invalid`, `unsupported`, or
the intentional top-level-return `extension`. `standardOutput`, `success` and
`exceptionType` are CPython oracle values for the original program, not results
copied from Lython. `note` explains temporary boundaries and extensions. Case
names are stable test identifiers; `family` links them to the inventory.

The public test asserts the expected compilation phase, diagnostics with source
spans, zero output for rejected programs, and compile-once reuse in both modes.
Unsupported fixtures retain their Python outputs to document what a future
implementation must produce when promoted to `supported`.

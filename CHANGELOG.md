# Changelog

## 0.4.0 - 2026-06-04

This release broadens Lython's Python compatibility for coding-agent scratch scripts and text-editing workflows while preserving the host-mediated containment model.

### Python Language Compatibility

- Added multiline list, tuple, set, dictionary, call, and subscript syntax support for common Python formatting styles.
- Expanded f-string support, including format specifiers, conversions, debug expressions, raw/triple-quoted variants, and nested expressions.
- Added one-line compound suites for `if`, loops, `try`, `with`, `match`, class, and function definitions.
- Filled in common builtins used by scratch scripts, including practical `iter`, `next`, `sum`, `min`, `max`, `abs`, `round`, `enumerate`, `zip`, `reversed`, `all`, and `any` behavior.

### Text Editing And File Workflows

- Made file handles first-class for supported text I/O, including `close()`, closed-state checks, iteration, and context-manager behavior.
- Added richer path and filesystem compatibility across `pathlib` and `os`, including path-like arguments, path properties, globbing, stat/scandir-style objects, rename/replace/remove helpers, and directory creation variants.
- Added `sys.exit(...)` support with structured exit results.

### Standard Library Surface

- Added `difflib` support for unified/context diffs, ndiff/restore, close matches, and `SequenceMatcher`.
- Added `pkgutil` module discovery helpers for the supported import model.
- Expanded `datetime` compatibility for date/time constructors, parsing, formatting, arithmetic, comparisons, and timedeltas.
- Improved `re.findall(...)` list compatibility.

### Host-Mediated Subprocesses

- Broadened the optional `subprocess` module beyond `run(...)` with `call(...)`, `check_call(...)`, and `check_output(...)`.
- Added `subprocess.PIPE`, `STDOUT`, and `DEVNULL`, plus host request fields for stdin/stdout/stderr modes, `shell`, text mode, encoding, errors, environment, timeout, and output bounds.
- Added `CompletedProcess.args` and `CompletedProcess.check_returncode()`.
- Kept process execution explicit and host-mediated; embedders still decide whether subprocesses exist and how requests are authorized.

### Static Analysis And Packaging

- Expanded static contracts for the new Python, filesystem, datetime, module, and subprocess surfaces.
- Added package release notes metadata and packaged this changelog with the NuGet artifact.

"""Explicit CPython correctness check; no comparative performance measurements."""

import argparse
import contextlib
import gc
import hashlib
import io
import json
import os
import pathlib
import platform
import sys
import sysconfig
import uuid


def digest(value):
    return hashlib.sha256(value.encode("utf-8", errors="strict")).hexdigest()


def verify(case):
    source = case["source"]
    fixture = case["fixtureJson"]
    expected = case["expectedOutput"]
    receipt = {
        "id": case["id"],
        "sourceSha256": digest(source),
        "fixtureSha256": digest(fixture),
        "expectedOutputSha256": digest(expected),
        "status": "Failure",
    }
    try:
        for field in ("sourceSha256", "fixtureSha256", "expectedOutputSha256"):
            if receipt[field] != case[field]:
                raise ValueError("Catalog digest mismatch: " + field)
        if len(source) > 1_000_000 or len(expected.encode("utf-8")) > 16 * 1024 * 1024:
            raise ValueError("Catalog exceeds ordinary source/output caps")
        json.loads(fixture)
        code = compile(source, case["id"] + ".py", "exec")
        for repetition in range(2):
            stdout = io.StringIO()
            stderr = io.StringIO()
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                exec(code, {"__name__": "__main__"})
            actual = stdout.getvalue()
            receipt["actualOutputSha256"] = digest(actual)
            receipt["standardError"] = stderr.getvalue()
            receipt["completedInvocations"] = repetition + 1
            if actual != expected or stderr.getvalue():
                receipt["status"] = "Mismatch"
                receipt["reason"] = "Complete output differs from the independent golden value"
                return receipt
        receipt["status"] = "Equivalent"
    except BaseException as failure:
        receipt["reason"] = type(failure).__name__ + ": " + str(failure)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=pathlib.Path, required=True)
    parser.add_argument("--out", type=pathlib.Path, required=True)
    args = parser.parse_args()
    if not sys.flags.isolated or not sys.flags.no_site or not gc.isenabled():
        parser.error("Launch with -I -S and ordinary enabled cyclic GC")
    catalog_bytes = args.catalog.read_bytes()
    catalog = json.loads(catalog_bytes.decode("utf-8", errors="strict"))
    if catalog["schemaVersion"] != 1 or catalog["catalogVersion"] != 1:
        parser.error("Unsupported catalog schema/version")
    cases = catalog["cases"]
    if len({case["id"] for case in cases}) != len(cases):
        parser.error("Duplicate case identifiers")
    receipts = [verify(case) for case in cases]
    with open(sys.executable, "rb") as stream:
        executable_digest = hashlib.file_digest(stream, "sha256").hexdigest()
    output = {
        "schemaVersion": 1,
        "catalogSha256": hashlib.sha256(catalog_bytes).hexdigest(),
        "verifierSha256": hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),
        "reference": {
            "implementation": platform.python_implementation(),
            "version": sys.version,
            "executable": sys.executable,
            "executableSha256": executable_digest,
            "flags": repr(sys.flags),
            "configArgs": sysconfig.get_config_var("CONFIG_ARGS"),
            "platform": platform.platform(),
            "pid": os.getpid(),
            "gcEnabled": gc.isenabled(),
        },
        "performanceQualified": False,
        "cases": receipts,
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.out.with_name(args.out.name + "." + uuid.uuid4().hex + ".partial")
    try:
        temporary.write_text(json.dumps(output, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
        temporary.replace(args.out)
    finally:
        temporary.unlink(missing_ok=True)
    failures = [row for row in receipts if row["status"] != "Equivalent"]
    print(str(len(receipts) - len(failures)) + "/" + str(len(receipts)) + " complete golden comparisons; no timings collected")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())

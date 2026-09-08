"""Regenerate the contained-`zipfile` (Z0) fixture catalog.

Usage (repository root):
    python tools/NewZipFixtures.py

Uses the local CPython 3.13 interpreter to author small, synthetic, fully
deterministic archives under tests/Fixtures/zipfile/cases/<id>/, each with an
`input.zip` payload and a `case.json` manifest. Every entry carries an explicit
`date_time`, so generation never consults the ambient clock; compressed bytes
may vary with the zlib build (recorded in the manifest) but all asserted facts
(names, sizes, CRCs, comments, behavior) are stable. Manifests capture trusted
CPython observations (namelist, per-entry metadata, read/testzip outcomes,
error categories) that later stages assert against.
"""
import binascii
import io
import json
import os
import struct
import sys
import zlib
import zipfile

CASES_ROOT = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..",
    "tests",
    "Fixtures",
    "zipfile",
    "cases",
)
FIXED_TIME = (2024, 2, 29, 12, 34, 56)
MAX_FIXTURE_BYTES = 32 * 1024


def dos_datetime(date_time):
    dos_time = (date_time[5] // 2) | (date_time[4] << 5) | (date_time[3] << 11)
    dos_date = date_time[2] | (date_time[1] << 5) | ((date_time[0] - 1980) << 9)
    return dos_time, dos_date


def zip_info(name, content, compress=zipfile.ZIP_STORED, date_time=FIXED_TIME,
             comment=b"", extra=b"", create_system=0):
    info = zipfile.ZipInfo(filename=name, date_time=date_time)
    info.compress_type = compress
    info.comment = comment
    info.extra = extra
    info.create_system = create_system
    return (info, content)


def build_archive(entries, comment=b""):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as handle:
        for info, content in entries:
            handle.writestr(info, content)
        handle.comment = comment
    return buffer.getvalue()


def build_raw_archive(records, comment=b"", zip64_end=False):
    """Hand-craft STORED entries with explicit raw name bytes and flag bits."""
    chunks = []
    central = []
    offset = 0
    for record in records:
        name = record["nameBytes"]
        content = record["content"]
        flags = record.get("flags", 0)
        method = record.get("method", 0)
        crc = zlib.crc32(content) & 0xFFFFFFFF
        dos_time, dos_date = dos_datetime(record.get("date_time", FIXED_TIME))
        descriptor = record.get("descriptor", False)
        local_crc, local_csize, local_usize = crc, len(content), len(content)
        if descriptor:
            local_crc, local_csize, local_usize = 0, 0, 0
        header = struct.pack(
            "<IHHHHHIIIHH", 0x04034B50, 20, flags, method,
            dos_time, dos_date, local_crc, local_csize, local_usize,
            len(name), 0,
        )
        chunks.append(header + name + content)
        if descriptor:
            chunks.append(struct.pack("<IIII", 0x08074B50, crc, len(content), len(content)))
        central.append((name, flags, method, dos_time, dos_date, crc,
                        len(content), offset, record.get("extra", b""),
                        bool(record.get("zip64_placeholder", False))))
        offset += len(header) + len(name) + len(content)
        if descriptor:
            offset += 16
    directory_offset = offset
    directory = b""
    for (name, flags, method, dos_time, dos_date, crc, size, local_offset, extra, placeholder) in central:
        if placeholder:
            extra = struct.pack("<HHQQQ", 0x0001, 24, size, size, local_offset)
            directory += struct.pack(
                "<IHHHHHHIIIHHHHHII", 0x02014B50, 45, 45, flags, method,
                dos_time, dos_date, crc, 0xFFFFFFFF, 0xFFFFFFFF, len(name), len(extra),
                0, 0, 0, 0, 0xFFFFFFFF,
            ) + name + extra
            continue
        directory += struct.pack(
            "<IHHHHHHIIIHHHHHII", 0x02014B50, 20, 20, flags, method,
            dos_time, dos_date, crc, size, size, len(name), len(extra),
            0, 0, 0, 0, local_offset,
        ) + name + extra
    offset += len(directory)
    if not zip64_end:
        end = struct.pack("<IHHHHIIH", 0x06054B50, 0, 0, len(central),
                          len(central), len(directory), directory_offset, len(comment))
        return b"".join(chunks) + directory + end + comment
    eocd64_offset = offset
    eocd64 = struct.pack("<IQHHIIQQQQ", 0x06064B50, 44, 45, 45, 0, 0,
                         len(central), len(central), len(directory), directory_offset)
    locator = struct.pack("<IIQI", 0x07064B50, 0, eocd64_offset, 1)
    end = struct.pack("<IHHHHIIH", 0x06054B50, 0, 0, 0xFFFF,
                      0xFFFF, 0xFFFFFFFF, 0xFFFFFFFF, len(comment))
    return b"".join(chunks) + directory + eocd64 + locator + end + comment


def probe_observations(payload):
    """Trusted CPython observations for one payload."""
    result = {"isZipfile": zipfile.is_zipfile(io.BytesIO(payload))}
    try:
        with zipfile.ZipFile(io.BytesIO(payload)) as handle:
            result["openError"] = None
            result["archiveCommentHex"] = handle.comment.hex()
            infos = []
            for info in handle.infolist():
                infos.append({
                    "name": info.filename,
                    "utf8Flag": bool(info.flag_bits & 0x800),
                    "descriptorFlag": bool(info.flag_bits & 0x8),
                    "compressType": info.compress_type,
                    "fileSize": info.file_size,
                    "compressSize": info.compress_size,
                    "crc32Hex": "%08x" % info.CRC,
                    "dateTime": list(info.date_time),
                    "commentHex": info.comment.hex(),
                    "createSystem": info.create_system,
                    "externalAttr": info.external_attr,
                    "headerOffset": info.header_offset,
                    "extraHex": info.extra.hex(),
                })
            result["entries"] = infos
            result["namelist"] = handle.namelist()
            result["testzip"] = handle.testzip()
            reads = []
            for info in handle.infolist():
                try:
                    with handle.open(info) as member:
                        reads.append({"hex": member.read().hex(), "error": None})
                except Exception as exc:
                    reads.append({"hex": None,
                                  "error": {"type": type(exc).__name__, "message": str(exc)}})
            result["readByIndex"] = reads
            by_name = []
            for name in handle.namelist():
                try:
                    by_name.append({"name": name, "hex": handle.read(name).hex(), "error": None})
                except Exception as exc:
                    by_name.append({"name": name, "hex": None,
                                    "error": {"type": type(exc).__name__, "message": str(exc)}})
            result["readByName"] = by_name
    except Exception as exc:
        result["openError"] = {"type": type(exc).__name__, "message": str(exc)}
        result["archiveCommentHex"] = None
        result["entries"] = []
        result["namelist"] = []
        result["testzip"] = None
        result["readByIndex"] = []
        result["readByName"] = []
    return result


def write_case(case_id, payload, tags, notes=""):
    assert len(payload) <= MAX_FIXTURE_BYTES, (case_id, len(payload))
    directory = os.path.join(CASES_ROOT, case_id)
    os.makedirs(directory, exist_ok=True)
    with open(os.path.join(directory, "input.zip"), "wb") as stream:
        stream.write(payload)
    manifest = {
        "id": case_id,
        "kind": "zip",
        "input": "input.zip",
        "producer": "CPython %s" % sys.version.split()[0],
        "producerZlib": zlib.ZLIB_VERSION,
        "createdByTool": "tools/NewZipFixtures.py",
        "tags": tags,
        "notes": notes,
    }
    manifest.update(probe_observations(payload))
    with open(os.path.join(directory, "case.json"), "w", encoding="utf-8") as stream:
        json.dump(manifest, stream, indent=2)
        stream.write("\n")
    print("%-22s %6d bytes entries=%d testzip=%r openError=%r" % (
        case_id, len(payload), len(manifest["entries"]),
        manifest["testzip"],
        manifest["openError"]["type"] if manifest["openError"] else None))
    return manifest


def main():
    os.makedirs(CASES_ROOT, exist_ok=True)

    write_case("zip-stored", build_archive([
        zip_info("hello.txt", b"hello stored\n"),
        zip_info("data/blob.bin", b"\x00\x01\xff\xfe" * 64),
    ]), ["stored", "focused"],
        notes="Two STORED members with fixed timestamps.")

    write_case("zip-deflated", build_archive([
        zip_info("default.txt", b"The quick brown fox jumps over the lazy dog. " * 20,
                 compress=zipfile.ZIP_DEFLATED),
        zip_info("level1.txt", b"aaaabbbbccccdddd" * 64, compress=zipfile.ZIP_DEFLATED),
        zip_info("level9.txt", b"0123456789abcdef" * 64, compress=zipfile.ZIP_DEFLATED),
        zip_info("empty.txt", b"", compress=zipfile.ZIP_DEFLATED),
    ]), ["deflated", "focused"],
        notes="DEFLATED members including an empty file.")

    dup = build_archive([
        zip_info("dup.txt", b"first\n"),
        zip_info("other.txt", b"other\n"),
        zip_info("dup.txt", b"second\n"),
    ])
    manifest = write_case("zip-duplicates", dup, ["duplicates", "identity", "focused"],
        notes="Duplicate names stay in list order; name lookup selects the last one.")
    assert manifest["namelist"] == ["dup.txt", "other.txt", "dup.txt"]
    assert manifest["readByName"][0]["hex"] == b"second\n".hex()
    assert manifest["readByIndex"][0]["hex"] == b"first\n".hex()

    write_case("zip-comments", build_archive([
        zip_info("a.txt", b"a\n", comment=b"per-file \xc3\xa9"),
        zip_info("b.txt", b"b\n", comment=b"raw \xff byte"),
    ], comment="archive Z0 \xc3\xa9 \xff raw".encode("latin-1")),
        ["comments", "metadata", "focused"],
        notes="Archive and per-file comments mix UTF-8 bytes with a raw 0xFF byte.")

    cp437 = build_raw_archive([
        {"nameBytes": "caf\xe9.txt".encode("cp437"), "content": b"cp437 ok\n"},
        {"nameBytes": b"plain.txt", "content": b"plain\n"},
    ])
    manifest = write_case("zip-cp437-names", cp437, ["cp437", "metadata", "focused"],
        notes="Raw CP437 name bytes without the UTF-8 flag; CPython decodes as CP437.")
    assert manifest["entries"][0]["name"] == "caf\xe9.txt", manifest["entries"][0]["name"]
    assert manifest["entries"][0]["utf8Flag"] is False

    write_case("zip-utf8-names", build_archive([
        zip_info("日本語.txt", "utf8 names\n".encode("utf-8")),
        zip_info("emoji-🎉.txt", b"party\n"),
    ]), ["utf8", "metadata", "focused"],
        notes="UTF-8 flag names; CPython round-trips them exactly.")

    write_case("zip-timestamps", build_archive([
        zip_info("min-dos.txt", b"min\n", date_time=(1980, 1, 1, 0, 0, 0)),
        zip_info("y2k.txt", b"y2k\n", date_time=(1999, 12, 31, 23, 59, 58)),
        zip_info("odd-second.txt", b"odd\n", date_time=(2024, 2, 29, 12, 34, 57)),
    ]), ["timestamps", "metadata", "focused"],
        notes="DOS date bounds and odd-second truncation (57 becomes 56).")

    zip64_payload = build_raw_archive(
        [{"nameBytes": b"tiny.txt", "content": b"zip64 forced\n",
          "zip64_placeholder": True}],
        zip64_end=True)
    manifest = write_case("zip-zip64", zip64_payload, ["zip64", "metadata", "focused"],
        notes="Small archive with true ZIP64 end structures (EOCD64 plus locator).")
    assert manifest["entries"][0]["fileSize"] == len(b"zip64 forced\n")
    assert manifest["entries"][0]["compressSize"] == len(b"zip64 forced\n")

    descriptor = build_raw_archive([
        {"nameBytes": b"stream.bin", "content": b"descriptor data\n" * 8,
         "flags": 0x08, "descriptor": True},
    ])
    manifest = write_case("zip-datadescriptor", descriptor, ["descriptor", "metadata", "focused"],
        notes="Local header carries a data descriptor (bit 3) with zeroed sizes.")
    assert manifest["entries"][0]["descriptorFlag"] is True
    assert manifest["entries"][0]["fileSize"] == len(b"descriptor data\n" * 8)

    write_case("zip-extra-field", build_archive([
        zip_info("extra.txt", b"extra\n",
                 extra=struct.pack("<HH", 0xCAFE, 4) + b"meta"),
    ]), ["extra", "metadata", "focused"],
        notes="Unknown extra field 0xCAFE must be preserved and ignored.")

    write_case("zip-dirs", build_archive([
        zip_info("docs/", b""),
        zip_info("docs/a.txt", b"nested\n"),
    ]), ["directories", "focused"],
        notes="Explicit directory entry plus a nested member.")

    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w"):
        pass
    write_case("zip-empty", buffer.getvalue(), ["empty", "focused"],
        notes="Valid end-of-central-directory with zero entries.")

    base = build_archive([zip_info("data.txt", b"0123456789abcdef")])
    at = base.find(b"PK\x01\x02")
    assert at >= 0
    corrupt_central = base[:at + 2] + b"\x03" + base[at + 3:]
    write_case("zip-corrupt-central", corrupt_central, ["corrupt", "integrity", "focused"],
        notes="First central-directory signature altered; open must fail deliberately.")

    base = build_archive([zip_info("data.txt", b"0123456789abcdef")])
    marker = b"0123456789abcdef"
    at = base.find(marker)
    assert at >= 0
    corrupt_crc = base[:at] + b"X" + base[at + 1:]
    manifest = write_case("zip-corrupt-crc", corrupt_crc, ["corrupt", "integrity", "focused"],
        notes="One flipped content byte; testzip names the member, read raises.")
    assert manifest["testzip"] == "data.txt", manifest["testzip"]

    base = build_archive([zip_info("data.txt", b"0123456789abcdef")])
    write_case("zip-truncated", base[:-30], ["corrupt", "integrity", "focused"],
        notes="End of central directory removed; recognition must report non-ZIP.")

    print("done.")


if __name__ == "__main__":
    main()

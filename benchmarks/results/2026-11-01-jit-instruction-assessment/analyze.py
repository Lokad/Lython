"""Offline B71 code recovery; no guest execution or new timing.

Format: https://raw.githubusercontent.com/torvalds/linux/master/tools/perf/Documentation/jitdump-specification.txt
Reject moved/overlapping code rather than assigning ambiguous historical samples.
"""
import collections, gzip, hashlib, json, re, struct, tarfile
from pathlib import Path

root = Path(__file__).resolve().parent
with tarfile.open(root / 'original-jit-code.tar.gz') as archive:
    archive.extractall(root / 'original', filter='data')
original = Path('benchmarks/results/2026-11-01-scalar-dispatch-attribution/raw-capture.tar.gz')
results = []
with tarfile.open(original) as archive:
    for label, pid in [('integer-loop', 2951179), ('csv-control', 2951198)]:
        record = json.load(archive.extractfile(f'perf/{label}/record.json'))
        assert record['identity']['processId'] == pid
        raw = (root / 'original' / f'jit-{pid}.dump').read_bytes()
        header = struct.unpack_from('<IIIIIIQQ', raw)
        assert header[0] == 0x4A695444 and header[1] == 1 and header[3] == 62 and header[5] == pid
        offset = header[2]
        counts = collections.Counter()
        loads, debug = [], {}
        while offset < len(raw):
            kind, size, stamp = struct.unpack_from('<IIQ', raw, offset)
            assert size >= 16 and offset + size <= len(raw)
            counts[kind] += 1
            payload = offset + 16
            if kind == 0:
                process, tid, vma, address, length, index = struct.unpack_from('<IIQQQQ', raw, payload)
                assert process == pid
                name_start = payload + 40
                name_end = raw.index(0, name_start, offset + size)
                name = raw[name_start:name_end].decode('utf-8')
                code = raw[name_end + 1:offset + size]
                # CLR stub-region declarations can omit their native payload.
                # Preserve those names/ranges without pretending bytes exist.
                assert len(code) == length or not code and name.startswith('stub ')
                loads.append(dict(address=address, size=length, index=index, name=name,
                                  code=code, timestamp=stamp))
            elif kind == 1:
                raise ValueError('Moved code requires time-aware mapping; stop this assessment.')
            elif kind == 2:
                address, length = struct.unpack_from('<QQ', raw, payload)
                cursor = payload + 16
                entries = []
                for _ in range(length):
                    position, line, discriminator = struct.unpack_from('<QII', raw, cursor)
                    cursor += 16
                    end = raw.index(0, cursor, offset + size)
                    entries.append(dict(address=position, line=line, discriminator=discriminator,
                                        file=raw[cursor:end].decode('utf-8')))
                    cursor = end + 1
                assert cursor == offset + size
                debug[address] = entries
            offset += size
        loads.sort(key=lambda x: x['address'])
        chains = gzip.decompress(archive.extractfile(f'perf/{label}/saved-chains.txt.gz').read()).decode()
        leaf = collections.Counter()
        tid, frames = None, []
        for line in chains.splitlines() + ['']:
            head = re.fullmatch(r'\s*(\d+)/(\d+)\s*', line)
            frame = re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)', line)
            if head:
                tid, frames = head[2], []
            elif frame and tid == record['guestTid']:
                frames.append(int(frame[1], 16))
            elif not line.strip() and tid is not None:
                if tid == record['guestTid'] and frames:
                    leaf[frames[0]] += 1
                tid, frames = None, []
        method_samples, offsets = collections.Counter(), collections.defaultdict(collections.Counter)
        unmapped, ambiguous = 0, []
        for address, count in leaf.items():
            matches = [i for i, item in enumerate(loads)
                       if item['address'] <= address < item['address'] + item['size']]
            if len(matches) == 1:
                i = matches[0]
                method_samples[i] += count
                offsets[i][address - loads[i]['address']] += count
            elif matches:
                ambiguous.append(dict(address=hex(address), samples=count,
                                      candidates=[loads[i]['name'] for i in matches]))
            else:
                unmapped += count
        methods = []
        folder = root / label
        folder.mkdir(exist_ok=True)
        for i, count in method_samples.most_common():
            item = loads[i]
            if '[Lokad.Lython]' not in item['name']:
                continue
            assert len(item['code']) == item['size'], 'Missing managed method bytes'
            file = f"method-{item['index']}.bin"
            (folder / file).write_bytes(item['code'])
            methods.append(dict(name=item['name'], address=hex(item['address']), size=item['size'],
                                index=item['index'], samples=count, offsets=offsets[i].most_common(),
                                debug=debug.get(item['address'], []), binary=file,
                                codeSHA256=hashlib.sha256(item['code']).hexdigest()))
        result = dict(case=label, pid=pid, guestTid=record['guestTid'],
                      samples=sum(leaf.values()), unmappedSamples=unmapped,
                      ambiguousSamples=ambiguous,
                      recordCounts=dict(counts), sourceDebugMethods=len(debug), methods=methods)
        (folder / 'methods.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        results.append({k: v for k, v in result.items() if k != 'methods'})
        print(label, 'records', dict(counts), 'unmapped', unmapped, 'debug', len(debug))
        for item in methods[:8]:
            print(item['samples'], item['index'], item['size'], item['name'], 'source entries', len(item['debug']))
(root / 'summary.json').write_text(json.dumps(results, indent=2), encoding='utf-8')

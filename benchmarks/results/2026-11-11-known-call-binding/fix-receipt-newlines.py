from pathlib import Path
import subprocess
for n in ['2026-11-11-native-closure-sequences','2026-11-11-captured-helper-cpython-checkpoint','2026-11-11-current-native-call-costs']:
 p=Path('benchmarks/results')/n/'README.md'
 original=subprocess.run(['git','show','HEAD:'+p.as_posix()],capture_output=True,check=True).stdout
 newline=b'\r\n'if b'\r\n'in original else b'\n'
 suffix='\nSubsequent exact-`6904093e` full delivery is **GREEN** on Windows/Ubuntu, actual\n.NET 11/10, original stress and installed Release consumers. The\n[terminal receipt](delivery-ci-status.json) is cached. Original collection-time\nstatus and adverse observations remain unchanged; this qualifies the retained\nsource, not later prototypes.\n'.encode('utf-8').replace(b'\n',newline)
 p.write_bytes(original+suffix)
print('Original evidence README bytes preserved; only subsequent full-delivery paragraph appended.')

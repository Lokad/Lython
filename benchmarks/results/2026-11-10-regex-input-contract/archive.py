from pathlib import Path
import hashlib,json,shutil,tarfile

root=Path(__file__).resolve().parent
baseline=root.parent/'regex-dev67-r215'
repo=Path('D:/code/Lython')
target=repo/'benchmarks/results/2026-11-10-regex-input-contract'
target.mkdir(exist_ok=True)
for prefix,source in [('validated',baseline),('trusted',root)]:
    folder=target/prefix;folder.mkdir(exist_ok=True)
    for name in ['identity.json','qualification.json','normal-state-proof.json','proposal.json','Utf8RegexProvider.cs','CursorAdmissionTests.cs','prepare.py','qualify.py','Directory.Build.props','Directory.Packages.props','NuGet.Config']:
        shutil.copyfile(source/name,folder/name)
for name in ['fsdecode-before.json','fsdecode-after.json','initial-package-gate.json','producer-source.patch']:
    shutil.copyfile(baseline/name,target/name)
for name in ['catalog.json','prepare-measure.py','comparison-identity.json','archive.py']:
    shutil.copyfile(root/name,target/name)
for phase in ['diagnostic','confirmation']:
    folder=target/phase;folder.mkdir(exist_ok=True)
    for name in ['summary.json','measurements.json']:
        shutil.copyfile(root/phase/name,folder/name)
with tarfile.open(target/'qualification-and-diagnostics.tar.xz','w:xz') as archive:
    binary_members={}
    def add(path,arcname):
        if path.suffix=='.dll':
            digest=hashlib.sha256(path.read_bytes()).hexdigest()
            if digest in binary_members:
                member=tarfile.TarInfo(arcname);member.type=tarfile.LNKTYPE
                member.linkname=binary_members[digest];archive.addfile(member)
                return
            binary_members[digest]=arcname
        archive.add(path,arcname=arcname)
    for prefix,source in [('validated',baseline),('trusted',root)]:
        for path in source.iterdir():
            if path.is_file() and path.suffix in {'.json','.log','.py','.props','.Config','.cs','.patch'}:
                add(path,arcname=f'{prefix}/{path.name}')
        for framework in ['net11.0','net10.0']:
            out=source/f'out/Lokad.Lython.PublicApi.Tests/Release/{framework}'
            for name in ['Lokad.Lython','Lokad.Lython.Utf8Regex','Lokad.Utf8Regex','Lokad.Utf8Regex.PythonRe']:
                add(out/(name+'.dll'),arcname=f'{prefix}/loaded/{framework}/{name}.dll')
        for folder in ['locks','tests']:
            path=source/folder
            if path.exists():
                for item in path.rglob('*'):
                    if item.is_file() and item.suffix in {'.json','.trx'}:
                        add(item,arcname=f'{prefix}/{item.relative_to(source).as_posix()}')
    for folder in ['diagnostic','confirmation','tool','images']:
        for item in (root/folder).rglob('*'):
            if folder=='images' and item.suffix=='.dll' and item.name not in {'Lokad.Lython.dll','Lokad.Lython.Utf8Regex.dll','Lokad.Utf8Regex.dll','Lokad.Utf8Regex.PythonRe.dll','Lokad.Lython.Benchmarks.dll'}:
                continue
            if item.is_file() and 'obj' not in item.relative_to(root).parts and item.suffix in {'.json','.cs','.csproj','.props','.txt','.dll','.log'}:
                add(item,arcname='trusted/'+item.relative_to(root).as_posix())
with tarfile.open(target/'qualification-and-diagnostics.tar.xz') as archive:
    for member in archive.getmembers():
        if member.isfile() or member.islnk():
            blob=archive.extractfile(member).read()
            if member.isfile(): assert len(blob)==member.size
digest=hashlib.sha256((target/'qualification-and-diagnostics.tar.xz').read_bytes()).hexdigest()
(target/'archive-identity.json').write_text(json.dumps(dict(archive='qualification-and-diagnostics.tar.xz',sha256=digest,bytes=(target/'qualification-and-diagnostics.tar.xz').stat().st_size,scope='Original immutable dev67 Validate and current-contract AssumeValid qualification, exact loaded binaries, original and longer-warmup diagnostic sources/data. No producer packages republished.'),indent=2)+'\n',encoding='utf-8')
print(target,digest,flush=True)

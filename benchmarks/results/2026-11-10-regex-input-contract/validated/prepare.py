from pathlib import Path
import hashlib,json,shutil,subprocess,xml.etree.ElementTree as E,zipfile
repo=Path('D:/code/Lython');root=Path(__file__).parent;old=root.parent/'regex-dev66-r210'
assert not(root/'feed').exists()
def write(path,text):path.write_text(text,encoding='utf-8',newline='\n')
source=json.loads((root.parent/'cpython-construction-r209/b561/source-identity.json').read_text(encoding='utf-8'))
for name,digest in source['normalizedSource'].items():assert hashlib.sha256((repo/name).read_bytes().replace(b'\r\n',b'\n')).hexdigest()==digest,name
for name in ['Directory.Build.props','Directory.Packages.props','NuGet.Config','Utf8RegexProvider.cs','CursorAdmissionTests.cs','workflow-fixture-manifest.json','qualify.py']:
    text=(old/name).read_text(encoding='utf-8').replace('regex-dev66-r210','regex-dev67-r215').replace('0.5.0-dev.66','0.5.0-dev.67')
    write(root/name,text)
before=json.loads((old/'normal-state-before.json').read_text(encoding='utf-8'))
write(root/'normal-state-before.json',json.dumps({name:hashlib.sha256((repo/name).read_bytes()).hexdigest()for name in before},indent=2)+'\n')
(root/'feed').mkdir();packages={}
expected=json.loads((root/'initial-package-gate.json').read_text(encoding='utf-8'))
for name in ['Lokad.Utf8Regex','Lokad.Utf8Regex.PythonRe']:
    filename=f'{name}.0.5.0-dev.67.nupkg';origin=Path('D:/code/utf8regex/artifacts/nuget')/filename
    assert hashlib.sha256(origin.read_bytes()).hexdigest()==expected[name]['newSHA256']
    shutil.copyfile(origin,root/'feed'/filename)
    with zipfile.ZipFile(origin)as archive:
        metadata=E.fromstring(archive.read(next(n for n in archive.namelist()if n.endswith('.nuspec'))))
        commit=next(e.attrib['commit']for e in metadata.iter()if e.tag.endswith('repository'));assert commit=='d8eb14a52b2c044f4345af03469356992be58861'
        dependencies=[e.attrib for e in metadata.iter()if e.tag.endswith('dependency')]
    packages[filename]=dict(sha256=expected[name]['newSHA256'],repositoryCommit=commit,dependencies=dependencies)
identity=dict(consumerRevision='5be440912cf408e920d63435ec392a7b1af24e69',consumerRuntime='All568 production sources equal fully qualified e543bf15, cachedGREEN42898279287; R214 semantic/retained-owner test additions included.',producerVersion='0.5.0-dev.67',producerSourceHead='Immutable package d8eb14a5; never substitute moving sibling HEAD',packages=packages,bridge='Exact unchanged dev66/development source and cursor-admission controls, Validate default; isolated feed/assets/locks/outputs/cache.',providerSourceSHA256=hashlib.sha256((root/'Utf8RegexProvider.cs').read_bytes()).hexdigest(),cursorTestSourceSHA256=hashlib.sha256((root/'CursorAdmissionTests.cs').read_bytes()).hexdigest(),limitations='Development contract/semantic/cancellation/ownership checks only, no release pin promotion, whole-backend performance or safety certification.')
write(root/'identity.json',json.dumps(identity,indent=2)+'\n')
patch=subprocess.check_output(['git','diff','b478f853f722b7c34de83bdcd14693ee594ca053','d8eb14a52b2c044f4345af03469356992be58861','--','src'],cwd='D:/code/utf8regex')
(root/'producer-source.patch').write_bytes(patch)
write(root/'proposal.json',json.dumps(dict(scope='One justified unchanged-bridge development qualification of materially changed immutable dev67, both actualtargets. Normal pin/source/output identities must remain unchanged.',source='Matching validation, necessary-start screens, nullable preparation and pooled working storage differ; documented-member XML equality alone is not API completeness.',inputValidation='Keep Validate until a separate Lython contract/UTF-8 lifetime proof; do not silently enable AssumeValid.',limits='Three final capabilities remain separately assessed; producer source safety/actual backend performance not certified by focused tests. No sibling edit/build, republishing or new core dependency.'),indent=2)+'\n')
print('Prepared immutable dev67 qualification; normal source/pin unchanged.')

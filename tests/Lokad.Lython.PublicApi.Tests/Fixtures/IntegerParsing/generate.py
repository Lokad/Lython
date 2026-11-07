"""Freeze trusted Python int() grammar/boundaries with CPython 3.13, not Lython."""
from contextlib import redirect_stdout
from pathlib import Path
import io,json,sys,unicodedata

assert sys.implementation.name=='cpython' and sys.version_info[:2]==(3,13)
rows=[]
def add(name,value,base):
    source=f"try:\n print(int({value!r},{base}))\nexcept ValueError as error:\n print(type(error).__name__,str(error))\n"
    output=io.StringIO()
    with redirect_stdout(output):exec(compile(source,'<frozen integer case>','exec'),{})
    rows.append(dict(name=name,source=source,output=output.getvalue()))
def digits(value,base):
    result=''
    while value:value,digit=divmod(value,base);result='0123456789abcdefghijklmnopqrstuvwxyz'[digit]+result
    return result or '0'

for base in range(2,37):
    for label,value in [('uint64',(1<<64)-1),('promoted',1<<64),
                        ('int128-high',1<<127),('uint128',(1<<128)-1),
                        ('promoted128',1<<128),('negative128',-(1<<128)),
                        ('chunks',-((1<<129)+57))]:
        text=('-' if value<0 else '')+digits(abs(value),base)
        add(f'base{base}-{label}',text,base)
spaces=['\t','\n','\v','\f','\r',' ','\x1c','\x1d','\x1e','\x1f','\x85','\xa0','\u1680',
        *map(chr,range(0x2000,0x200b)),'\u2028','\u2029','\u202f','\u205f','\u3000']
for index,space in enumerate(spaces):
    text=space+'+1_234'+space
    add(f'space{index}-text',text,10);add(f'space{index}-bytes',text.encode('utf-8'),10)
for index,(text,base) in enumerate([
    ('٠b_١٠١',0),('٠b1',16),('０x_١_٢',0),('٠_01',0),('０_０',0),('١٢',2),
    ('＋１２',10),('²',10),('Ⅻ',10),('⑫',10),('፩',10),('0ｘ1',0),('𝑥',36),
    ('0b1',12),('0x1',36),('0_00',0),('0_1',0),('0x_',0),('0x__1',0),
    ('_1',10),('1__0',10),('1_0_',10),('+',10),('',0),('7\0',10),
    (' \t-0x_'+('_'.join(['ffff']*32))+'\r\n',0),
]):add(f'grammar{index}',text,base)
Path(__file__).with_name('cases.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print(f'Frozen {len(rows)} cases; {sys.version.split()[0]}, Unicode {unicodedata.unidata_version}')

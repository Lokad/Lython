using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeReturnFunctionTests
{
    // Goldens independently captured with isolated CPython 3.13.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "negated-branches", "events=[]\ndef double(value):\n    if not not value: return 'full'\n    return 'empty'\ndef triple(value):\n    if not not not value: return 'empty'\n    return 'full'\nclass Flag:\n    def __bool__(self):\n        events.append('bool')\n        return True\nfor value in (None,False,0,1,'','x',[],[1]):\n    print(double(value),triple(value))\nfor operation in (double,triple):\n    print(operation(Flag()),events)\n    events.clear()\n", "empty empty\nempty empty\nempty empty\nfull full\nempty empty\nfull full\nempty empty\nfull full\nfull ['bool']\nfull ['bool']\n" },
        { "unary-values", "def state(value):\n    if not value:\n        return 'empty'\n    return 'full'\ndef positive(value): return +value\ndef negative(value): return -value\ndef invert(value): return ~value\nprint([state(value) for value in (None, False, 0, 0.0, '', b'', [], (), {}, set(), range(0), True, 3, 'x')])\nfor value in (False, True, -3, 0, 1, 1 << 80):\n    print(positive(value) is value, positive(value), negative(value), invert(value))\nfor value in (-0.0, 2.5):\n    print(positive(value) is value, positive(value), negative(value))\nfor operation in (positive, negative, invert):\n    try: operation('text')\n    except TypeError: print('bad')\n", "['empty', 'empty', 'empty', 'empty', 'empty', 'empty', 'empty', 'empty', 'empty', 'empty', 'empty', 'full', 'full', 'full']\nFalse 0 0 -1\nFalse 1 -1 -2\nTrue -3 3 2\nTrue 0 0 -1\nTrue 1 -1 -2\nTrue 1208925819614629174706176 -1208925819614629174706176 -1208925819614629174706177\nTrue -0.0 0.0\nTrue 2.5 -2.5\nbad\nbad\nbad\n" },
        { "unary-protocols", "events=[]\nclass Value:\n    def __bool__(self):\n        events.append('bool')\n        return False\n    def __pos__(self):\n        events.append('plus')\n        return 11\n    def __neg__(self):\n        events.append('minus')\n        return 12\n    def __invert__(self):\n        events.append('invert')\n        return 13\ndef state(value):\n    if not value: return 'empty'\n    return value.upper()\ndef positive(value): return +value\ndef negative(value): return -value\ndef invert(value): return ~value\ndef wanted(value, *, prefix='N\u00e9'):\n    if not value: return False\n    return value.startswith(prefix)\nvalue=Value()\nprint(state(value),positive(value),negative(value),invert(value),events)\nprint(wanted('',prefix=value),wanted('N\u00e9x'),wanted('no'))\n", "empty 11 12 13 ['bool', 'plus', 'minus', 'invert']\nFalse True False\n" },
        { "captured-bound-method", "def make():\n count=0\n def method(self,value):\n  nonlocal count\n  count+=1\n  return value.upper()\n def read():return count\n return method,read\nmethod,read=make()\nclass Box:pass\nBox.run=method\nbox=Box()\nsaved=box.run\nprint(box.run('a'),read(),saved(value='b'),read())\n", "A 1 B 2\n" },
        { "captured-sequence-text", "def make():\n count=0\n def words(value=' A B '):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n def read():return count\n return words,read\nwords,read=make()\nprint(words(),read())\nprint(words(value=' C '),read())\nprint(list(map(words,['D E','F'])),read())\n", "['a', 'b'] 1\n['c'] 2\n[['d', 'e'], ['f']] 4\n" },
        { "captured-sequence-protocol-once", "def make():\n count=0\n def words(value):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n def read():return count\n return words,read\nwords,read=make()\nclass Text:\n def lower(self):\n  print('hook',read())\n  return 'X Y'\nprint(words('A'),read())\nprint(words(Text()),read())\n", "['a'] 1\nhook 2\n['X', 'Y'] 2\n" },
        { "captured-live-read-delete", "def make():\n value=[]\n def read():return value\n def write(new):\n  nonlocal value\n  value=new\n def erase():\n  nonlocal value\n  del value\n return read,write,erase\nread,write,erase=make()\na=read()\na.append('first')\nwrite(['second'])\nprint(a,read())\nerase()\ntry:read()\nexcept NameError:print('missing')\nwrite(a)\nprint(read() is a,read())\n", "['first'] ['second']\nmissing\nTrue ['first']\n" },
        { "captured-sequence-error-context", "def make():\n count=0\n def split(value):\n  nonlocal count\n  count+=1\n  return value.split('')\n def read():return count\n return split,read\nsplit,read=make()\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:split('abc')\n except ValueError as error:print(read(),error.__context__ is outer)\n", "1 True\n" },
        { "captured-sequence-later-read", "def make():\n value=3\n def update():\n  nonlocal value\n  value+=2\n  return value\n return update\nupdate=make()\nprint(update(),update())\n", "5 7\n" },
        { "captured-sequence-promotion", "def make():\n count=(1<<63)-1\n def words(value):\n  nonlocal count\n  count+=1\n  return value.upper()\n def read():return count\n return words,read\nwords,read=make()\nprint(words('é'),read())\nprint(words(value='ß'),read())\n", "É 9223372036854775808\nSS 9223372036854775809\n" },
        { "incoming-write-live-owner", "def outer():\n value=1\n def write(x):\n  nonlocal value\n  value=x\n  return value\n print(value,write(7),value)\n value=11\n print(write(13),value)\n return write\nwrite=outer()\nprint(write(17))\n", "1 7 7\n13 13\n17\n" },
        { "incoming-write-owner-locals", "def outer():\n value=[]\n def write(x):\n  nonlocal value\n  value=x\n  return locals()['value']\n saved=[]\n print(write(saved) is saved,value is saved,locals()['value'] is saved)\n return write\nwrite=outer()\nother=[]\nprint(write(other) is other)\n", "True True True\nTrue\n" },
        { "incoming-write-after-owner-deletion", "def outer():\n value=1\n def write(x):\n  nonlocal value\n  value=x\n def read():return value\n def erase():\n  nonlocal value\n  del value\n del value\n try:read()\n except NameError:print('missing')\n write(7)\n print(read(),read())\n erase()\n return write,read\nwrite,read=outer()\nwrite(19)\nprint(read())\n", "missing\n7 7\n19\n" },
        { "incoming-write-transitive-shadow", "value=100\ndef outer():\n value=1\n def middle():\n  nonlocal value\n  def write(x):\n   nonlocal value\n   value=x\n   return value\n  return write\n write=middle()\n print(write(7),value)\n return write\nprint(outer()(11),value)\n", "7 7\n11 100\n" },
        { "incoming-write-comprehension-binding", "def outer():\n value=0\n def write(items):\n  nonlocal value\n  result=[(value:=item) for item in items]\n  return result,value\n print(write([3,8]),value)\n return write\nprint(outer()([5,9]))\n", "([3, 8], 8) 8\n([5, 9], 9)\n" },
        { "incoming-write-class-cell", "class C:\n def write(self,new):\n  nonlocal __class__\n  __class__=new\n  return __class__\n def read(self):return __class__\ninstance=C()\nprint(instance.write(int).__name__,instance.read().__name__)\n", "int int\n" },
        { "incoming-write-class-body-owner", "def outer():\n value=1\n def write(x):\n  nonlocal value\n  value=x\n write(7)\n class C:\n  result=value\n print(value,C.result)\n return write,C\nwrite,C=outer()\nprint(write(11),C.result)\n", "7 7\nNone 7\n" },
        { "incoming-write-default-and-exception", "events=[]\ndef outer():\n value=1\n def write(x=events.append('default')):\n  nonlocal value\n  value=3\n  raise ValueError(value)\n try:write()\n except ValueError as error:print(value,error.args,events)\n return write\nwrite=outer()\ntry:write(9)\nexcept ValueError as error:print(error.args,events)\n", "3 (3,) ['default']\n(3,) ['default']\n" },
        { "record-locals-and-retained-aliases", """
            def parse(row):
                raw = row['amount']
                amount = int(raw.strip())
                return amount, raw, raw
            row = {'amount': ' 19 '}
            amount, first, second = parse(row)
            print(amount, first is second, second is row['amount'])
            """, "19 True True\n" },
        { "record-converter-default-and-rebinding", """
            def parse(row, convert=int):
                raw = row['amount']
                amount = convert(raw)
                saved = raw
                return amount, saved, raw
            row = {'amount': '23'}
            int = lambda value: 42
            amount, first, second = parse(row)
            print(amount, first is second, second is row['amount'])
            print(parse(row, lambda value: 9)[0])
            """, "23 True True\n9\n" },
        { "captured-read-before-and-after-binding", "def outer():\n def read():return value+1\n try:read()\n except NameError as error:print(type(error).__name__)\n value=3\n print(read())\n del value\n try:read()\n except NameError as error:print(type(error).__name__)\n return read\nread=outer()\ntry:read()\nexcept NameError as error:print(type(error).__name__)\n", "NameError\n4\nNameError\nNameError\n" },
        { "captured-callable-rebinding", "def outer():\n operation=int\n def convert(text):return operation(text)\n print(convert('7'))\n operation=lambda value:'changed'\n print(convert('8'))\n return convert\nprint(outer()('9'))\n", "7\nchanged\nchanged\n" },
        { "captured-read-then-local-introspection", "def outer():\n offset=5\n def work(value):\n  value+=offset\n  return value,locals()['offset'],locals()['value']\n return work\nprint(outer()(3))\n", "(8, 5, 8)\n" },
        { "captured-write-and-retained-reader", "def outer():\n total=1\n def bump(value):\n  nonlocal total\n  total+=value\n  return total\n def read():return total\n print(bump(2),read(),bump(4),read())\n return bump,read\nbump,read=outer()\nprint(bump(8),read())\n", "3 3 7 7\n15 15\n" },
        { "closed-nested-global-and-shadow", "offset=7\ndef outer():\n offset=99\n def convert(value):return int(value)+global_offset\n return convert\nglobal_offset=3\nconvert=outer()\nprint(convert('7'))\nglobal_offset=8\nprint(convert('7'),convert.__qualname__)\n", "10\n15 outer.<locals>.convert\n" },
        { "closed-nested-locals-promotion", "value=100\ndef outer():\n value=99\n def work(value):\n  value+=3\n  if value<0:return None\n  return value,locals()['value'],globals()['value']\n return work\nprint(outer()(4))\n", "(7, 7, 100)\n" },
        { "real-capture-live-cell", "def outer():\n offset=3\n def add(value):return value+offset\n print(add(2))\n offset=9\n return add\nprint(outer()(2))\n", "5\n11\n" },
        { "closed-nested-unbound", "def outer():\n value=100\n def work(flag):\n  if flag:value=3\n  return value+1\n return work\nf=outer()\nprint(f(True))\ntry:f(False)\nexcept UnboundLocalError:print('unbound')\n", "4\nunbound\n" },
        { "closed-nested-class-control", "def outer():\n class Base:\n  def value(self):return 3\n class Child(Base):\n  def value(self):return super().value()+4\n return Child\nprint(outer()().value())\n", "7\n" },
        { "primitive-numeric-mixed-return-shapes", "def numeric0(a,b):return a + b\ndef numeric1(a,b):return a - b\ndef numeric2(a,b):return a * b\ndef numeric3(a,b):return a // b\ndef numeric4(a,b):return a % b\ndef numeric5(a,b):return a | b\ndef numeric6(a,b):return a ^ b\ndef numeric7(a,b):return a & b\ndef numeric8(a,b):return a << b\ndef numeric9(a,b):return a >> b\ndef numeric10(a,b):return a / b\ndef numeric11(a,b):return a == b\ndef numeric12(a,b):return a != b\ndef numeric13(a,b):return a < b\ndef numeric14(a,b):return a <= b\ndef numeric15(a,b):return a > b\ndef numeric16(a,b):return a >= b\nprint(numeric0(1.5,2),numeric1(1.5,2),numeric2(1.5,2),numeric3(1.5,2),numeric4(1.5,2),numeric10(1.5,2),numeric11(1.5,2),numeric12(1.5,2),numeric13(1.5,2),numeric14(1.5,2),numeric15(1.5,2),numeric16(1.5,2))\nprint(numeric0(-7.5,2.5),numeric1(-7.5,2.5),numeric2(-7.5,2.5),numeric3(-7.5,2.5),numeric4(-7.5,2.5),numeric10(-7.5,2.5),numeric11(-7.5,2.5),numeric12(-7.5,2.5),numeric13(-7.5,2.5),numeric14(-7.5,2.5),numeric15(-7.5,2.5),numeric16(-7.5,2.5))\nprint(numeric0(0.0,-3.0),numeric1(0.0,-3.0),numeric2(0.0,-3.0),numeric3(0.0,-3.0),numeric4(0.0,-3.0),numeric10(0.0,-3.0),numeric11(0.0,-3.0),numeric12(0.0,-3.0),numeric13(0.0,-3.0),numeric14(0.0,-3.0),numeric15(0.0,-3.0),numeric16(0.0,-3.0))\nprint(numeric0(True,True),numeric1(True,True),numeric2(True,True),numeric3(True,True),numeric4(True,True),numeric5(True,True),numeric6(True,True),numeric7(True,True),numeric8(True,True),numeric9(True,True),numeric10(True,True),numeric11(True,True),numeric12(True,True),numeric13(True,True),numeric14(True,True),numeric15(True,True),numeric16(True,True))\nprint(numeric0(False,True),numeric1(False,True),numeric2(False,True),numeric3(False,True),numeric4(False,True),numeric5(False,True),numeric6(False,True),numeric7(False,True),numeric8(False,True),numeric9(False,True),numeric10(False,True),numeric11(False,True),numeric12(False,True),numeric13(False,True),numeric14(False,True),numeric15(False,True),numeric16(False,True))\nprint(numeric0(True,3),numeric1(True,3),numeric2(True,3),numeric3(True,3),numeric4(True,3),numeric5(True,3),numeric6(True,3),numeric7(True,3),numeric8(True,3),numeric9(True,3),numeric10(True,3),numeric11(True,3),numeric12(True,3),numeric13(True,3),numeric14(True,3),numeric15(True,3),numeric16(True,3))\nprint(numeric0(2**70+7,1.25),numeric1(2**70+7,1.25),numeric2(2**70+7,1.25),numeric3(2**70+7,1.25),numeric4(2**70+7,1.25),numeric10(2**70+7,1.25),numeric11(2**70+7,1.25),numeric12(2**70+7,1.25),numeric13(2**70+7,1.25),numeric14(2**70+7,1.25),numeric15(2**70+7,1.25),numeric16(2**70+7,1.25))\ndef bits(a,b):return (a&b)^(a|b)\nfor a,b in [(True,True),(False,True),(True,3)]:\n value=bits(a,b)\n print(value,type(value).__name__)\ndef amount(quantity,cents,discount=10):return quantity*cents*(100-discount)//100\nprint(amount(1.5,199),amount(True,199),amount(-3.5,199,15))\n", "3.5 -0.5 3.0 0.0 1.5 0.75 False True True True False False\n-5.0 -10.0 -18.75 -3.0 0.0 -3.0 False True True True False False\n-3.0 3.0 -0.0 -0.0 -0.0 -0.0 False True False False True True\n2 0 1 1 0 True False True 2 0 1.0 True False False True False True\n1 -1 0 0 0 True True False 0 0 0.0 False True True True False False\n4 -2 3 0 1 3 2 1 8 0 0.3333333333333333 False True True True False False\n1.1805916207174113e+21 1.1805916207174113e+21 1.475739525896764e+21 9.444732965739291e+20 0.25 9.444732965739291e+20 False True False False True True\nFalse bool\nTrue bool\n2 int\n268.0 179 -593.0\n" },
        { "primitive-binary-protocols-and-declined-operators", "from decimal import Decimal\ndef multiply(value):return value*3\nclass Left:\n def __mul__(self,other):\n  print('left once')\n  return other+7\nclass Right:\n def __rmul__(self,other):\n  print('right once')\n  return other+8\ndef reflected(value):return 3*value\nprint(multiply(Left()),reflected(Right()))\nprint(multiply(1+2j),multiply(Decimal('1.25')))\ndef power(a,b):return a**b\ndef identity(a,b):return a is b\ndef contains(a,b):return a in b\nitems=[]\nprint(power(2,-3),identity(items,items),contains(2,[1,2]))\n", "left once\nright once\n10 11\n(3+6j) 3.75\n0.125 True True\n" },
        { "integer-binary-family-and-dynamic-mixed", "def op0(a,b):return a + b\ndef op1(a,b):return a - b\ndef op2(a,b):return a * b\ndef op3(a,b):return a // b\ndef op4(a,b):return a % b\ndef op5(a,b):return a | b\ndef op6(a,b):return a ^ b\ndef op7(a,b):return a & b\ndef op8(a,b):return a << b\ndef op9(a,b):return a >> b\ndef op10(a,b):return a / b\ndef op11(a,b):return a == b\ndef op12(a,b):return a != b\ndef op13(a,b):return a < b\ndef op14(a,b):return a <= b\ndef op15(a,b):return a > b\ndef op16(a,b):return a >= b\nprint(op0(19,3),op1(19,3),op2(19,3),op3(19,3),op4(19,3),op5(19,3),op6(19,3),op7(19,3),op8(19,3),op9(19,3),op10(19,3),op11(19,3),op12(19,3),op13(19,3),op14(19,3),op15(19,3),op16(19,3))\nprint(op0(-19,3),op1(-19,3),op2(-19,3),op3(-19,3),op4(-19,3),op5(-19,3),op6(-19,3),op7(-19,3),op8(-19,3),op9(-19,3),op10(-19,3),op11(-19,3),op12(-19,3),op13(-19,3),op14(-19,3),op15(-19,3),op16(-19,3))\nprint(op0(19,-3),op1(19,-3),op2(19,-3),op3(19,-3),op4(19,-3),op5(19,-3),op6(19,-3),op7(19,-3),op10(19,-3),op11(19,-3),op12(19,-3),op13(19,-3),op14(19,-3),op15(19,-3),op16(19,-3))\nprint(op0(1237940039285380274899124231,3),op1(1237940039285380274899124231,3),op2(1237940039285380274899124231,3),op3(1237940039285380274899124231,3),op4(1237940039285380274899124231,3),op5(1237940039285380274899124231,3),op6(1237940039285380274899124231,3),op7(1237940039285380274899124231,3),op8(1237940039285380274899124231,3),op9(1237940039285380274899124231,3),op10(1237940039285380274899124231,3),op11(1237940039285380274899124231,3),op12(1237940039285380274899124231,3),op13(1237940039285380274899124231,3),op14(1237940039285380274899124231,3),op15(1237940039285380274899124231,3),op16(1237940039285380274899124231,3))\nprint(op2(True,3),op2(1.5,3),op0(\"a\",\"b\"),op2(\"x\",3))\n", "22 16 57 6 1 19 16 3 152 2 6.333333333333333 False True False False True True\n-16 -22 -57 -7 2 -17 -18 1 -152 -3 -6.333333333333333 False True True True False False\n16 22 -57 -7 -2 -1 -18 17 -6.333333333333333 False True False False True True\n1237940039285380274899124234 1237940039285380274899124228 3713820117856140824697372693 412646679761793424966374743 2 1237940039285380274899124231 1237940039285380274899124228 3 9903520314283042199192993848 154742504910672534362390528 4.126466797617934e+26 False True False False True True\n3 4.5 ab xxx\n" },
        { "integer-accounting-forwarding-and-order", "def amount(quantity,cents,discount=10):\n value=quantity*cents\n return value*(100-discount)//100\ndef mark(name,value):\n print(name)\n return value\nprint(amount(mark('quantity',3),mark('cents',199),discount=mark('discount',15)))\nprint(amount(-3,199),amount(2**80,7,0))\ndef bounded(a,b):\n if a*b<100:return a-b\n return a//b\nprint(bounded(2,3),bounded(100,3))\n", "quantity\ncents\ndiscount\n507\n-538 8462480737302404222943232\n-1 33\n" },
        { "integer-binary-error-context", "def bad0(value):return value // 0\ndef bad1(value):return value % 0\ndef bad2(value):return value / 0\ndef bad3(value):return value << -1\ntry:raise RuntimeError(\"outer\")\nexcept RuntimeError as outer:\n try:bad0(7)\n except (ZeroDivisionError,ValueError) as error:print(type(error).__name__,error.__context__ is outer)\n try:bad1(7)\n except (ZeroDivisionError,ValueError) as error:print(type(error).__name__,error.__context__ is outer)\n try:bad2(7)\n except (ZeroDivisionError,ValueError) as error:print(type(error).__name__,error.__context__ is outer)\n try:bad3(7)\n except (ZeroDivisionError,ValueError) as error:print(type(error).__name__,error.__context__ is outer)\n", "ZeroDivisionError True\nZeroDivisionError True\nZeroDivisionError True\nValueError True\n" },
        { "fused-siblings-and-forwarded-local", "def words(value):\n prepared=value.lower()\n return prepared.split()\ndef combined(left,right,size=len):\n return size(left.upper().split())+size(right.lower().split())\nprint(words('  İΣ ΣΙΣ ß\\u2003'))\nprint(combined(' ß ΣΙΣ ', ' İΣ '))\nprint(words(''),combined('', ''))\n", "['i̇ς', 'σις', 'ß']\n3\n[] 0\n" },
        { "canonical-closed-compositions", "def folded(value):return value.casefold().rsplit()\ndef capitalized(value):return value.capitalize().splitlines()\ndef titled(value):return value.title().lstrip().rstrip()\ndef swapped(value):return value.swapcase().expandtabs()\ndef encoded(value):return len(value.encode())\nprint(folded(' Aİß Σ C\\n'))\nprint(capitalized('élAN\\nΣΙΣ'))\nprint(titled('  élAN σΙΣ  '))\nprint(swapped('ß\\tÉ😀'))\nprint(encoded('A😀é'))\nprint(folded(''),capitalized(''),encoded(''))\n", "['ai̇ss', 'σ', 'c']\n['Élan', 'σις']\nÉlan Σις\nSS      é😀\n7\n[] [] 0\n" },
        { "canonical-closed-dynamic-receiver", "def folded(value):return value.casefold().rsplit()\nclass Subject:\n def casefold(self):\n  print('casefold once')\n  return self\n def rsplit(self):\n  print('rsplit once')\n  return ['custom']\nprint(folded(Subject()))\n", "casefold once\nrsplit once\n['custom']\n" },
        { "all-native-string-methods", "def helper_strip(value):return value.strip()\ndef helper_lower(value):return value.lower()\ndef helper_upper(value):return value.upper()\ndef helper_islower(value):return value.islower()\ndef helper_isupper(value):return value.isupper()\ndef helper_istitle(value):return value.istitle()\ndef helper_isalpha(value):return value.isalpha()\ndef helper_isdigit(value):return value.isdigit()\ndef helper_isalnum(value):return value.isalnum()\ndef helper_isascii(value):return value.isascii()\ndef helper_isdecimal(value):return value.isdecimal()\ndef helper_isidentifier(value):return value.isidentifier()\ndef helper_isnumeric(value):return value.isnumeric()\ndef helper_isprintable(value):return value.isprintable()\ndef helper_isspace(value):return value.isspace()\nfor value in [' aÉ2 ', 'é', '²', '\\x1c', 'ÉLAN', 'Élan']:\n print([helper_strip(value),helper_lower(value),helper_upper(value),helper_islower(value),helper_isupper(value),helper_istitle(value),helper_isalpha(value),helper_isdigit(value),helper_isalnum(value),helper_isascii(value),helper_isdecimal(value),helper_isidentifier(value),helper_isnumeric(value),helper_isprintable(value),helper_isspace(value)])\n", "['aÉ2', ' aé2 ', ' AÉ2 ', False, False, False, False, False, False, False, False, False, False, True, False]\n['é', 'é', 'É', True, False, False, True, False, True, False, False, True, False, True, False]\n['²', '²', '²', False, False, False, False, True, True, False, False, False, True, True, False]\n['', '\\x1c', '\\x1c', False, False, False, False, False, False, True, False, False, False, False, True]\n['ÉLAN', 'élan', 'ÉLAN', False, True, False, True, False, True, False, False, True, False, True, False]\n['Élan', 'élan', 'ÉLAN', False, False, True, True, False, True, False, False, True, False, True, False]\n" },
        { "native-conversions", "def integer(v):return int(v)\ndef floating(v):return float(v)\ndef text(v):return str(v)\ndef length(v):return len(v)\ndef absolute(v):return abs(v)\nprint(integer(' 42 '), integer(b'-3'), floating('1.25'))\nprint(text(123), length({'a':1}), absolute(-7))\n", "42 -3 1.25\n123 1 7\n" },
        { "dictionary-alias", "def pick(row):return row['value']\nitems=[]\nrow={'value':items}\npicked=pick(row)\npicked.append('ok')\nprint(picked is items, row['value'])\n", "True ['ok']\n" },
        { "key-error-context", "def pick(row):return row['value']\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:pick({})\n except KeyError as error:print(error.args, error.__context__ is outer)\n", "('value',) True\n" },
        { "conversion-error-context", "def convert(v):return int(v)\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:convert('bad')\n except ValueError as error:print(type(error).__name__, error.__context__ is outer)\n", "ValueError True\n" },
        { "builtin-rebinding", "def convert(v):return int(v)\noriginal=int\nint=lambda value:value+'!'\nprint(convert('4'))\nint=original\nprint(convert('4'))\n", "4!\n4\n" },
        { "local-callable", "def apply(operation,value):return operation(value)\nprint(apply(int,'7'))\nprint(apply(lambda value:value*2,7))\n", "7\n14\n" },
        { "nested-capture", "def outer():\n int=lambda value:value+'?'\n def convert(value):return int(value)\n return convert\nprint(outer()('5'))\n", "5?\n" },
        { "integer-protocol", "def convert(value):return int(value)\nclass Number:\n def __int__(self):\n  print('hook')\n  return 7\nprint(convert(Number()))\n", "hook\n7\n" },
        { "mapping-protocol", "def pick(row):return row['value']\nclass Row:\n def __getitem__(self,key):\n  print(key)\n  return 'custom'\nprint(pick(Row()))\n", "value\ncustom\n" },
        { "protocol-dictionary-key", "def pick(row):return row['value']\nclass Key:\n def __hash__(self):return hash('value')\n def __eq__(self,other):\n  print('equal')\n  return other=='value'\nrow={Key():9}\nprint(pick(row))\n", "equal\n9\n" },
        { "class-native-body", "class Convert:\n def number(self,value):return int(value)\nprint(Convert().number('12'))\n", "12\n" },
        { "explicit-global", "def outer():\n def convert(value):\n  global int\n  return int(value)\n return convert\nprint(outer()('23'))\n", "23\n" },
        { "compound-text", "def words(value):return value.lower().split()\ndef normalize(value):return value.strip().upper().replace('A','z')\ndef count(value):return len(value.lower().split(None,1))\ndef wanted(value):return value.strip().startswith('hello')\nprint(words(' A B '),normalize(' ab '),count('A B C'),wanted(' hello'))\n", "['a', 'b'] zB 2 True\n" },
        { "compound-numeric", "parse=int\ndef number(value):return abs(parse(value.strip())+3)\ndef text(value):return str(abs(value)+2).startswith('4')\nprint(number(' -8 '),text(-2))\n", "5 True\n" },
        { "compound-rebinding", "def count(value):return len(value.lower().split())\noriginal=len\nlen=lambda value:99\nprint(count('A B'))\nlen=original\nprint(count('A B'))\n", "99\n2\n" },
        { "compound-protocol-once", "def words(value):return value.lower().split()\nclass Text:\n def lower(self):\n  print('hook')\n  return 'A B'\nprint(words(Text()))\n", "hook\n['A', 'B']\n" },
        { "compound-error-order", "def number(value):return int(value.strip())+missing\ntry:number('bad')\nexcept ValueError:print('conversion first')\n", "conversion first\n" },
        { "unknown-mapping-result", "def count(row):return len(row['value'])\nclass Value:\n def __len__(self):\n  print('length hook')\n  return 3\nprint(count({'value':Value()}))\n", "length hook\n3\n" },
        { "compound-exception-context", "def number(value):return int(value.strip())+2\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:number(' bad ')\n except ValueError as error:print(type(error).__name__,error.__context__ is outer)\n", "ValueError True\n" },
        { "shared-unary-aliases", "native=sum\ndef checksum(value):return native(value)\ndef apply(operation,value):return operation(value)\nprint(checksum(b'\\x01\\xff'),apply(abs,True),apply(float,b'1.5'),apply(str,42))\nnative=lambda value:99\nprint(checksum(b''))\nnative=sum\nprint(checksum([2,3]))\n", "256 1 1.5 42\n99\n5\n" },
        { "length-proof-boundary", "from collections import defaultdict,deque,ChainMap\ndef length(value):return len(value)\nprint(length('abc'),length(b'ab'),length([1]),length((1,2)),length({'a':1}),length({1,2}),length(range(7)),length(deque([1,2,3])))\nprint(length(defaultdict(int,{'x':1})),length(ChainMap({'x':1},{'y':2})))\nfor value in [None,3,iter([1])]:\n try:length(value)\n except TypeError:print('type')\n", "3 2 1 2 1 2 7 3\n1 2\ntype\ntype\ntype\n" },
        { "native-defaults-and-call-forms", "def clean(value=' A '):return value.strip()\ndef nothing():pass\nprint(clean(),clean(value=' B '),clean(*[' C ']),clean(**{'value':' D '}),nothing())\nclean.__defaults__=(' E ',)\nprint(clean(),list(map(clean,[' F ',' G '])))\n", "A B C D None\nE ['F', 'G']\n" },
        { "native-live-module-slots", "def add(value):return value+offset\noffset=3\nprint(add(2))\noffset=7\nprint(add(2))\ndef outer():\n offset=99\n return add(2)\nprint(outer())\n", "5\n9\n9\n" },
        { "native-defining-builtins", "def convert(value):return int(value)\ndef invoke():\n int=lambda value:99\n return convert('7')\nprint(invoke())\n", "7\n" },
        { "native-local-callable-default", "def convert(value,operation=int):return operation(value)\nprint(convert('7'),convert('8',str))\nconvert.__defaults__=(lambda value:99,)\nprint(convert('9'))\n", "7 8\n99\n" },
        { "forwarded-local-chain", "def words(value):\n normalized=value.lower()\n result=normalized.split()\n return result\ndef number(value):\n parsed=int(value)\n return parsed\nprint(words(' A B '),number('7'))\n", "['a', 'b'] 7\n" },
        { "forwarded-parameter-read-again", "def combine(value):\n value=value.strip()\n upper=value.upper()\n return upper+value\nprint(combine(' a '))\n", "Aa\n" },
        { "forwarded-parameter-reassignment", "def clean(value):\n value=value.strip()\n value=value.upper()\n return value\nprint(clean(' a '))\n", "A\n" },
        { "forwarded-indirect-locals", "class Result:\n @property\n def split(self):return locals\nclass Source:\n def lower(self):return Result()\ndef words(value):\n normalized=value.lower()\n result=normalized.split()\n return result\nprint(sorted(words(Source())))\n", "['normalized', 'value']\n" },
        { "forwarded-live-callable", "def convert(value):\n operation=int\n return operation(value)\noriginal=int\nint=lambda value:'changed'\nprint(convert('7'))\nint=original\nprint(convert('7'))\n", "changed\n7\n" },
        { "forwarded-error-order", "def number(value):\n parsed=int(value.strip())\n return parsed+missing\ntry:number('bad')\nexcept ValueError:print('conversion first')\n", "conversion first\n" },
        { "forwarded-retained-alias", "def pick(row):\n chosen=row['value']\n return chosen\nitems=[]\nreturned=pick({'value':items})\nreturned.append('ok')\nprint(returned is items,items)\n", "True ['ok']\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeReturnsPreservePythonBehavior(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var options = new LythonRunOptions { MemoryBudget = budget };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnboundCaptureKeepsItsReadSpanAndFunctionTrace(bool asynchronous)
    {
        const string source = "def make(bind):\n def read():return value+1\n if bind:value=7\n return read\nreturn make(bind)()\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var bind in new[] { false, true })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["bind"] = bind } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    if (bind)
                    {
                        Assert.True(result.Success, result.Failure?.Message);
                        Assert.Equal(new System.Numerics.BigInteger(8), result.ReturnValue);
                    }
                    else
                    {
                        var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
                        Assert.Equal("NameError", failure.ExceptionType);
                        Assert.Equal(2, failure.Span?.Line);
                        Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "read" && frame.Span?.Line == 5);
                    }
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedReturnSurvivesOperandReuseAndCodeDisposal(bool captured)
    {
        var source = "def keep(value):return value\npayload='é'*4096\nheld=keep(payload)\n" +
            "for i in range(16):keep(str(i))\nprint(held is payload)\nreturn held\n";
        if (captured) source = "def make(value):\n def keep():return value\n return keep\npayload='é'*4096\nkeep=make(payload)\nheld=keep()\n" +
            "for i in range(16):keep()\nprint(held is payload)\npayload=None\nkeep=None\nreturn held\n";
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using var script = new LythonEngine().Compile(source, budget);
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal("True\n", retained.StandardOutput);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var second = script.Run(new MockLythonHost()))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.Equal(retained.ReturnValue, second.ReturnValue);
                }
                Assert.Equal(held, budget.ReservedBytes);
                script.Dispose();
                Assert.True(budget.ReservedBytes > 0);
                Assert.Equal(new string('é', 4096), retained.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task CompoundReturnKeepsSuspendingReboundBuiltinAndCleanup(int scopeShape)
    {
        var source = "def count(value):\n result=len(value.lower().split())\n return result\n" +
            "def length(value):\n with open('/pause') as file:\n  return int(file.read())\n" +
            "len=length\nprint(count('A B'))\nprint('late')\n";
        if (scopeShape != 0) source = source.Replace("def count(value):\n result=len(value.lower().split())\n return result\n",
            "def outer():\n def count(value):\n  result=len(value.lower().split())\n  return result\n return count\ncount=outer()\n", StringComparison.Ordinal);
        if (scopeShape == 2) source = source.Replace("def outer():\n", "def outer():\n separator=None\n", StringComparison.Ordinal)
            .Replace("value.lower().split()", "value.lower().split(separator)", StringComparison.Ordinal);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/pause", "7");
            var reached = host.PauseReadUntilCancellation("/pause");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("late", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeepNativeExpressionRemainsSupported()
    {
        // Beyond native-return eligibility, within the existing frontend chain bound.
        var expression = "value" + string.Concat(Enumerable.Repeat(".strip()", 20));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile($"def clean(value):return {expression}\nprint(clean(' x '))", budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            using var result = script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("x\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedNativeFailureKeepsDefiningSourceAndFunctionFrame(bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/helpers.py", "def convert(value):return int(value.strip())\n");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("from helpers import convert\nconvert(' bad ')\n", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions
            {
                SourcePath = "/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helpers" }
            };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.False(result.Success);
                Assert.Equal("ValueError", result.Failure?.ExceptionType);
                var frame = Assert.Single(result.Failure!.StackTrace);
                Assert.Equal("convert", frame.FunctionName);
                Assert.Equal("/helpers.py", frame.SourcePath);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("mapping", false)] [InlineData("mapping", true)]
    [InlineData("conversion", false)] [InlineData("conversion", true)]
    [InlineData("multiply", false)] [InlineData("multiply", true)]
    [InlineData("reflected-multiply", false)] [InlineData("reflected-multiply", true)]
    public async Task ProtocolReturnRetainsItsOwnerUntilSuspendedInputCancels(string shape, bool insideHandler)
    {
        var definition = shape switch
        {
            "mapping" => "def helper(value):return value['key']\nclass Value:\n def __getitem__(self,key):\n",
            "multiply" => "def helper(value):return value*3\nclass Value:\n def __mul__(self,other):\n",
            "reflected-multiply" => "def helper(value):return 3*value\nclass Value:\n def __rmul__(self,other):\n",
            _ => "def helper(value):return int(value)\nclass Value:\n def __int__(self):\n",
        };
        var invocation = insideHandler
            ? "try:raise LookupError('outer')\nexcept LookupError as outer:\n print(helper(Value()))\n print('late')\n"
            : "print(helper(Value()))\nprint('late')\n";
        var source = definition + "  with open('/pause') as file:\n   value=file.read()\n" +
            (shape == "mapping" ? "  return value\n" : "  return int(value)\n") + invocation;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/pause", "7");
            var reached = host.PauseReadUntilCancellation("/pause");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                await reached.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("late", result.StandardOutput);
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

}

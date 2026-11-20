namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormatOperationTests
{
    [Fact]
    public async Task PositionalTextKeepsRepeatedUtf8FragmentsAndMixedFieldOrder()
    {
        await FormatTemplateTests.AssertReusable("""
            method='{1}|{{}}{0}{1:}'.format
            fragment='🦊é'*1300
            assert method('prefix',fragment)==fragment+'|{}prefix'+fragment
            events=[]
            class Field:
             def __format__(self,spec):
              events.append(spec)
              return 'hook'
            assert method('prefix',Field())=='hook|{}prefixhook'
            assert events==['','']
            try:method('prefix')
            except IndexError:pass
            else:raise AssertionError('missing positional field accepted')
            assert method('',fragment)==fragment+'|{}'+fragment
            return 'ok'
            """, requireAsyncIo: false);
    }

    [Fact]
    public async Task NestedFieldsShareNumberingAndPreserveConversionOrder()
    {
        await FormatTemplateTests.AssertReusable("""
            events=[]
            class Field:
             def __init__(self,name):self.name=name
             def __format__(self,spec):
              events.append(self.name+':'+spec)
              return self.name
             def __repr__(self):
              events.append('repr:'+self.name)
              return self.name
            a,b=Field('a'),Field('b')
            assert '{1:{0}}|{0!r:>3}'.format(a,b)=='b|  a'
            assert events==['a:','b:a','repr:a']
            events.clear()
            assert '{:{}}'.format(a,b)=='a'
            assert events==['b:','a:b']
            assert 'value={0}, none={1}, braces={{ok}}'.format(12,None)=='value=12, none=None, braces={ok}'
            return 'ok'
            """, requireAsyncIo: false);
    }

    [Fact]
    public async Task MappingAndStructuralErrorsRemainPythonShaped()
    {
        await FormatTemplateTests.AssertReusable("""
            assert '{name:>3}'.format_map({'name':'x'})=='  x'
            for template in ['{}','{0}']:
             try:template.format_map({'name':'x'})
             except ValueError as error:assert str(error)=='Format string contains positional fields'
             else:raise AssertionError('positional field accepted')
            cases=[('{'+'9'*19+'}','Too many decimal digits in format string'),
                   ('{0!{}}','Unknown conversion specifier {'),
                   ('{0:{1:{2}}}','Max string recursion exceeded')]
            for template,message in cases:
             try:template.format('ab',5,6)
             except ValueError as error:assert str(error)==message
             else:raise AssertionError('invalid format accepted')
            events=[]
            class Items:
             def __getitem__(self,key):events.append(key);return 1
            try:'{0[a]b}'.format(Items())
            except ValueError as error:assert str(error)=="Only '.' or '[' may follow ']' in format field specifier"
            else:raise AssertionError('trailing content accepted')
            assert events==['a']
            return 'ok'
            """, requireAsyncIo: false);
    }

    [Fact]
    public async Task PreparedSelectorsReadLiveDictionariesAndGiveHooksFreshKeys()
    {
        await FormatTemplateTests.AssertReusable("""
            values={'naïve':'before'}
            keys=[]
            class Selector:
             def __getitem__(self,key):
              with open('/value') as handle:
               result=handle.read()
              keys.append(key)
              return result
            class Mutate:
             def __format__(self,spec):
              values['naïve']='after'
              return ''
            method='{0[naïve]}{1}{0[naïve]}'.format
            assert method(values,Mutate())=='beforeafter'
            assert method(Selector(),'|')=='ok|ok'
            assert keys==['naïve','naïve'] and keys[0] is not keys[1]
            try:method({'other':'x'},'|')
            except KeyError as error:assert error.args[0]=='naïve'
            else:raise AssertionError('missing dictionary key accepted')
            return 'ok'
            """);
    }

    [Fact]
    public async Task ExpandedSpecsKeepEscapingAliasesFundedAcrossRepeatedCalls()
    {
        await FormatTemplateTests.AssertReusable("""
            saved=[]
            spec='λ'*4096
            class Field:
             def __format__(self,value):
              assert value==spec
              saved.append(value)
              return ''
            method='{0:{1}}{2:🟩>{3}}'.format
            for repeat in range(3):assert method(Field(),spec,'a',4)=='🟩🟩🟩a'
            assert len(saved)==3
            assert all(value==spec for value in saved)
            assert saved[0] is not saved[1] and saved[1] is not saved[2]
            return 'ok'
            """, requireAsyncIo: false);
    }

    [Fact]
    public async Task NativeFormatPathsShareUtf8SpecifierSemantics()
    {
        await FormatTemplateTests.AssertReusable("""
            from datetime import date
            width,precision=7,2
            spec='🟩>7.2s'
            expected='🟩'*5+'éa'
            value='éabc'
            assert format(value,spec)==expected
            assert f'{value:{spec}}'==expected
            assert '{0:🟩>{1}.{2}s}'.format(value,width,precision)==expected
            assert '{0:🟩>7.2s}'.format(value)==expected
            assert '{value:{specifier}}'.format_map({'value':value,'specifier':spec})==expected
            assert '{0:{1}}'.format(date(2026,11,20),'λ%Y%m%d')=='λ20261120'
            class Echo:
             def __format__(self,value):return value
            assert format(Echo(),spec) is spec
            for spec,error in [('.', 'Format specifier missing precision'),
                               ('??λ','Invalid format specifier \'??λ\' for object of type \'str\'')]:
             try:'{0:{1}}'.format(value,spec)
             except ValueError as failure:assert str(failure)==error
             else:raise AssertionError('invalid specifier accepted')
            return 'ok'
            """, requireAsyncIo: false);
    }
}

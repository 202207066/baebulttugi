"""Apply explicitly approved review rows to a NEW local catalog. Never publishes or changes user data."""
import argparse,copy,json,math,re,zipfile,xml.etree.ElementTree as ET
from pathlib import Path

NS={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def read_sheet(path,name):
    with zipfile.ZipFile(path) as z:
        if sum(i.file_size for i in z.infolist())>120*1024*1024:raise ValueError('Review workbook exceeds size limit')
        def xml(part):
            raw=z.read(part)
            if b'<!DOCTYPE' in raw or b'<!ENTITY' in raw:raise ValueError('XML entities are not supported')
            return ET.fromstring(raw)
        shared=[''.join(x.itertext()) for x in xml('xl/sharedStrings.xml').findall('s:si',NS)] if 'xl/sharedStrings.xml' in z.namelist() else []
        sheet=next(s for s in xml('xl/workbook.xml').findall('s:sheets/s:sheet',NS) if s.get('name')==name)
        rel=sheet.get('{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id')
        target=next(x.get('Target') for x in xml('xl/_rels/workbook.xml.rels') if x.get('Id')==rel)
        if '..' in target or ':' in target:raise ValueError('Invalid worksheet path')
        target=target.lstrip('/') if target.startswith('/') else 'xl/'+target
        rows=[]
        for row in xml(target).findall('s:sheetData/s:row',NS):
            values={}
            for c in row.findall('s:c',NS):
                col=re.match('[A-Z]+',c.get('r','')).group();value=c.find('s:v',NS)
                v=value.text or '' if value is not None else ''
                if c.get('t')=='s':v=shared[int(v)]
                elif c.get('t')=='inlineStr':v=''.join(t.text or '' for t in c.findall('.//s:t',NS))
                elif v and c.get('t') not in ('str','e'):v=float(v)
                values[col]=(v,c.find('s:f',NS) is not None)
            rows.append(values)
        return rows
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--catalog',type=Path,required=True);p.add_argument('--review',type=Path,required=True);p.add_argument('--out',type=Path,required=True);p.add_argument('--version',required=True);a=p.parse_args()
    if a.out.resolve()==a.catalog.resolve():p.error('Use a NEW output path; preserve the original catalog')
    if a.out.exists():p.error('Output already exists; use a new versioned path')
    original=json.loads(a.catalog.read_text(encoding='utf8'));next_catalog=copy.deepcopy(original)
    if not re.fullmatch(r'\d{4}\.\d{1,2}\.\d{1,2}\.\d{1,5}',a.version) or tuple(map(int,a.version.split('.')))<=tuple(map(int,original['Version'].split('.'))):p.error('Version must be newer than input catalog')
    entries={(e['Track'],e['Menu'].get('StableKey') or f"{e['Menu']['Id']}:{e['Menu']['Category']}:{e['Menu']['Name']}"):e for e in next_catalog['Menus']}
    rows=read_sheet(a.review,'1차 검토')
    if [rows[0].get(c,('',False))[0] for c in ['A','B','C','D','E','F','O']]!=['트랙','메뉴키','메뉴명','기존 분류','1인분 단백질(g)','1인분 제공량(g)','검토 승인']:p.error('Unexpected review headers')
    seen=set();approved=0
    for row in rows[1:]:
        def value(c):return row.get(c,('',False))[0]
        ident=(value('A'),value('B'))
        if ident in seen or ident not in entries:p.error('Unknown or duplicate menu key: '+str(ident))
        seen.add(ident);entry=entries[ident];m=entry['Menu']
        if value('C')!=m['Name'] or value('D')!=m['Category'] or (value('E') if value('E')!='' else None)!=m['Protein']:p.error('Menu identity / original protein changed: '+str(ident))
        if value('O') not in ('대기','승인'):p.error('Review status must be 대기 or 승인')
        if value('O')!='승인':continue
        for c in ['F','I','J','K','L','M','N','O','P','Z']:
            if row.get(c,('',False))[1]:p.error('Review inputs must be literal values: '+str(ident)+' '+c)
        grams=value('F');side=m['Category'] in ('주찬','부찬','김치류')
        if side and (not isinstance(grams,(int,float)) or not math.isfinite(grams) or grams<=0 or m['Protein'] is None or m['Protein']/grams*100>100 or value('J')):p.error('Unresolved protein / serving data: '+str(ident))
        if any(value(c) not in ('포함','없음 확인') for c in ['K','L','M']) or value('N'):p.error('Resolve and confirm all extra allergens before approval: '+str(ident))
        base=value('Z');allergens=str(int(base)) if isinstance(base,(int,float)) and math.isfinite(base) and base==int(base) else str(base).strip()
        expanded=re.sub('[①-⑳]',lambda x:str(ord(x[0])-ord('①')+1)+',',allergens)
        codes=re.findall(r'\d+',expanded)
        if allergens!='없음' and (not codes or re.sub(r'[0-9,\s/;·.]+','',expanded) or any(not 1<=int(n)<=19 for n in codes)):p.error('Allergen codes require explicit valid values or 없음: '+str(ident))
        m.update(ServingGrams=grams if isinstance(grams,(int,float)) else None,ServingBasis=str(value('I')),ProteinReviewed=True,ProteinReview='',Almond=value('K'),Sesame=value('L'),OtherBeans=value('M'),AllergyReview='',Allergens=allergens)
        entry['Review']='검토 승인 · '+str(value('P'));approved+=1
    if len(seen)!=len(entries):p.error('Review workbook must contain the complete menu key set')
    if approved==0:p.error('No approved rows; output was not written')
    next_catalog['Version']=a.version
    next_catalog['PublishedAt']='-'.join([a.version.split('.')[0],a.version.split('.')[1].zfill(2),a.version.split('.')[2].zfill(2)])
    a.out.write_text(json.dumps(next_catalog,ensure_ascii=False,separators=(',',':')),encoding='utf8')
    print(json.dumps({'approved':approved,'pending':len(entries)-approved,'output':str(a.out),'published':False},ensure_ascii=False))
if __name__=='__main__':main()

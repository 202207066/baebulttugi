from pathlib import Path
import csv,json,re,math,collections,hashlib,argparse,datetime
parser=argparse.ArgumentParser(description='Export reviewed food-only TSV tables as an official app package.')
parser.add_argument('--version',required=True,help='Monotonically increasing YYYY.M.D.N version')
parser.add_argument('--data-dir',required=True,type=Path)
parser.add_argument('--app-dir',required=True,type=Path)
parser.add_argument('--site-catalog-dir',required=True,type=Path)
a=parser.parse_args()
if not re.fullmatch(r'\d{4}\.\d{1,2}\.\d{1,2}\.\d{1,5}',a.version):parser.error('Invalid version')
data=a.data_dir;wpf=a.app_dir;site=a.site_catalog_dir
if not wpf.is_dir():parser.error('App folder must already exist')
def rows(name):return list(csv.reader((data/(name+'.tsv')).open(encoding='utf8'),delimiter='\t'))
def key(s):return re.sub('[^0-9A-Za-z가-힣]','',s).lower()
def cat(s):return {'국·찌개류':'국류','주찬류':'주찬','부찬류':'부찬','간식류':'후식류'}.get(s,s)
def num(s):
 try:
  n=float(s.replace(',','').strip());return n if math.isfinite(n) and n>=0 else None
 except:return None
def ingredients(text):
 out=[]
 for line in text.strip().splitlines():
  if not line.strip():continue
  m=re.fullmatch(r'(.+):\s*([0-9]+(?:\.[0-9]+)?)\s*(g|kg|ml|mL|l|L|개|봉)\s*',line.strip())
  if not m:return []
  out.append({'Name':m[1].strip(),'Amount':float(m[2]),'Unit':m[3].lower()})
 return out
out={'SchemaVersion':1,'Version':a.version,'PublishedAt':datetime.date.today().isoformat(),'Source':'배불뚝이 공식 메뉴 자료 · 메뉴별 최종 영양값 · 원본 레시피 식재료분량','Menus':[],'Recipes':[],'Cooking':{}}
audit=[]
for t in 'AB':
 nr=rows('nutrition'+t); nutrition=collections.defaultdict(list)
 for r in nr[1:]:
  if r and r[0]:nutrition[key(r[0])].append(r)
 pr=rows('portions'+t); portions=collections.defaultdict(list)
 for r in pr[1:]:
  if len(r)>21:portions[(r[0],cat(r[2]),r[1])].append(r)
 tr=rows('track'+t);hi=next(i for i,r in enumerate(tr[:15]) if '메뉴ID'in r and '메뉴명'in r);h=tr[hi]
 for r in tr[hi+1:]:
  def at(n):return r[h.index(n)].strip() if n in h and h.index(n)<len(r) else ''
  id,name,category=at('메뉴ID'),at('메뉴명'),cat(at('카테고리'))
  if not id or not name:continue
  k=f'{id}:{category}:{name}';ns=nutrition.get(key(name),[]);exact=[n for n in ns if n[0].strip()==name];ns=exact or ns;uniq={tuple(num(x) for x in n[1:5]) for n in ns};review=[]
  vals=list(uniq)[0] if len(uniq)==1 else (None,)*4
  if len(uniq)>1:review.append('같은 이름의 영양값이 서로 달라 확인 필요')
  ps=portions.get((id,category,name),[]);recipe=None;nutrition_source=f'트랙{t} 메뉴별 최종 영양값'
  if not ns and len(ps)==1:
   vals=tuple(num(v) for v in ps[0][6:10]);nutrition_source='원본 식재료 분량정리의 영양값 · '+ps[0][10]
  if any(v is None for v in vals):review.append('영양정보 누락 · 자동 식단 제외')
  if len(ps)==1:
   p=ps[0];text=p[15];source='원본 레시피 1인분량';pv=num(p[6])
   if vals[0] is not None and pv is not None and abs(vals[0]-pv)>0.11:
    # Final nutrition and latest source sometimes refer to different recipe variants.
    # Retain both provenance and an explicit warning; never silently recompute nutrition.
    if ingredients(p[21]):text=p[21];source='기존 레시피 시트 1인분량'
    review.append('최종 영양값과 최신 원본 레시피의 열량이 다름 · 분량 확인 필요')
   ing=ingredients(text)
   if ing:
    recipe={'Track':t,'Key':k,'Name':name,'Category':category,'Source':source+(' · '+' / '.join(review) if review else ''),'Ingredients':ing}
    out['Recipes'].append(recipe)
   else:review.append('식재료·1인분량 확인 필요')
   method=p[11].strip()
   if method and '미기재' not in method:out['Cooking'].setdefault(re.sub(r'\s+','',name),method)
  else:review.append('일치하는 레시피 분량 없음 또는 여러 건 · 확인 필요')
  allergens=','.join(dict.fromkeys(x.strip() for x in (at('알레르기 코드')+','+at('추가 알레르기 코드')).split(',') if x.strip()))
  m={'Track':t,'Menu':{'Id':id,'Name':name,'Category':category,'Allergens':allergens,'Carb':vals[1],'Protein':vals[2],'Fat':vals[3],'Calories':vals[0],'Source':f'공식 자료 {out["Version"]} · '+nutrition_source+(' · '+' / '.join(review) if review else '')},'Review':' / '.join(review)}
  out['Menus'].append(m);audit.append([t,id,name,category,len(recipe['Ingredients']) if recipe else 0,','.join(n for n,v in zip(['kcal','탄수화물','단백질','지방'],vals) if v is None),allergens,m['Review']])
for r in rows('cooking')[1:]:
 if len(r)>1 and r[0].strip() and r[1].strip():
  k=re.sub(r'\s+','',r[0].strip())
  out['Cooking'].setdefault(k,r[1].strip())
assert 1 <= len(out['Menus']) <= 50000
for t in 'AB':
 keys=[x['Menu']['Id']+':'+x['Menu']['Category']+':'+x['Menu']['Name'] for x in out['Menus'] if x['Track']==t];assert len(keys)==len(set(keys))
raw=json.dumps(out,ensure_ascii=False,separators=(',',':')).encode('utf8');(wpf/'OfficialCatalog.json').write_bytes(raw)
site.mkdir(parents=True,exist_ok=True);filename='catalog-'+a.version+'.json';(site/filename).write_bytes(raw)
manifest={'schemaVersion':1,'version':out['Version'],'file':filename,'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw)}
(site/'latest.json').write_text(json.dumps(manifest),encoding='utf8')
with (data/'공식자료_점검.csv').open('w',encoding='utf-8-sig',newline='') as f:
 writer=csv.writer(f);writer.writerow(['트랙','메뉴ID','메뉴명','분류','식재료행수','누락영양','알레르기코드','확인사항']);writer.writerows(audit)
print(json.dumps({'menus':len(out['Menus']),'recipes':len(out['Recipes']),'missingNutrition':sum(bool(a[5]) for a in audit),'recipeReview':sum(bool(a[7]) for a in audit),'bytes':len(raw)},ensure_ascii=False))

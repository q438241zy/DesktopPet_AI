"""Record visually reviewed contact points for the final eleven appearances."""
import json, subprocess, sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
art=root/'artwork/interaction-five'
audit=json.loads((art/'expansion-progress.json').read_text())
def points(size,high,receive,gift,slot,front,box,book,page,sizes=None):
    return dict(size=size,high=high,receive=receive,gift=gift,slot=slot,front=front,box=box,book=book,page=page,sizes=sizes or {})
reviews={
'deepseek-adult':('adult-deepseek-prompts.json',{
 'swim':points([1536,1024],[[143,181],[603,126],[1018,157],[1407,190]],[985,381],[1354,362],[298,339],356,90,[615,317],[971,331],{'highfive':[1764,882]}),
 'sports':points([1536,1024],[[338,148],[778,89],[1147,117],[1503,157]],[985,360],[1358,349],[270,335],350,86,[650,309],[992,328],{'highfive':[2048,683]}),
 'wedding':points([1254,1254],[[173,181],[643,127],[1118,159],[1553,183]],[389,855],[895,842],[417,195],210,63,[902,190],[378,819],{'highfive':[1916,821]})}),
'gpt-adult':('adult-gpt-claude-prompts.json',{
 'original':points([2048,683],[[183,137],[710,111],[1174,108],[1673,127]],[1270,249],[1741,236],[326,284],300,75,[737,270],[1134,270],{'book':[1764,882]}),
 'swim':points([2048,683],[[215,133],[714,105],[1190,120],[1634,140]],[1245,249],[1664,247],[282,329],344,95,[630,294],[990,297],{'book':[1536,1024]}),
 'wedding':points([1536,1024],[[175,153],[714,104],[1205,116],[1667,147]],[990,363],[1370,365],[389,212],226,52,[835,195],[368,862],{'highfive':[2048,745],'book':[1182,1330]}),
 'sports':points([2048,683],[[167,213],[626,138],[1042,175],[1415,204]],[1248,263],[1684,268],[360,257],270,81,[755,268],[1252,264],{'highfive':[1764,882],'book':[1942,809]})}),
'claude-adult':('adult-gpt-claude-prompts.json',{
 'original':points([1536,1024],[[123,166],[626,116],[1092,124],[1532,158]],[997,340],[1375,337],[321,282],300,79,[688,249],[1111,273],{'highfive':[1881,836],'book':[1774,887]}),
 'swim':points([1536,1024],[[162,166],[650,129],[1079,151],[1496,162]],[964,365],[1353,359],[343,295],312,91,[703,268],[1135,278],{'highfive':[1886,834],'book':[1774,887]}),
 'wedding':points([1254,1254],[[254,157],[834,122],[280,759],[829,770]],[407,813],[886,811],[391,192],206,56,[859,172],[420,806]),
 'sports':points([1536,1024],[[69,211],[476,169],[873,191],[1243,217]],[965,351],[1328,364],[255,306],319,77,[632,289],[1031,305],{'book':[1619,971]})})}
for character,(manifest,p) in reviews.items():
    sources={}
    for g in audit['groups']:
        if g['character']==character:
            outfit=g['outfit'];group=g['key'].removeprefix(character+'-'+outfit+'-')
            c=next(c for c in g['candidates'] if c['file']==g['geometryCandidate'])
            sources.setdefault(outfit,{})[group]={'file':c['file'],'rows':c['rows']}
    if character=='claude-adult':sources['wedding']['highfive']={'file':'claude-adult-wedding-highfive-v4.png','rows':2}
    path=art/(character+'-contact-review.json')
    path.write_text(json.dumps(dict(character=character,manifest=manifest,points=p,sources=sources),indent=2)+'\n')
    subprocess.run([sys.executable,str(root/'tools/select-five-contacts.py'),str(path)],check=True,cwd=root)

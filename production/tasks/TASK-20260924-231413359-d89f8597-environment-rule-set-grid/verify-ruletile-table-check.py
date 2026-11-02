import re
base="C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/"
tile=open(base+"production/tasks/TASK-20260924-231413359-d89f8597-environment-rule-set-grid/removed-ruletile/WallRuleTile.asset").read()
rules=[]
for block in tile.split("  - m_Id:")[1:]:
    hexs=re.search(r"m_Neighbors: ([0-9a-f]+)",block).group(1)
    vals=[int.from_bytes(bytes.fromhex(hexs[i:i+8]),"little") for i in range(0,len(hexs),8)]
    pos=[(int(a),int(b)) for a,b in re.findall(r"- \{x: (-?\d+), y: (-?\d+), z: 0\}",block)]
    rt=int(re.search(r"m_RuleTransform: (\d+)",block).group(1))
    rules.append((vals,pos,rt))
print("rules",[(r[0],r[2]) for r in rules])
def rot(p,a):
    x,y=p
    return {0:(x,y),90:(y,-x),180:(-x,-y),270:(-y,x)}[a]
offs=[(-1,1),(0,1),(1,1),(-1,0),(1,0),(-1,-1),(0,-1),(1,-1)]
def matches(rule,floor,a):
    vals,pos,_=rule
    for n,p in zip(vals,pos):
        q=rot(p,a); isf=q in floor
        if n==3 and not isf: return False   # Floor: other==marker
        if n==4 and isf: return False       # Void: other null or != marker
    return True
table=[]
for pat in range(256):
    floor={offs[i] for i in range(8) if pat>>i&1}
    m=0
    for ri,r in enumerate(rules):
        for a in (0,90,180,270):
            if matches(r,floor,a): m|=1<<(ri*4+a//90)
    table.append(m)
src=open(base+"Assets/ColorBlockJam/Tests/Environment/EditMode/EnvironmentRuleSetTableTests.cs").read()
arr=src[src.index("s_ruleTileMatches ="):]
arr=arr[arr.index("{")+1:arr.index("}")]
rec=[int(v) for v in re.findall(r"\d+",arr)]
print("recorded",len(rec),"independent",len(table),"equal",rec==table, "mismatches",sum(1 for a,b in zip(rec,table) if a!=b))
print("rule offsets same as RuleSet order:", all(r[1]==offs for r in rules), "all Rotated:", all(r[2]==1 for r in rules))

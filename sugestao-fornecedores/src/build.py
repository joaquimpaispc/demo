import os
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def rd(rel):
    return open(os.path.join(root, rel), encoding='utf-8').read()
t = rd('src/template.html')
sj = rd('node_modules/xlsx/dist/xlsx.full.min.js').replace('</script', '<\\/script')
core = rd('src/core.js')
data = rd('data/samples/aoa.json').replace('</', '<\\/')
t = t.replace('/*__SHEETJS__*/', sj, 1).replace('/*__CORE__*/', core, 1).replace('/*__DATA__*/null', data, 1)
out = os.path.join(root, 'dist', 'sugestao_fornecedores.html')
os.makedirs(os.path.dirname(out), exist_ok=True)
open(out, 'w', encoding='utf-8').write(t)
print(len(t))

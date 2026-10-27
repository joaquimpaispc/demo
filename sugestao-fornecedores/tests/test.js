const path=require('path'), XLSX=require('xlsx'), fs=require('fs');
const root=path.join(__dirname,'..');
const C=require(path.join(root,'src/core.js'));
const wb=XLSX.readFile(path.join(root,'data/samples/historico.xlsx'),{cellDates:false});
const sheets=wb.SheetNames.map(n=>XLSX.utils.sheet_to_json(wb.Sheets[n],{header:1,raw:true,defval:null,blankrows:true}));
const p=C.parseWorkbook(sheets);
console.log('req',p.requisition.length,'hist',p.history.length);
const refDay=C.toDay('2026-10-26');
const res=C.analyse(p.requisition,p.history,{refDay});
for(const r of res){
  console.log('\n#'+r.line.linha, r.line.designacao,'|',r.key,'|',r.mat.grade,'| basis',r.basis,'| med',r.median&&r.median.toFixed(3),'| est',r.estimate&&r.estimate.toFixed(0));
  for(const s of r.suppliers) console.log('   ',s.score, s.fornecedor.slice(0,30),'enc',s.nOrders,'avg',s.avgPrice&&s.avgPrice.toFixed(3),'last',s.lastPrice&&s.lastPrice.toFixed(3),C.dayToISO(s.lastDay),'lead',s.leadAvg&&s.leadAvg.toFixed(1),s.matchLabel, JSON.stringify(Object.fromEntries(Object.entries(s.components).map(([k,v])=>[k,+v.toFixed(2)]))));
  for(const x of r.rows) if(x.flags.length) console.log('      !',x.h.fornecedor.slice(0,20),C.dayToISO(x.h.data),x.h.designacao.slice(0,35),x.flags.join('; '), x.cmp&&x.cmp.toFixed(3));
}
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify(res.map(r=>({line:r.line,key:r.key,basis:r.basis,estimate:r.estimate,suppliers:r.suppliers.map(s=>({...s,rows:undefined,orders:undefined})),rows:r.rows.map(x=>({...x,h:x.h}))}))));
fs.writeFileSync(path.join(__dirname,'aoa.json'),JSON.stringify(sheets.map(a=>a.map(row=>{row=row||[];let n=row.length;while(n>0&&(row[n-1]===null||row[n-1]===''))n--;return row.slice(0,n);}))));

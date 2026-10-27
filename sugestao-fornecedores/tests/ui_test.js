const path=require('path'), puppeteer=require('puppeteer-core');
const root=path.join(__dirname,'..');
const shot=n=>path.join(__dirname,n);
(async()=>{
 const b=await puppeteer.launch({executablePath:'/usr/bin/google-chrome',headless:'new',args:['--no-sandbox']});
 const p=await b.newPage(); await p.setViewport({width:1500,height:1000});
 const errs=[]; p.on('pageerror',e=>errs.push('pageerror '+e.message)); p.on('console',m=>{if(m.type()==='error')errs.push('console '+m.text())});
 await p.goto('file://'+path.join(root,'dist/sugestao_fornecedores.html'));
 await p.evaluate(()=>{document.getElementById('refDate').value='2026-10-26';document.getElementById('refDate').dispatchEvent(new Event('change'))});
 console.log(await p.$eval('#status',e=>e.innerText));
 console.log(await p.$eval('#kpis',e=>e.innerText.replace(/\n/g,' | ')));
 await p.screenshot({path:shot('s1.png')});
 await p.click('tr.main[data-i="1"]'); await p.screenshot({path:shot('s2.png'),fullPage:false});
 await p.evaluate(()=>document.querySelector('tr.main[data-i="1"]').scrollIntoView());
 await p.screenshot({path:shot('s2b.png')});
 await p.click('.tab[data-t="sups"]'); await p.screenshot({path:shot('s3.png')});
 // upload simple requisition file
 const [fc]=await Promise.all([p.waitForFileChooser(),p.click('#fReq')]); await fc.accept([path.join(root,'data/samples/fornecedores.xlsx')]);
 await new Promise(r=>setTimeout(r,800));
 console.log('AFTER SIMPLE:',await p.$eval('#status',e=>e.innerText));
 await p.click('.tab[data-t="lines"]');
 console.log(await p.$$eval('tr.main',rs=>rs.map(r=>r.innerText.replace(/\s+/g,' ').slice(0,140)).join('\n')));
 await p.screenshot({path:shot('s4.png')});
 const [fc2]=await Promise.all([p.waitForFileChooser(),p.click('#fReq')]); await fc2.accept([path.join(root,'data/samples/historico.xlsx')]);
 await new Promise(r=>setTimeout(r,800));
 console.log('AFTER FULL:',await p.$eval('#status',e=>e.innerText));
 console.log('ERRORS',errs);
 await b.close();
})();

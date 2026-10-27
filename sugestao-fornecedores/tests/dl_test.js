const fs=require('fs'), path=require('path'), puppeteer=require('puppeteer-core');
const root=path.join(__dirname,'..');
const dl=path.join(__dirname,'dl');
fs.mkdirSync(dl,{recursive:true});
(async()=>{
 const b=await puppeteer.launch({executablePath:'/usr/bin/google-chrome',headless:'new',args:['--no-sandbox']});
 const p=await b.newPage(); const errs=[]; p.on('pageerror',e=>errs.push(e.message));
 const cdp=await p.target().createCDPSession(); await cdp.send('Page.setDownloadBehavior',{behavior:'allow',downloadPath:dl});
 await p.goto('file://'+path.join(root,'dist/sugestao_fornecedores.html'));
 await p.evaluate(()=>{document.getElementById('refDate').value='2026-10-26';document.getElementById('refDate').dispatchEvent(new Event('change'))});
 await p.click('#bXlsx'); await p.click('#bCsv');
 await p.click('.tab[data-t="sups"]'); await p.click('#bRfqAll');
 await new Promise(r=>setTimeout(r,2500)); console.log('errs',errs); await b.close();
})();

const { chromium } = require('playwright');
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1200}});
 page.on('console',m=>console.log('[console]',m.type(),m.text()));
 page.on('pageerror',e=>console.log('[pageerror]',e.message));
 await page.goto('http://localhost:4173',{waitUntil:'networkidle'});
 console.log('title:',await page.title());
 console.log('sample:',(await page.locator('body').innerText()).slice(0,250));
 await browser.close();
})();

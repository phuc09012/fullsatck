const { chromium } = require('playwright');
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1200}});
 await page.goto('http://localhost:4173',{waitUntil:'networkidle'});
 console.log('title:', await page.title());
 console.log('top:', (await page.locator('body').innerText()).slice(0,280));
 await page.click('text=Sách mới nhập');
 await page.waitForTimeout(300);
 const bookCards = await page.locator('.book-card').count();
 console.log('book cards visible:', bookCards);
 console.log('pagination text:', await page.locator('.pagination-row').innerText());
 await page.click('text=Báo cáo');
 await page.waitForTimeout(300);
 console.log('reports text:', (await page.locator('#reports').innerText()).slice(0,300));
 await browser.close();
})();

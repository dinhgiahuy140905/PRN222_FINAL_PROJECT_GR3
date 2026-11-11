// Uses a dedicated, seeded web/SQL fixture created by RoleUiBrowserTests.cs.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn } = require('node:child_process');
(async () => {
const [base, otherStore, artifacts] = process.argv.slice(2);
assert.ok(base?.startsWith('http://127.0.0.1:'));
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'saleshub-role-browser-'));
const chrome = spawn(process.env.BROWSER_PATH || 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    ['--headless', '--disable-gpu', '--no-first-run', '--remote-debugging-port=0', '--window-size=1440,1000', `--user-data-dir=${profile}`],
    { stdio: 'ignore', windowsHide: true });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
let ws, nextId = 0;
const pending = new Map(), failures = [], checks = [];
const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++nextId;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('CDP timeout: ' + method)); }, 15000);
    pending.set(id, { resolve: value => { clearTimeout(timer); resolve(value); }, reject: error => { clearTimeout(timer); reject(error); } });
    ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expression => {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true, userGesture: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text + ': ' + result.exceptionDetails.exception?.description);
    return result.result.value;
};
const waitFor = async (condition, label) => {
    for (let i = 0; i < 150; i++) {
        try { if (await condition()) return; } catch { }
        await delay(50);
    }
    throw new Error('Timed out: ' + label);
};
const go = async route => {
    await evaluate('window.__navigatingRoleTest=true');
    await send('Page.navigate', { url: base + route });
    await waitFor(() => evaluate(`!window.__navigatingRoleTest && location.href.startsWith(${JSON.stringify(base)}) && document.readyState === 'complete'`), route);
};
const click = async selector => {
    const exists = await evaluate(`(() => { const e=document.querySelector(${JSON.stringify(selector)}); if(!e || e.disabled || !e.getClientRects().length) return false; e.click(); return true; })()`);
    assert.ok(exists, 'Missing/hidden button: ' + selector);
};
const fields = async values => evaluate(`(() => { const values=${JSON.stringify(values)}; for(const [name,value] of Object.entries(values)) {const e=document.querySelector('[name="'+name+'"]'); if(!e) throw new Error('Missing field '+name); e.value=value; e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true}));} })()`);
const submit = async selector => {
    await evaluate('window.__submittingRoleTest=true');
    await click(selector);
    await waitFor(() => evaluate('!window.__submittingRoleTest && document.readyState === "complete"'), 'form submit ' + selector);
};
const check = async (expression, label) => { assert.ok(await evaluate(expression), label); checks.push(label); };
const noBlankAlerts = async () => check(`![...document.querySelectorAll('.alert-danger')].some(e => e.getClientRects().length && getComputedStyle(e).display!=='none' && !e.textContent.trim())`, 'no visible blank red alert');
const productRow = sku => `[...document.querySelectorAll('tbody tr')].find(row=>row.textContent.includes(${JSON.stringify(sku)}))`;
try {
    await waitFor(() => fs.existsSync(path.join(profile, 'DevToolsActivePort')), 'Chrome startup');
    const port = fs.readFileSync(path.join(profile, 'DevToolsActivePort'), 'utf8').split('\n')[0];
    const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
    ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { ws.addEventListener('open', resolve, { once: true }); ws.addEventListener('error', reject, { once: true }); });
    ws.addEventListener('message', event => {
        const message = JSON.parse(event.data);
        if (message.id) {
            const callback = pending.get(message.id); pending.delete(message.id);
            if (callback) message.error ? callback.reject(new Error(message.error.message)) : callback.resolve(message.result);
        }
        if (message.method === 'Runtime.exceptionThrown') failures.push(message.params.exceptionDetails.exception?.description || message.params.exceptionDetails.text);
        if (message.method === 'Page.javascriptDialogOpening') send('Page.handleJavaScriptDialog', { accept: true }).catch(() => {});
    });
    await send('Page.enable'); await send('Runtime.enable');
    fs.mkdirSync(artifacts, { recursive: true });
    for (const role of ['owner', 'staff']) {
        await send('Network.clearBrowserCookies');
        await go('/Account/Login');
        await fields({ Email: role + '@browser.test', Password: 'BrowserTest123!' });
        await submit('form[action*="Login"] button[type="submit"]');
        await check(`location.pathname==='/Home/Dashboard'`, role + ' login');
        await check(`!!document.querySelector('.sidebar a[href="/Staff"] , .sidebar a[href="/Staff/Index"]')===${role === 'owner'}`, role + ' staff management visibility');
        await noBlankAlerts();
        await click('#notificationToggle');
        await check(`document.getElementById('notificationToggle').getAttribute('aria-expanded')==='true' && document.querySelector('.notification-menu').classList.contains('show')`, role + ' notifications open');
        await click('.notification-menu a[href*="low-stock"]');
        await waitFor(() => evaluate(`location.search.includes('low-stock') && document.readyState==='complete'`), 'notification destination');
        await check(`document.body.textContent.includes('Shared low stock')`, role + ' notification links to filtered products');
        await go('/Order/Create'); await noBlankAlerts();
        const beforeTheme = await evaluate('document.documentElement.dataset.theme');
        await click('#themeToggle');
        await check(`document.documentElement.dataset.theme!==${JSON.stringify(beforeTheme)}`, role + ' theme toggle');
        await noBlankAlerts();
        await click('#sidebarCollapse');
        await check(`document.documentElement.dataset.sidebar==='collapsed'`, role + ' collapse sidebar');
        await click('#sidebarBrand');
        await check(`document.documentElement.dataset.sidebar==='expanded'`, role + ' expand sidebar');
        await click('.user-info');
        await waitFor(() => evaluate(`location.pathname.startsWith('/Settings') && document.readyState==='complete'`), 'profile link');
        await check(`!!document.querySelector('[data-settings-target="store"]')===${role === 'owner'}`, role + ' store settings visibility');
        for (const section of ['profile', 'appearance', 'preferences', 'security', ...(role === 'owner' ? ['store'] : [])]) {
            await click(`[data-settings-target="${section}"]`);
            await check(`!document.querySelector('[data-settings-section="${section}"]').hidden`, role + ' settings ' + section);
            await noBlankAlerts();
        }
        await click('[data-settings-target="profile"]');
        await fields({ 'Profile.FullName': 'Browser ' + role + ' updated', 'Profile.Phone': '0901234567' });
        await submit('form[action*="UpdateProfile"] button[type="submit"]');
        await check(`document.body.textContent.includes('Browser ${role} updated')`, role + ' save profile');
        await click('[data-settings-target="preferences"]');
        await evaluate(`document.getElementById('preferenceNumber').value='en-US'; document.getElementById('preferenceDate').value='MM/dd/yyyy'; document.getElementById('preferenceCurrency').value='code';`);
        await submit('#preferencesForm button[type="submit"]');
        await check(`localStorage.getItem('saleshub-numberFormat')==='en-US' && localStorage.getItem('saleshub-currencyDisplay')==='code'`, role + ' save preferences');
        if (role === 'owner') {
            await click('[data-settings-target="store"]');
            await fields({ 'Store.Name': 'Role UI Store A edited' });
            await submit('form[action*="UpdateStore"] button[type="submit"]');
            await check(`document.querySelector('.store-switcher').textContent.includes('Role UI Store A edited')`, 'Owner saves store settings');
            await go('/Staff/Index');
            await fields({ 'Invite.Email': 'candidate@browser.test' });
            await submit('form[action*="Invite"] button[type="submit"]');
            await check(`document.querySelector('.staff-list').textContent.includes('candidate@browser.test')`, 'Owner adds staff');
            await evaluate(`window.__submittingRoleTest=true; [...document.querySelectorAll('.staff-list-item')].find(e=>e.textContent.includes('candidate@browser.test')).querySelector('button').click()`);
            await waitFor(() => evaluate(`!window.__submittingRoleTest && document.readyState==='complete'`), 'remove staff');
            await check(`!document.querySelector('.staff-list').textContent.includes('candidate@browser.test')`, 'Owner removes staff');
        }
        await go('/Settings/Index?section=security');
        await fields({ 'Security.CurrentPassword': 'BrowserTest123!', 'Security.NewPassword': 'BrowserChanged456!', 'Security.ConfirmPassword': 'BrowserChanged456!' });
        await submit('form[action*="ChangePassword"] button[type="submit"]');
        await check(`location.pathname.startsWith('/Settings') && document.querySelector('.alert-success')?.textContent.includes('mật khẩu')`, role + ' change password retains login');
        await click('.sidebar-help');
        await waitFor(() => evaluate(`location.pathname==='/Home/Help' && document.readyState==='complete'`), 'help link');
        await check(`document.body.textContent.includes('Hướng dẫn sử dụng SalesHub')`, role + ' help');
        await go('/Product/Create');
        const sku = 'BROWSER-' + role.toUpperCase();
        await fields({ Name: 'Browser ' + role + ' product', Sku: sku, Unit: 'pcs', SalePrice: '12.50', OpeningStock: '5', ReorderLevel: '1' });
        await submit('.page-content form button[type="submit"]');
        const productId = await evaluate(`Number(${productRow(sku)}.querySelector('a[href*="/Product/Edit/"]').getAttribute('href').split('/').pop())`);
        assert.ok(productId, role + ' create product');
        checks.push(role + ' create product');
        await evaluate(`${productRow(sku)}.querySelector('.product-more-button').click()`);
        await check(`${productRow(sku)}.querySelector('.dropdown-menu').classList.contains('show')`, role + ' product actions menu');
        await evaluate(`${productRow(sku)}.querySelector('a[href*="/Product/Edit/"]').click()`);
        await waitFor(() => evaluate(`location.pathname==='/Product/Edit/${productId}' && document.readyState==='complete'`), 'edit product');
        await fields({ Name: 'Browser ' + role + ' edited' });
        await submit('.page-content form button[type="submit"]');
        await go('/Product/AdjustStock/' + productId);
        await fields({ QuantityChange: '2', Note: 'Browser adjustment' });
        await submit('.page-content form button[type="submit"]');
        await check(`${productRow(sku)}.textContent.includes('7')`, role + ' stock adjustment');
        await evaluate(`${productRow(sku)}.querySelector('.product-more-button').click()`);
        await evaluate(`window.__submittingRoleTest=true; ${productRow(sku)}.querySelector('form[action*="Deactivate"] button').click()`);
        await waitFor(() => evaluate(`!window.__submittingRoleTest && document.readyState==='complete'`), 'deactivate product');
        await go('/Product/Index?status=inactive');
        await check(`!!${productRow(sku)}`, role + ' deactivate product');
        await evaluate(`${productRow(sku)}.querySelector('.product-more-button').click()`);
        await evaluate(`window.__submittingRoleTest=true; ${productRow(sku)}.querySelector('form[action*="Activate"] button').click()`);
        await waitFor(() => evaluate(`!window.__submittingRoleTest && document.readyState==='complete'`), 'reactivate product');
        await go('/Product/Index');
        await check(`!!${productRow(sku)}`, role + ' reactivate product');
        await go('/Category/Index');
        await fields({ Name: 'Browser ' + role + ' category' });
        await submit('.page-content form[action*="Create"] button[type="submit"]');
        await check(`document.querySelector('.category-list').textContent.includes('Browser ${role} category')`, role + ' create category');
        await submit('.category-list form[action*="Delete"] button');
        await check(`!document.querySelector('.category-list')?.textContent.includes('Browser ${role} category')`, role + ' delete empty category');
        await go('/Customer/Create');
        await fields({ Name: 'Browser ' + role + ' customer', Phone: '0901234567' });
        await submit('.page-content form button[type="submit"]');
        await check(`document.body.textContent.includes('Browser ${role} customer')`, role + ' create customer');
        await evaluate(`[...document.querySelectorAll('tbody tr')].find(e=>e.textContent.includes('Browser ${role} customer')).querySelector('a[href*="/Customer/Edit/"]').click()`);
        await waitFor(() => evaluate(`location.pathname.startsWith('/Customer/Edit/') && document.readyState==='complete'`), 'edit customer');
        await fields({ Name: 'Browser ' + role + ' customer edited' });
        await submit('.page-content form button[type="submit"]');
        await check(`document.body.textContent.includes('Browser ${role} customer edited')`, role + ' edit customer');
        await go('/Order/Create');
        await fields({ 'Items[0].ProductId': String(productId), 'Items[0].Quantity': '8', CustomerName: 'Browser ' + role + ' order', Note: 'Keep browser note' });
        await submit('#orderForm button[type="submit"]');
        await check(`document.querySelector('.validation-summary-errors')?.textContent.includes('chỉ còn')`, role + ' visible stock validation');
        await fields({ 'Items[0].Quantity': '1' });
        await click('#addOrderItem'); await click('#addOrderItem');
        await evaluate(`document.querySelectorAll('.order-item-row')[1].querySelector('.remove-order-item').click()`);
        await check(`document.querySelectorAll('.order-item-row').length===2 && document.querySelectorAll('.quantity-input')[1].name==='Items[1].Quantity'`, role + ' add remove reindex');
        await evaluate(`document.querySelectorAll('.order-item-row')[1].querySelector('.remove-order-item').click()`);
        await submit('#orderForm button[type="submit"]');
        await check(`location.pathname.startsWith('/Order/Details/')`, role + ' create order');
        const orderId = await evaluate(`Number(location.pathname.split('/').pop())`);
        await submit('form[action*="Complete"] button[type="submit"]');
        await submit('form[action*="RecordPayment"] button[type="submit"]');
        await check(`document.body.textContent.includes('Đã thanh toán')`, role + ' complete and record payment');
        await go('/Order/Invoice/' + orderId);
        await check(`document.querySelector('.invoice-document') && document.body.textContent.includes('ĐÃ THANH TOÁN')`, role + ' invoice');
        await click('a[target="_blank"]');
        await waitFor(async () => (await send('Target.getTargets')).targetInfos.some(info=>info.targetId!==target.id && info.url.includes('/Order/Invoice/') && /print=true/i.test(info.url)), 'print invoice opens tab');
        checks.push(role + ' print invoice opens tab');
        for (const info of (await send('Target.getTargets')).targetInfos) {
            if (info.type==='page' && info.targetId!==target.id) await send('Target.closeTarget', { targetId: info.targetId });
        }
        await go('/Order/Details/' + orderId);
        await submit('form[action*="RecordRefund"] button[type="submit"]');
        await submit('form[action*="Cancel"] button[type="submit"]');
        await check(`document.body.textContent.includes('Đã hủy')`, role + ' refund and cancel');
        await go('/Report/Index');
        await fields({ from: '2026-01-01', to: '2026-12-31', currencyCode: 'VND' });
        await submit('.page-content form button[type="submit"]');
        await check(`document.querySelector('[name="currencyCode"]').value==='VND'`, role + ' report filter');
        await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: false });
        await click('#menuToggle');
        await check(`document.body.classList.contains('sidebar-open')`, role + ' mobile menu open');
        await click('#sidebarBackdrop');
        await check(`!document.body.classList.contains('sidebar-open')`, role + ' mobile menu close');
        await click('#notificationToggle');
        await check(`document.querySelector('.notification-menu').classList.contains('show')`, role + ' mobile notifications');
        await click('#notificationToggle');
        await send('Emulation.clearDeviceMetricsOverride');
        if (role === 'owner') {
            await go('/Store/Select');
            await evaluate(`document.querySelector('input[name="storeId"][value="${otherStore}"]').closest('form').querySelector('button').click()`);
            await waitFor(() => evaluate(`location.pathname==='/Home/Dashboard' && document.body.textContent.includes('Role UI Empty Store B') && document.readyState==='complete'`), 'switch to staff membership');
            await check(`!document.querySelector('.sidebar a[href="/Staff"],.sidebar a[href="/Staff/Index"]')`, 'Owner account with Staff membership cannot manage staff');
            await click('#notificationToggle');
            await check(`document.querySelector('.notification-empty') && !document.querySelector('.notification-count')`, 'notifications isolated from other store');
        } else {
            await go('/Staff/Index');
            await check(`location.pathname==='/Account/AccessDenied'`, 'Staff direct management access rejected');
            await go('/Store/Create');
            await check(`location.pathname==='/Account/AccessDenied'`, 'Staff cannot create store');
            await go('/Order/Create');
        }
        await noBlankAlerts();
        const screenshot = await send('Page.captureScreenshot', { format: 'png' });
        fs.writeFileSync(path.join(artifacts, role + '.png'), Buffer.from(screenshot.data, 'base64'));
        await submit('form[action*="Logout"] button[type="submit"]');
        await go('/Order/Create');
        await check(`location.pathname==='/Account/Login'`, role + ' logout protects routes');
    }
    assert.deepEqual(failures, [], 'Uncaught browser JavaScript errors');
    fs.writeFileSync(path.join(artifacts, 'checks.json'), JSON.stringify(checks, null, 2));
    console.log('PASS: ' + checks.length + ' browser checks for Owner and Staff; no uncaught JavaScript errors.');
} catch (error) {
    if (ws?.readyState === WebSocket.OPEN) {
        try { const screenshot = await send('Page.captureScreenshot', { format: 'png' }); fs.mkdirSync(artifacts, { recursive: true }); fs.writeFileSync(path.join(artifacts, 'failure.png'), Buffer.from(screenshot.data, 'base64')); } catch { }
    }
    console.error(error.stack); process.exitCode = 1;
} finally {
    ws?.close();
    chrome.kill();
    await new Promise(resolve => { if (chrome.exitCode !== null) resolve(); else { chrome.once('exit', resolve); setTimeout(resolve, 3000); } });
    fs.rmSync(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
}
})().catch(error => { console.error(error.stack); process.exitCode = 1; });

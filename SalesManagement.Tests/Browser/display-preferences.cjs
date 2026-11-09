// Run: node SalesManagement.Tests/Browser/display-preferences.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { pathToFileURL } = require('node:url');
const script = fs.readFileSync(path.join(__dirname, '../../SalesManagement.Web/wwwroot/js/display-format.js'), 'utf8');
const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'saleshub-display-'));
try {
    const fixture = path.join(directory, 'test.html');
    fs.writeFileSync(fixture, `<!doctype html><meta charset="utf-8">
        <span id="vnd" data-money="1234" data-currency="VND"></span>
        <span id="usd" data-money="12.5" data-currency="USD"></span>
        <span id="eur" data-money="12.5" data-currency="EUR"></span>
        <span id="number" data-number="12345"></span>
        <span id="date" data-date="2026-11-09T22:30:00" data-time="true"></span>
        <input id="price" type="number" value="12.50"><pre id="result">PENDING</pre>
        <script>localStorage.clear();</script><script>${script}</script><script>
        const check = (value, expected) => { if (value !== expected) throw new Error(value + ' !== ' + expected); };
        try {
            check(document.getElementById('vnd').textContent, '1.234 ₫');
            check(document.getElementById('usd').textContent, '12,50 USD');
            check(document.getElementById('eur').textContent, '12,50 EUR');
            check(document.getElementById('date').textContent, '09/11/2026 22:30');
            localStorage.setItem('saleshub-numberFormat', 'en-US');
            localStorage.setItem('saleshub-currencyDisplay', 'code');
            localStorage.setItem('saleshub-dateFormat', 'MM/dd/yyyy');
            window.dispatchEvent(new Event('saleshub:displaychange'));
            check(document.getElementById('vnd').textContent, '1,234 VND');
            check(document.getElementById('usd').textContent, '12.50 USD');
            check(document.getElementById('number').textContent, '12,345');
            check(document.getElementById('date').textContent, '11/09/2026 22:30');
            localStorage.setItem('saleshub-dateFormat', 'yyyy-MM-dd');
            window.dispatchEvent(new Event('storage'));
            check(document.getElementById('date').textContent, '2026-11-09 22:30');
            check(document.getElementById('price').value, '12.50');
            check(window.SalesHubDisplay.money(325.95, 'USD'), '325.95 USD');
            localStorage.setItem('saleshub-numberFormat', 'invalid');
            check(window.SalesHubDisplay.money(325.95, 'USD'), '325,95 USD');
            document.getElementById('result').textContent = 'PASS';
        } catch(error) { document.getElementById('result').textContent = 'FAIL: ' + error.message; }
        </script>`);
    const browser = process.env.BROWSER_PATH || 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
    const result = spawnSync(browser, ['--headless', '--disable-gpu', '--no-first-run',
        `--user-data-dir=${path.join(directory, 'profile')}`, '--dump-dom', '--virtual-time-budget=1000', pathToFileURL(fixture).href],
        { encoding: 'utf8', timeout: 30000 });
    if (result.error) throw result.error;
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /<pre id="result">PASS<\/pre>/, result.stdout);
    console.log('PASS: number/date/currency preferences update display and preserve numeric inputs.');
} finally { fs.rmSync(directory, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 }); }

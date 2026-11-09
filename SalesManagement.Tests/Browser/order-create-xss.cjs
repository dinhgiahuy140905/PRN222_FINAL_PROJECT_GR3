// Run: node SalesManagement.Tests/Browser/order-create-xss.cjs
// Requires Chrome on Windows, or set BROWSER_PATH to a Chromium executable.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { pathToFileURL } = require('node:url');
const currencyCode = process.argv[2] || 'VND';
const numberLocale = process.argv[3] || 'vi-VN';
const currencyDisplay = process.argv[4] || 'symbol';
const displayScript = fs.readFileSync(path.join(__dirname, '../../SalesManagement.Web/wwwroot/js/display-format.js'), 'utf8');
const expectedMoney = amount => new Intl.NumberFormat(numberLocale, {
    minimumFractionDigits: currencyCode === 'VND' ? 0 : 2,
    maximumFractionDigits: currencyCode === 'VND' ? 0 : 2
}).format(amount) + ' ' + (currencyCode === 'VND' && currencyDisplay === 'symbol' ? '₫' : currencyCode);
assert.ok(['VND', 'USD', 'EUR'].includes(currencyCode));

const view = fs.readFileSync(path.join(__dirname, '../../SalesManagement.Web/Views/Order/Create.cshtml'), 'utf8');
const products = [
    { Id: 1, Name: '</option></select><img src=x onerror="window.__xssTest=1">', Sku: '<>&"\'', Stock: 10, SalePrice: 100 },
    { Id: 2, Name: 'Product <>&"\'', Sku: '</option></select><img src=x onerror="window.__xssTest=1">', Stock: 20, SalePrice: 50 }
];
if (currencyCode !== 'VND') {
    products[0].SalePrice = 100.1;
    products[1].SalePrice = 50.25;
}
const escape = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const label = product => `${product.Name} · ${product.Sku} (còn ${product.Stock})`;
// Model the initial Razor-encoded row; run the actual view JavaScript unchanged.
const options = products.map(p => `<option value="${p.Id}">${escape(label(p))}</option>`).join('');
const script = view.match(/<script>([\s\S]*?)<\/script>/)[1]
    .replace('@Html.Raw(JsonSerializer.Serialize(Model.CurrencyCode))', JSON.stringify(currencyCode))
    .replace('@Html.Raw(productsJson)', JSON.stringify(products).replace(/</g, '\\u003c'));
const checks = `
    const check = (condition, message) => { if (!condition) throw new Error(message); };
    const rows = () => [...document.querySelectorAll('.order-item-row')];
    const labels = ${JSON.stringify(products.map(label)).replace(/</g, '\\u003c')};
    const verify = row => {
        const options = row.querySelector('.product-select').options;
        check(options.length === 3, 'Unexpected option count');
        labels.forEach((label, i) => check(options[i + 1].textContent === label, 'Product label changed'));
    };
    try {
        verify(rows()[0]);
        document.getElementById('addOrderItem').click();
        document.getElementById('addOrderItem').click();
        check(rows().length === 3, 'Add row failed');
        rows().forEach(verify);
        rows()[1].querySelector('.remove-order-item').click();
        check(rows().length === 2, 'Remove middle row failed');
        rows().forEach((row, i) => {
            check(row.querySelector('select').name === 'Items[' + i + '].ProductId', 'Product reindex failed');
            check(row.querySelector('input').name === 'Items[' + i + '].Quantity', 'Quantity reindex failed');
            row.querySelector('select').value = String(i + 1);
            row.querySelector('input').value = String(i + 2);
            row.querySelector('input').dispatchEvent(new Event('input'));
        });
        const discount = document.getElementById('Discount');
        discount.value = '25';
        discount.dispatchEvent(new Event('input'));
        check(document.getElementById('orderSubtotal').textContent === ${JSON.stringify(expectedMoney(currencyCode === 'VND' ? 350 : 350.95))}, 'Subtotal failed');
        check(document.getElementById('orderTotal').textContent === ${JSON.stringify(expectedMoney(currencyCode === 'VND' ? 325 : 325.95))}, 'Discount failed');
        setTimeout(() => {
            document.getElementById('result').textContent =
                !document.querySelector('img') && window.__xssTest !== 1 ? 'PASS' : 'FAIL: XSS';
        }, 100);
    } catch (error) { document.getElementById('result').textContent = 'FAIL: ' + error.message; }
`;
const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'saleshub-xss-'));
try {
    const fixture = path.join(directory, 'test.html');
    fs.writeFileSync(fixture, `<!doctype html><meta charset="utf-8">
        <button id="addOrderItem">Add</button><input id="Discount" value="0">
        <div id="orderItems"><div class="order-item-row">
        <select class="product-select" name="Items[0].ProductId"><option value="">Choose</option>${options}</select>
        <input class="quantity-input" name="Items[0].Quantity" value="1">
        <span class="item-price"></span><strong class="item-total"></strong>
        <button class="remove-order-item">Remove</button></div></div>
        <span id="orderSubtotal"></span><span id="orderTotal"></span><pre id="result">PENDING</pre>
        <script>localStorage.setItem('saleshub-numberFormat', ${JSON.stringify(numberLocale)});localStorage.setItem('saleshub-currencyDisplay', ${JSON.stringify(currencyDisplay)});</script>
        <script>${displayScript}</script><script>${script}</script><script>${checks}</script>`);
    const browser = process.env.BROWSER_PATH || 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
    const result = spawnSync(browser, ['--headless', '--disable-gpu', '--no-first-run',
        `--user-data-dir=${path.join(directory, 'profile')}`, '--dump-dom', '--virtual-time-budget=1000',
        pathToFileURL(fixture).href], { encoding: 'utf8', timeout: 30000 });
    if (result.error) throw result.error;
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /<pre id="result">PASS<\/pre>/, result.stdout);
    console.log('PASS: XSS payloads render as text; add/remove, reindex, totals and discount work.');
} finally {
    fs.rmSync(directory, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}

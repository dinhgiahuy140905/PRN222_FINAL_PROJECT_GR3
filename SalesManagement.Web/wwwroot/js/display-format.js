(() => {
    const read = (key, choices, fallback) => {
        try { const value = localStorage.getItem('saleshub-' + key); return choices.includes(value) ? value : fallback; }
        catch { return fallback; }
    };
    const numberLocale = () => read('numberFormat', ['vi-VN', 'en-US'], 'vi-VN');
    const number = value => new Intl.NumberFormat(numberLocale()).format(Number(value));
    const money = (value, code) => {
        const decimals = code === 'VND' ? 0 : 2;
        const label = code === 'VND' && read('currencyDisplay', ['symbol', 'code'], 'symbol') === 'symbol' ? '₫' : code;
        return new Intl.NumberFormat(numberLocale(), { minimumFractionDigits: decimals, maximumFractionDigits: decimals }).format(Number(value)) + ' ' + label;
    };
    const date = (value, includeTime) => {
        // Stored timestamps are shop-local; preserve the date without converting timezones.
        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value);
        if (!match) return value;
        const [, year, month, day, hour, minute] = match;
        const format = read('dateFormat', ['dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd'], 'dd/MM/yyyy');
        const result = format === 'yyyy-MM-dd' ? `${year}-${month}-${day}` : format === 'MM/dd/yyyy' ? `${month}/${day}/${year}` : `${day}/${month}/${year}`;
        return result + (includeTime ? ` ${hour}:${minute}` : '');
    };
    const render = (root = document) => {
        root.querySelectorAll('[data-money]').forEach(element => { element.textContent = money(element.dataset.money, element.dataset.currency); });
        root.querySelectorAll('[data-number]').forEach(element => { element.textContent = number(element.dataset.number); });
        root.querySelectorAll('[data-date]').forEach(element => { element.textContent = date(element.dataset.date, element.dataset.time === 'true'); });
    };
    window.SalesHubDisplay = { money, number, date, render };
    render();
    window.addEventListener('storage', render.bind(null, document));
    window.addEventListener('saleshub:displaychange', render.bind(null, document));
})();

document.addEventListener("DOMContentLoaded", () => {
    "use strict";
    const selector = 'form[data-live-filter]';
    const state = new WeakMap();
    const originalSortButtons = new WeakSet(document.querySelectorAll(".sort-button"));
    const originalClickableRows = new WeakSet(document.querySelectorAll(".announcement-clickable-row"));
    let pending, sequence = 0;
    function status(form, message) {
        let box = form.querySelector('[data-live-filter-status]');
        if (!message) { if (box) { box.textContent = ''; box.hidden = true; } return; }
        if (!box) { box = document.createElement('span'); box.dataset.liveFilterStatus = ''; box.className = 'form-text w-100'; box.setAttribute('role', 'status'); form.append(box); }
        box.textContent = message;
        box.hidden = false;
    }
    // Patch existing nodes so filter, sort and row event handlers stay attached.
    function patch(old, fresh) {
        if (old.nodeType !== fresh.nodeType || old.nodeName !== fresh.nodeName) { old.replaceWith(fresh.cloneNode(true)); return; }
        if (old.nodeType === Node.TEXT_NODE) { if (old.nodeValue !== fresh.nodeValue) old.nodeValue = fresh.nodeValue; return; }
        if (old.nodeType !== Node.ELEMENT_NODE || old.tagName === 'SCRIPT' || old.hasAttribute('data-live-filter-status')) return;
        const focused = old === document.activeElement;
        for (const attr of [...old.attributes]) if (!fresh.hasAttribute(attr.name) && attr.name !== 'nonce') old.removeAttribute(attr.name);
        for (const attr of fresh.attributes) if (attr.name !== 'nonce' && !(focused && attr.name === 'value')) old.setAttribute(attr.name, attr.value);
        let index = 0;
        for (const next of [...fresh.childNodes].filter(n => n.nodeName !== 'SCRIPT')) {
            let current = old.childNodes[index];
            if (next.nodeType === Node.ELEMENT_NODE && next.id) {
                const keyed = [...old.children].find(n => n.id === next.id);
                if (keyed && keyed !== current) { old.insertBefore(keyed, current || null); current = keyed; }
            }
            if (current) patch(current, next); else old.append(next.cloneNode(true));
            index++;
        }
        while (old.childNodes.length > index) old.lastChild.remove();
        if (!focused && old instanceof HTMLInputElement && old.type !== 'file') { old.value = fresh.value; old.checked = fresh.checked; }
        if (!focused && old instanceof HTMLSelectElement) old.value = fresh.value;
    }
    async function update(form, url) {
        pending?.abort(); const abort = new AbortController(); pending = abort; const version = ++sequence;
        const main = form.closest('main'); if (!main) { status(form, 'Unable to find the results area. Please reload this page.'); return; }
        main.setAttribute('aria-busy', 'true'); status(form, '');
        const timer = setTimeout(() => abort.abort(), 30000);
        try {
            const response = await fetch(url, { signal: abort.signal, credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected && new URL(response.url).pathname.toLowerCase().includes('/identity/')) throw new Error('Could not update. Check your connection or sign in again.');
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const next = page.querySelector('main');
            if (!next || !next.querySelector(selector)) throw new Error('Unexpected response. Reload this page to continue.');
            if (version !== sequence) return;
            window.EduSmartTables?.enhance(next);
            const x = window.scrollX, y = window.scrollY;
            const regions = [...main.querySelectorAll('.table-responsive')].map(n => [n, n.scrollLeft, n.scrollTop]);
            patch(main, next);
            main.querySelectorAll(selector).forEach(node => node.classList.add("live-filter-form"));
            window.EduSmartTables?.enhance(main);
            regions.forEach(([node, left, top]) => { if (node.isConnected) { node.scrollLeft = left; node.scrollTop = top; } });
            window.history.replaceState(null, '', url);
            window.scrollTo({ left: x, top: y, behavior: 'instant' });
            status(form, '');
            document.dispatchEvent(new CustomEvent('edusmart:filters-updated'));
        } catch (error) {
            if (version === sequence) status(form, error.name === 'AbortError' ? 'The request timed out. Change a filter or submit again to retry.' : error.message);
        } finally { clearTimeout(timer); if (version === sequence) main.removeAttribute('aria-busy'); }
    }
    function schedule(form, delay) {
        const previous = state.get(form); clearTimeout(previous);
        pending?.abort(); sequence++;
        state.set(form, setTimeout(() => { if (form.checkValidity()) form.requestSubmit(); }, delay));
    }
    document.querySelectorAll(selector).forEach(form => {
        form.classList.add('live-filter-form');
        form.addEventListener('input', event => { if (['text', 'search', 'number'].includes(event.target.type)) schedule(form, 350); });
        form.addEventListener('change', () => schedule(form, 0));
        form.addEventListener('submit', event => {
            event.preventDefault(); clearTimeout(state.get(form));
            const url = new URL(form.action || location.href, location.href); url.search = '';
            for (const [key, value] of new FormData(form)) if (String(value).trim()) url.searchParams.append(key, value);
            if (event.submitter?.name) url.searchParams.set(event.submitter.name, event.submitter.value);
            update(form, url);
        });
    });
    document.addEventListener('click', event => {
        const sort = event.target.closest('.sort-button');
        if (sort && !originalSortButtons.has(sort)) {
            const form = document.querySelector(selector);
            const column = sort.dataset.sort;
            const by = document.getElementById('SortBy'), direction = document.getElementById('SortDirection');
            if (form && column && by && direction) {
                event.preventDefault();
                direction.value = by.value === column && direction.value === 'asc' ? 'desc' : 'asc';
                by.value = column; form.requestSubmit(); return;
            }
        }
        const row = event.target.closest('.announcement-clickable-row');
        if (row && !originalClickableRows.has(row) && !event.target.closest('a,button,input,select,textarea,form') && row.dataset.detailsUrl) {
            const destination = new URL(row.dataset.detailsUrl, location.href);
            if (destination.origin === location.origin) location.assign(destination.href);
            return;
        }
        const link = event.target.closest('a');
        if (!link || event.defaultPrevented || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download')) return;
        const form = document.querySelector(selector); if (!form) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin || url.pathname !== location.pathname || url.hash) return;
        // Same-page reset, filter and pagination links use the same partial update.
        event.preventDefault(); clearTimeout(state.get(form)); update(form, url);
    });
});

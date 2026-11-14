(() => {
    'use strict';
    const page = document.querySelector('.tutor-students-page');
    if (!page) return;
    const form = document.getElementById('roster-filter-form');
    const results = document.getElementById('roster-results');
    const message = page.querySelector('[data-roster-status]');
    let current = new URL(location.href), controller, version = 0, timer, busy = false, expired = false;
    function fromForm() {
        const url = new URL(page.dataset.rosterUrl, location.origin);
        for (const [key, value] of new FormData(form)) if (String(value).length) url.searchParams.set(key, value);
        url.searchParams.set('page', '1'); return url;
    }
    async function load(url, userAction = false) {
        if (expired) return;
        if (userAction) current = new URL(url);
        const ownVersion = ++version;
        controller?.abort(); controller = new AbortController(); busy = true;
        const endpoint = new URL(url); endpoint.searchParams.set('fragment', 'true');
        if (!userAction) endpoint.searchParams.set('live', 'true');
        results.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(endpoint, { signal: controller.signal, cache:'no-store', headers:{'X-Requested-With':'XMLHttpRequest'} });
            if (response.status === 401 || response.status === 403 || response.redirected) {
                expired = true; throw Error('Your session has expired. Please sign in again.');
            }
            if (!response.ok) throw Error(response.status === 404 ? 'This class is no longer available. Select All My Classes to continue.' : 'Unable to update students. Please try again.');
            const html = await response.text();
            if (ownVersion !== version) return;
            const doc = new DOMParser().parseFromString(html, 'text/html');
            const section = doc.querySelector('[data-roster-page]');
            if (!section) throw Error('Unable to update students. Please reload this page.');
            const normalized = section.outerHTML;
            if (results.dataset.snapshot !== normalized) { results.innerHTML = normalized; results.dataset.snapshot = normalized; }
            current = new URL(url); current.searchParams.delete('fragment'); current.searchParams.delete('live');
            current.searchParams.set('page', section.dataset.rosterPage);
            form.elements.sort.value = current.searchParams.get('sort') || 'name_asc';
            if (userAction) history.pushState(null, '', current);
            message.textContent = 'Up to date · Updates automatically every 15 seconds.';
        } catch (error) {
            if (error.name !== 'AbortError' && ownVersion === version) message.textContent = error.message;
        } finally {
            if (ownVersion === version) { busy = false; results.setAttribute('aria-busy', 'false'); }
        }
    }
    function filter(delay = 0) {
        clearTimeout(timer); version++; controller?.abort(); busy = false;
        timer = setTimeout(() => { timer = null; void load(fromForm(), true); }, delay);
    }
    form.addEventListener('submit', event => { event.preventDefault(); filter(); });
    form.addEventListener('change', event => { if (event.target.tagName === 'SELECT') filter(); });
    form.elements.search.addEventListener('input', () => filter(350));
    results.addEventListener('click', event => {
        const link = event.target.closest('[data-roster-link]');
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault(); clearTimeout(timer); timer = null; void load(new URL(link.href), true);
    });
    function poll() {
        if (!busy && !timer && !expired && !document.hidden && !results.contains(document.activeElement)) void load(current);
    }
    window.setInterval(poll, 15000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) poll(); });
    window.addEventListener('focus', poll);
    window.addEventListener('popstate', () => {
        current = new URL(location.href);
        for (const name of ['search','classId','status','sort']) form.elements[name].value = current.searchParams.get(name) || (name === 'sort' ? 'name_asc' : '');
        clearTimeout(timer); timer = null; void load(current);
    });
})();


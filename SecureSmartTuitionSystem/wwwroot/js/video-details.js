(() => {
    'use strict';
    const form = document.querySelector('[data-video-viewing-form]');
    let page = 1, serial = 0, controller, debounce;
    const host = document.querySelector('[data-video-viewing-host]');
    const message = document.querySelector('[data-video-viewing-message]');
    const retry = document.querySelector('[data-video-viewing-retry]');
    let lastParts = null;
    async function refresh() {
        if (!form || document.hidden) return;
        controller?.abort(); controller = new AbortController();
        const aborter = controller;
        const current = ++serial;
        const url = new URL(form.action, location.href);
        url.search = new URLSearchParams(new FormData(form));
        url.searchParams.set('Page', String(page));
        host.setAttribute('aria-busy', 'true');
        if (retry) retry.disabled = true;
        if (!host.childElementCount) message.textContent = 'Loading viewing information…';
        const timeout = setTimeout(() => aborter.abort(), 12000);
        try {
            const response = await fetch(url, { signal: aborter.signal, cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.redirected) throw new Error();
            const html = await response.text();
            if (current !== serial) return;
            const doc = new DOMParser().parseFromString(html, 'text/html');
            const results = doc.querySelector('[data-video-viewing-results]');
            if (!results) throw new Error();
            const keys = ['totals', 'rows', 'pagination'];
            const incoming = Object.fromEntries(keys.map(key => [key, results.querySelector(`[data-video-viewing-part="${key}"]`)]));
            const updated = results.querySelector('[data-video-viewing-updated]');
            if (keys.some(key => !incoming[key]) || !updated) throw new Error();
            const nextParts = Object.fromEntries(keys.map(key => [key, incoming[key].innerHTML]));
            const focusPage = document.activeElement?.dataset.videoViewingPage;
            let paginationChanged = false;
            if (!host.childElementCount) {
                host.replaceChildren(results);
            } else {
                for (const key of keys) {
                    if (lastParts?.[key] === nextParts[key]) continue;
                    const currentPart = host.querySelector(`[data-video-viewing-part="${key}"]`);
                    if (!currentPart) throw new Error();
                    currentPart.replaceChildren(...Array.from(incoming[key].childNodes));
                    if (key === 'pagination') paginationChanged = true;
                }
                const currentTime = host.querySelector('[data-video-viewing-updated]');
                if (currentTime && currentTime.textContent !== updated.textContent) currentTime.textContent = updated.textContent;
            }
            lastParts = nextParts;
            if (focusPage && paginationChanged) (host.querySelector(`[data-video-viewing-page="${Number(focusPage)}"]`) || form.querySelector('[name="Search"]')).focus({ preventScroll: true });
            message.textContent = '';
            if (retry) {
                if (document.activeElement === retry) form.querySelector('[name="Search"]').focus({ preventScroll: true });
                retry.hidden = true;
            }
        } catch (error) {
            if (current === serial) {
                message.textContent = 'Could not update viewing information. Check your connection or sign-in, then select Retry. If the video changed, reload this page.';
                if (retry) retry.hidden = false;
            }
        } finally {
            clearTimeout(timeout);
            if (current === serial) { host.removeAttribute('aria-busy'); if (retry) retry.disabled = false; }
        }
    }
    if (form) {
        retry?.addEventListener('click', refresh);
        form.addEventListener('submit', event => { event.preventDefault(); refresh(); });
        form.querySelector('select').addEventListener('change', () => { page = 1; refresh(); });
        form.querySelector('[name="Search"]').addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => { page = 1; refresh(); }, 450); });
        host.addEventListener('click', event => {
            const button = event.target.closest('[data-video-viewing-page]');
            if (button) { page = Number(button.dataset.videoViewingPage); refresh(); }
        });
        document.addEventListener('visibilitychange', () => { if (document.hidden) { controller?.abort(); serial++; } else refresh(); });
        // Only replace the small viewing report; the player and selected filters remain intact.
        setInterval(() => { if (!host.contains(document.activeElement)) refresh(); }, 20000);
        refresh();
    }
    document.querySelector('[data-video-detail-state]')?.addEventListener('submit', event => {
        if (!confirm('Change this recorded lesson’s availability to students?')) event.preventDefault();
    });
})();

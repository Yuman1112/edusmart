(() => {
    'use strict';
    const panel = document.querySelector('[data-availability-live]');
    const form = document.getElementById('tutor-availability-form');
    const root = panel || form;
    if (!root || !root.dataset.liveUrl || !window.fetch) return;
    const endpoint = new URL(root.dataset.liveUrl, location.href);
    if (endpoint.origin !== location.origin) return;
    const message = root.querySelector('[data-availability-live-status]');
    let active = true, expired = false, busy = false, timer, controller, revision = 0;
    function status(text) { if (message) { message.textContent=text; message.classList.toggle('d-none',!text); } }
    async function refresh() {
        if (!active || expired || busy || document.hidden || form?.dataset.saving === 'true') return;
        busy = true;
        const requestRevision = revision;
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 12000);
        try {
            const response = await fetch(endpoint.href, {credentials:'same-origin',cache:'no-store',signal:controller.signal,headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (!active) return;
            if (response.redirected || response.status===401 || response.status===403) {
                expired=true; clearInterval(timer); status('Automatic updates stopped. Sign in again to continue.'); return;
            }
            if (!response.ok) throw new Error('Unavailable');
            const doc = new DOMParser().parseFromString(await response.text(),'text/html');
            const next = doc.querySelector('[data-availability-state]');
            if (!next?.dataset.version) throw new Error('Invalid response');
            if (!active || requestRevision!==revision || form?.dataset.saving==='true') return;
            if (form) {
                if (next.dataset.version !== form.elements.namedItem('Version').value) {
                    form.dataset.stale = 'true';
                    form.querySelector('[data-availability-stale-notice]')?.classList.remove('d-none');
                    document.dispatchEvent(new Event('availability:stale'));
                }
            } else {
                const current = panel.querySelector('[data-availability-state]');
                if (current.dataset.version !== next.dataset.version) {
                    const scroll = current.querySelector('.table-responsive')?.scrollLeft || 0;
                    const focused = current.contains(document.activeElement) ? document.activeElement.closest('a')?.getAttribute('href') : null;
                    const replacement = document.importNode(next,true);
                    current.replaceWith(replacement);
                    const table = replacement.querySelector('.table-responsive'); if (table) table.scrollLeft=scroll;
                    if (focused) Array.from(replacement.querySelectorAll('a')).find(link=>link.getAttribute('href')===focused)?.focus({preventScroll:true});
                }
            }
            status('');
        } catch {
            if (active) status('Updates are temporarily unavailable. Retrying automatically.');
        } finally { clearTimeout(timeout); busy=false; }
    }
    document.addEventListener('availability:saved',()=>{revision++;});
    window.addEventListener('focus',refresh);
    document.addEventListener('visibilitychange',refresh);
    function start() { timer=setInterval(refresh,5000); refresh(); }
    window.addEventListener('pagehide',()=>{active=false;clearInterval(timer);controller?.abort();});
    window.addEventListener('pageshow',event=>{if(event.persisted&&!expired){active=true;start();}});
    start();
})();

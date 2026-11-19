(() => {
    const panel = document.querySelector('[data-teaching-details]');
    if (!panel) return;
    const endpoint = new URL(panel.dataset.liveUrl, location.href);
    if (endpoint.origin !== location.origin) return;
    const message = panel.querySelector('[data-detail-status]');
    let active = true, expired = false, busy = false, controller, timer;
    async function refresh() {
        if (!active || expired || busy || document.hidden) return;
        busy = true;
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 12000);
        try {
            const response = await fetch(endpoint, { credentials:'same-origin', cache:'no-store', signal:controller.signal, headers:{'X-Requested-With':'XMLHttpRequest'} });
            if (!active) return;
            if (response.redirected || response.status === 401 || response.status === 403) {
                expired = true; clearInterval(timer);
                message.textContent = 'Automatic updates stopped. Sign in again to continue.';
                message.classList.remove('d-none'); return;
            }
            if (!response.ok) throw new Error('Unavailable');
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const next = page.querySelector('[data-teaching-detail-content]');
            if (!next) throw new Error('Invalid response');
            if (!active) return;
            const current = panel.querySelector('[data-teaching-detail-content]');
            if (current.dataset.version !== next.dataset.version) current.replaceWith(document.importNode(next, true));
            message.textContent = ''; message.classList.add('d-none');
        } catch (error) {
            if (active) { message.textContent = 'Updates are temporarily unavailable. Retrying automatically.'; message.classList.remove('d-none'); }
        } finally { clearTimeout(timeout); busy = false; }
    }
    function start() { timer = setInterval(refresh, 5000); refresh(); }
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', refresh);
    window.addEventListener('pagehide', () => { active=false; clearInterval(timer); controller?.abort(); });
    window.addEventListener('pageshow', event => { if (event.persisted && !expired) { active=true; start(); } });
    start();
})();

(() => {
    function init() {
        const status = document.getElementById('material-refresh-status');
        let results = document.querySelector('[data-material-results]');
        if (!status || !results || !status.dataset.materialRefreshUrl) return;
        let busy = false, stopped = false, sequence = 0, controller, timer;
        let previous = null;
        const normal = 'Materials update automatically while this page is visible.';
        function say(text) { if (status.textContent !== text) status.textContent = text; }
        async function refresh() {
            if (busy || stopped || document.hidden) return;
            busy = true;
            const request = ++sequence;
            const abort = new AbortController();
            controller = abort;
            const timeout = setTimeout(() => abort.abort(), 12000);
            try {
                const response = await fetch(status.dataset.materialRefreshUrl, {
                    credentials: 'same-origin', cache: 'no-store', signal: abort.signal,
                    headers: { Accept: 'text/html' }
                });
                if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html')) throw new Error();
                const html = await response.text();
                if (request !== sequence || document.hidden || stopped) return;
                const parsed = new DOMParser().parseFromString(html, 'text/html');
                const next = parsed.querySelector('[data-material-results]');
                if (!next || parsed.querySelector('script')) throw new Error();
                // Keep keyboard focus and open link menus stable until the user leaves the results.
                if (!results.contains(document.activeElement) && previous !== next.outerHTML) {
                    results.replaceWith(next);
                    results = next;
                    previous = next.outerHTML;
                }
                say(normal);
            } catch {
                if (request === sequence && !document.hidden && !stopped)
                    say('Unable to update materials. Retrying automatically. Refresh the page if this continues.');
            } finally {
                clearTimeout(timeout);
                if (request === sequence) busy = false;
            }
        }
        function cancel() { ++sequence; controller?.abort(); busy = false; }
        function start() { clearInterval(timer); timer = setInterval(refresh, 10000); }
        document.addEventListener('visibilitychange', () => { if (document.hidden) cancel(); else refresh(); });
        window.addEventListener('focus', refresh);
        window.addEventListener('pagehide', () => { stopped = true; clearInterval(timer); cancel(); });
        window.addEventListener('pageshow', event => { if (event.persisted) { stopped = false; start(); refresh(); } });
        start();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

(() => {
    function init() {
        const status = document.getElementById('material-refresh-status');
        const feedback = document.getElementById('material-action-status');
        const filters = document.querySelector('[data-material-filters]');
        let results = document.querySelector('[data-material-results]');
        if (!status || !results || !filters) return;
        let current = new URL(results.dataset.materialUrl || location.href, location.href);
        let sequence = 0, controller, saving = false, stopped = false, timer, searchTimer, feedbackTimer;
        const normal = 'Materials update automatically while this page is visible.';
        function say(message) {
            status.textContent = message;
            status.hidden = status.dataset.quiet === 'true' && message === normal;
        }
        function announce(message, success) {
            if (!feedback) return;
            clearTimeout(feedbackTimer);
            feedback.hidden = false;
            feedback.className = 'alert ' + (success ? 'alert-success' : 'alert-warning');
            feedback.textContent = message;
            if (success) feedbackTimer = setTimeout(() => { feedback.hidden = true; }, 5000);
        }
        function signature(element) {
            const copy = element.cloneNode(true);
            copy.querySelectorAll('input[name="__RequestVerificationToken"]').forEach(input => input.removeAttribute('value'));
            return copy.outerHTML;
        }
        let previous = signature(results);
        function menus(dispose = false) {
            results.querySelectorAll('[data-bs-toggle="dropdown"]').forEach(button => {
                const Dropdown = window.bootstrap?.Dropdown;
                if (!Dropdown) return;
                if (dispose) Dropdown.getInstance(button)?.dispose();
                else Dropdown.getOrCreateInstance(button, { popperConfig: config => ({ ...config, strategy: 'fixed' }) });
            });
        }
        function syncForm() {
            ['Search', 'ClassId', 'Status', 'Sort'].forEach(name => {
                const field = filters.elements.namedItem(name);
                if (field) field.value = current.searchParams.get(name) || (name === 'Sort' ? 'updated_desc' : name === 'Status' ? 'active' : '');
            });
        }
        async function load(url = current, navigation = false, push = false) {
            if (stopped || saving || (!navigation && document.hidden)) return;
            if (!navigation && (results.querySelector('.dropdown-menu.show') || results.querySelector('select') === document.activeElement)) return;
            const requested = new URL(url, location.href);
            if (requested.origin !== location.origin) return;
            controller?.abort();
            const request = ++sequence;
            const abort = new AbortController();
            controller = abort;
            const timeout = setTimeout(() => abort.abort(), 12000);
            requested.searchParams.set('fragment', 'true');
            try {
                const response = await fetch(requested, { credentials: 'same-origin', cache: 'no-store', signal: abort.signal, headers: { Accept: 'text/html' } });
                if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html')) throw new Error();
                const parsed = new DOMParser().parseFromString(await response.text(), 'text/html');
                const next = parsed.querySelector('[data-material-results]');
                if (!next || parsed.querySelector('script')) throw new Error();
                if (request !== sequence || stopped) return;
                const nextSignature = signature(next);
                if (navigation || nextSignature !== previous) {
                    menus(true);
                    results.replaceWith(next);
                    results = next;
                    previous = nextSignature;
                    menus();
                }
                current = new URL(next.dataset.materialUrl || requested, location.href);
                current.searchParams.delete('fragment');
                if (navigation) {
                    history[push ? 'pushState' : 'replaceState'](null, '', current);
                    syncForm();
                }
                say(normal);
            } catch {
                if (request === sequence && !stopped && !document.hidden) say('Unable to update materials. Please try again. Automatic updates will retry.');
            } finally { clearTimeout(timeout); }
        }
        function formUrl(form) {
            const url = new URL(form.action, location.href);
            for (const [key, value] of new FormData(form)) url.searchParams.set(key, value);
            url.searchParams.set('Page', '1');
            return url;
        }
        document.addEventListener('submit', async event => {
            const form = event.target;
            if (form.matches('[data-material-status-form]')) {
                event.preventDefault();
                event.stopImmediatePropagation();
                if (saving || !window.confirm(form.dataset.confirm)) return;
                const body = new FormData(form);
                const url = new URL(form.action, location.href);
                url.searchParams.set('json', 'true');
                saving = true;
                ++sequence;
                controller?.abort();
                const button = event.submitter;
                if (button) button.disabled = true;
                window.bootstrap?.Dropdown.getInstance(form.closest('.dropdown')?.querySelector('[data-bs-toggle="dropdown"]'))?.hide();
                try {
                    const response = await fetch(url, { method: 'POST', body, credentials: 'same-origin', headers: { Accept: 'application/json' } });
                    if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error();
                    const data = await response.json();
                    if (typeof data.message !== 'string') throw new Error();
                    announce(data.message, response.ok && data.ok === true);
                } catch { announce('Could not confirm the change. Check the current status before trying again.', false); }
                finally {
                    saving = false;
                    if (button) button.disabled = false;
                    await load(current, true);
                }
            } else if (form.matches('[data-material-filters], [data-material-sort-form]')) {
                event.preventDefault();
                clearTimeout(searchTimer);
                load(formUrl(form), true, true);
            }
        }, true);
        document.addEventListener('change', event => {
            const form = event.target.closest('form');
            if (form?.matches('[data-material-sort-form]') || (form === filters && event.target.tagName === 'SELECT')) {
                clearTimeout(searchTimer);
                load(formUrl(form), true, true);
            }
        });
        filters.addEventListener('input', event => {
            if (event.target.name !== 'Search') return;
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => load(formUrl(filters), true, true), 450);
        });
        document.addEventListener('click', event => {
            const link = event.target.closest('a[data-material-nav]');
            if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            load(link.href, true, true);
        });
        function cancel() { ++sequence; controller?.abort(); }
        document.addEventListener('visibilitychange', () => { if (document.hidden) cancel(); else load(); });
        window.addEventListener('focus', () => load());
        window.addEventListener('popstate', () => load(location.href, true));
        window.addEventListener('pagehide', () => { stopped = true; cancel(); clearInterval(timer); clearTimeout(searchTimer); });
        function start() { clearInterval(timer); timer = setInterval(() => load(), 10000); }
        window.addEventListener('pageshow', event => { if (event.persisted) { stopped = false; start(); load(); } });
        menus();
        start();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();

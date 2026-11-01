(() => {
    function init() {
        const form = document.getElementById('material-create-form');
        if (!form) return;
        const select = form.querySelector('select[name="ClassSessionId"]');
        const submit = form.querySelector('button[type="submit"]');
        const empty = document.getElementById('material-no-classes');
        const message = document.getElementById('material-class-update');
        if (!select || !submit || !empty || !message) return;
        let known = false, busy = false, submitting = false, invalidated = false;
        let choicesKey = '', sequence = 0, controller, interval;
        const allowed = () => Boolean(select.value) && Array.from(select.options).some(o => o.value === select.value && o.value !== '');
        function updateButton() { submit.disabled = submitting; }
        function warn(text) {
            if (message.textContent !== text) message.textContent = text;
            message.hidden = false;
        }
        async function refresh(force = false) {
            if (submitting || document.hidden || (busy && !force)) return;
            controller?.abort();
            const request = ++sequence;
            const abort = new AbortController(); controller = abort; busy = true;
            const timeout = setTimeout(() => abort.abort(), 12000);
            try {
                const response = await fetch(form.dataset.classChoicesUrl, {
                    credentials: 'same-origin', cache: 'no-store', signal: abort.signal,
                    headers: { Accept: 'application/json' }
                });
                if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('Unavailable');
                const data = await response.json();
                if (request !== sequence || document.hidden) return;
                if (!Array.isArray(data.classes) || data.classes.some(x => typeof x.id !== 'string' || typeof x.text !== 'string')) throw new Error('Invalid response');
                const previous = select.value;
                const key = JSON.stringify(data.classes);
                if (key !== choicesKey) {
                    const options = [];
                    const placeholder = document.createElement('option');
                    placeholder.value = ''; placeholder.textContent = 'Select assigned class'; options.push(placeholder);
                    data.classes.forEach(item => {
                        const option = document.createElement('option');
                        option.value = item.id; option.textContent = item.text; options.push(option);
                    });
                    select.replaceChildren(...options);
                    select.value = data.classes.some(x => x.id === previous) ? previous : '';
                    choicesKey = key;
                }
                empty.hidden = data.classes.length > 0;
                select.disabled = data.classes.length === 0;
                known = true;
                if (previous && !select.value) {
                    invalidated = true;
                    warn('Your selected class is no longer available. Choose another assigned class. Your title, description and file selection have been kept.');
                } else if (!invalidated) {
                    message.hidden = true;
                }
                updateButton();
            } catch {
                if (request !== sequence || document.hidden) return;
                known = false;
                warn('Unable to update assigned classes. Your input has been kept. Availability will be checked again automatically. If the session expired, sign in again.');
                updateButton();
            } finally {
                clearTimeout(timeout);
                if (request === sequence) busy = false;
            }
        }
        select.addEventListener('change', () => {
            invalidated = false; message.hidden = true; updateButton();
        });
        form.addEventListener('submit', event => {
            if (event.defaultPrevented) return;
            if (!known || !allowed()) {
                event.preventDefault(); warn('Select a currently assigned class before uploading.'); refresh(true); return;
            }
            if (window.jQuery?.fn?.valid && !window.jQuery(form).valid()) { event.preventDefault(); return; }
            submitting = true; controller?.abort(); updateButton();
        });
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) { controller?.abort(); ++sequence; busy = false; }
            else refresh(true);
        });
        window.addEventListener('focus', () => refresh());
        function startTimer() { clearInterval(interval); interval = setInterval(() => refresh(), 10000); }
        window.addEventListener('pagehide', () => { clearInterval(interval); controller?.abort(); ++sequence; busy = false; });
        window.addEventListener('pageshow', event => { if (event.persisted) { submitting = false; known = false; updateButton(); startTimer(); refresh(true); } });
        updateButton(); startTimer(); refresh();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

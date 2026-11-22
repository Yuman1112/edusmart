(() => {
    'use strict';
    const panel = document.querySelector('[data-availability-live]');
    const form = document.getElementById('tutor-availability-form');
    const root = panel || form;
    if (!root || !root.dataset.liveUrl || !window.fetch) return;
    const endpoint = new URL(root.dataset.liveUrl, location.href);
    if (endpoint.origin !== location.origin) return;
    const message = root.querySelector('[data-availability-live-status]');
    let active = true, expired = false, busy = false, timer, controller, revision = 0, queued = false;
    const toolbar = document.querySelector('[data-availability-week-form]');
    const weekInput = toolbar?.elements.namedItem('week');
    let displayedWeek = weekInput?.value;
    let pendingWeek = null;
    const weekControls = toolbar ? [...toolbar.querySelectorAll('button, input[type="date"]')] : [];
    const originalDisabled = new Map(weekControls.map(control => [control, control.disabled]));
    const weekStatus = toolbar ? document.createElement('span') : null;
    if (weekStatus) {
        weekStatus.className = 'form-text m-0';
        weekStatus.setAttribute('role', 'status');
        weekStatus.setAttribute('aria-live', 'polite');
        toolbar.append(weekStatus);
    }
    function lockWeek(locked) {
        weekControls.forEach(control => control.disabled = locked || originalDisabled.get(control));
        toolbar?.setAttribute('aria-busy', String(locked));
    }
    function normalizedWeek(value) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
        const date = new Date(value + 'T00:00:00Z');
        if (Number.isNaN(date.getTime()) || date.toISOString().slice(0,10) !== value) return null;
        date.setUTCDate(date.getUTCDate() - (date.getUTCDay() + 6) % 7);
        return date.toISOString().slice(0,10);
    }
    function restoreWeekAddress() {
        if (!displayedWeek) return;
        const address = new URL(location.href);
        address.searchParams.set('week', displayedWeek);
        history.replaceState(null, '', address.href);
    }
    function chooseWeek(value, updateAddress = true) {
        if (!weekInput || pendingWeek || expired) return;
        const target = normalizedWeek(value);
        if (!target || target < '2000-01-01' || target > '2100-12-31') {
            weekInput.value = displayedWeek;
            if (weekStatus) weekStatus.textContent = 'Choose a week between 2000 and 2100.';
            return;
        }
        // Keep the rendered date and table together until the requested week is ready.
        weekInput.value = displayedWeek;
        pendingWeek = { value: target, updateAddress };
        endpoint.searchParams.set('week', target);
        lockWeek(true);
        if (weekStatus) weekStatus.textContent = 'Updating…';
        revision++; queued = true; controller?.abort(); refresh();
    }
    toolbar?.addEventListener('submit',event=>{event.preventDefault();chooseWeek(weekInput.value);});
    weekInput?.addEventListener('change',()=>chooseWeek(weekInput.value));
    toolbar?.querySelectorAll('[data-week-shift]').forEach(button=>button.addEventListener('click',()=>{
        const date=new Date(weekInput.value+'T00:00:00Z'); date.setUTCDate(date.getUTCDate()+Number(button.dataset.weekShift));
        if (!Number.isNaN(date.getTime())) chooseWeek(date.toISOString().slice(0,10));
    }));
    toolbar?.querySelector('[data-week-today]')?.addEventListener('click',()=>{
        const parts=Object.fromEntries(new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Kuala_Lumpur',year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date()).map(part=>[part.type,part.value]));
        chooseWeek(`${parts.year}-${parts.month}-${parts.day}`);
    });
    window.addEventListener('popstate',()=>{if(toolbar)chooseWeek(new URL(location.href).searchParams.get('week')||weekInput.value,false);});
    function status(text) { if (message) { message.textContent=text; message.classList.toggle('d-none',!text); } }
    async function refresh() {
        if (!active || expired || busy || document.hidden || form?.dataset.saving === 'true') return;
        busy = true; queued=false;
        const requestRevision = revision;
        const requestController = new AbortController();
        controller = requestController;
        const timeout = setTimeout(() => requestController.abort(), 12000);
        try {
            const response = await fetch(endpoint.href, {credentials:'same-origin',cache:'no-store',signal:requestController.signal,headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (!active || requestRevision !== revision) return;
            if (response.redirected || response.status===401 || response.status===403) {
                expired=true; clearInterval(timer);
                if (pendingWeek) { pendingWeek = null; lockWeek(false); restoreWeekAddress(); if (weekStatus) weekStatus.textContent = 'Sign in again to change weeks.'; }
                status('Automatic updates stopped. Sign in again to continue.'); return;
            }
            if (!response.ok) throw new Error('Unavailable');
            const doc = new DOMParser().parseFromString(await response.text(),'text/html');
            const next = doc.querySelector('[data-availability-state]');
            if (!next?.dataset.version) throw new Error('Invalid response');
            if (!active || requestRevision!==revision || form?.dataset.saving==='true') return;
            if (pendingWeek && next.dataset.week !== pendingWeek.value) throw new Error('Unexpected week');
            if (form) {
                if (next.dataset.version !== form.elements.namedItem('Version').value) {
                    form.dataset.stale = 'true';
                    form.querySelector('[data-availability-stale-notice]')?.classList.remove('d-none');
                    document.dispatchEvent(new Event('availability:stale'));
                }
            } else {
                const current = panel.querySelector('[data-availability-state]');
                if (current.dataset.version !== next.dataset.version || current.dataset.week !== next.dataset.week) {
                    const scroll = current.querySelector('.table-responsive')?.scrollLeft || 0;
                    const focused = current.contains(document.activeElement) ? document.activeElement.closest('a')?.getAttribute('href') : null;
                    const replacement = document.importNode(next,true);
                    current.replaceWith(replacement);
                    const table = replacement.querySelector('.table-responsive'); if (table) table.scrollLeft=scroll;
                    if (focused) Array.from(replacement.querySelectorAll('a')).find(link=>link.getAttribute('href')===focused)?.focus({preventScroll:true});
                }
            }
            if (toolbar && next.dataset.week) {
                weekInput.value=next.dataset.week;
                const label=toolbar.querySelector('[data-availability-week-label]'); if(label)label.textContent=next.dataset.weekLabel;
                endpoint.searchParams.set('week',next.dataset.week);
                displayedWeek = next.dataset.week;
                if (pendingWeek) {
                    if (pendingWeek.updateAddress) {
                        const address = new URL(location.href);
                        address.searchParams.set('week', next.dataset.week);
                        history.pushState(null, '', address.href);
                    }
                    pendingWeek = null;
                    lockWeek(false);
                    if (weekStatus) weekStatus.textContent = '';
                }
            }
            status('');
        } catch {
            if (active && requestRevision===revision) {
                if (pendingWeek) {
                    pendingWeek = null;
                    endpoint.searchParams.set('week', displayedWeek);
                    weekInput.value = displayedWeek;
                    lockWeek(false);
                    restoreWeekAddress();
                    if (weekStatus) weekStatus.textContent = 'Could not change week. Your previous week is still shown. Please try again.';
                } else status('Updates are temporarily unavailable. Retrying automatically.');
            }
        } finally { clearTimeout(timeout); busy=false; if(queued&&active&&!expired)refresh(); }
    }
    document.addEventListener('availability:saved',()=>{revision++;});
    window.addEventListener('focus',refresh);
    document.addEventListener('visibilitychange',refresh);
    function start() { timer=setInterval(refresh,5000); refresh(); }
    window.addEventListener('pagehide',()=>{active=false;clearInterval(timer);controller?.abort();});
    window.addEventListener('pageshow',event=>{if(event.persisted&&!expired){active=true;start();}});
    start();
})();

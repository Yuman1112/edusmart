document.addEventListener('DOMContentLoaded', () => {
    if (!document.querySelector('[data-correction-page]')) return;
    let modal, instance, trigger, lastSignature = null;
    let resultsBusy = false, detailBusy = false, posting = false, stopped = false, dirty = false, closingAfterSave = false;
    let resultsAbort, detailAbort, detailSequence = 0, lastFilterInput = 0;
    const liveStatus = message => { const el = document.querySelector('[data-correction-live-status]'); if (el) el.textContent = message; };
    function feedback(message) {
        let el = document.querySelector('[data-correction-feedback]');
        if (!el) { el = document.createElement('div'); el.dataset.correctionFeedback = ''; el.setAttribute('role', 'status'); document.querySelector('[data-correction-page]').prepend(el); }
        if (el) { el.textContent = message; el.className = 'alert alert-success'; el.hidden = false; }
    }
    function dateBounds() {
        const from = document.getElementById('correction-from'), to = document.getElementById('correction-to');
        if (!from || !to) return;
        from.min = '2000-01-01'; from.max = to.value || '2100-12-31';
        to.min = from.value || '2000-01-01'; to.max = '2100-12-31';
    }
    function ensureModal() {
        if (modal?.isConnected) return true;
        if (!window.bootstrap?.Modal) { liveStatus('The review dialog could not start. Reload this page.'); return false; }
        modal = document.createElement('div');
        modal.id = 'correction-workflow-modal'; modal.className = 'modal fade attendance-page';
        modal.tabIndex = -1; modal.setAttribute('aria-labelledby', 'correction-workflow-title'); modal.setAttribute('aria-hidden', 'true');
        modal.innerHTML = '<div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable"><div class="modal-content"></div></div>';
        // Keep the dialog outside the main area patched by the shared filter script.
        document.body.append(modal); instance = window.bootstrap.Modal.getOrCreateInstance(modal);
        modal.addEventListener('hide.bs.modal', event => {
            if (posting) { event.preventDefault(); return; }
            if (dirty && !closingAfterSave && !window.confirm('Discard your unsaved remarks and close this review?')) event.preventDefault();
        });
        modal.addEventListener('hidden.bs.modal', () => {
            detailAbort?.abort(); detailSequence++; detailBusy = false;
            modal.querySelector('.modal-content').replaceChildren(); dirty = false;
            if (trigger?.isConnected) trigger.focus();
            if (closingAfterSave) { closingAfterSave = false; refreshResults(true); }
        });
        return true;
    }
    function loadingContent() {
        const content = modal.querySelector('.modal-content');
        content.innerHTML = '<div class="modal-header"><h2 class="modal-title h5" id="correction-workflow-title">Correction Request</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div><div class="modal-body"><p role="status">Loading latest request details…</p></div><div class="modal-footer"><button type="button" class="outline-button" data-bs-dismiss="modal">Close</button></div>';
    }
    function modalError(message) {
        let el = modal.querySelector('[data-correction-modal-error]');
        if (!el) { el = document.createElement('div'); el.className = 'alert alert-danger'; el.dataset.correctionModalError = ''; el.setAttribute('role', 'alert'); modal.querySelector('.modal-body').prepend(el); }
        el.textContent = message; el.hidden = false;
    }
    async function loadDetails(url, button) {
        if (posting || !ensureModal()) return;
        if (dirty && !window.confirm('Reload the latest details and discard your unsaved remarks?')) return;
        const target = new URL(url, location.href); if (target.origin !== location.origin) return;
        detailAbort?.abort(); const controller = new AbortController(); detailAbort = controller;
        const sequence = ++detailSequence; detailBusy = true; dirty = false;
        if (button) trigger = button;
        loadingContent(); instance.show(button || trigger);
        const timeout = setTimeout(() => controller.abort(), 12000);
        try {
            const response = await fetch(target, { credentials: 'same-origin', cache: 'no-store', signal: controller.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html')) throw new Error('This request is unavailable or your sign-in expired. Close the dialog and refresh the list.');
            const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
            if (!doc.querySelector('.modal-header') || !doc.querySelector('.modal-footer') || doc.querySelector('script')) throw new Error('The latest details could not be loaded. Close the dialog and retry.');
            if (stopped || sequence !== detailSequence) return;
            modal.querySelector('.modal-content').replaceChildren(...Array.from(doc.body.childNodes));
        } catch (error) {
            if (stopped || sequence !== detailSequence) return;
            modal.querySelector('.modal-body').replaceChildren();
            modalError(error.name === 'AbortError' ? 'Loading timed out. Close the dialog and retry.' : error.message);
        } finally { clearTimeout(timeout); if (sequence === detailSequence) detailBusy = false; }
    }
    function reviewMode(form, decision) {
        if (!form?.matches('[data-correction-review]')) return;
        const required = decision === 'reject' || form.dataset.approvalOptional !== 'true';
        const field = form.querySelector('[name=reviewRemark]'); field.required = required; field.setCustomValidity('');
        form.querySelector('[data-review-requirement]').textContent = required ? '(Required)' : '(Optional for approval)';
        form.querySelector('[data-review-help]').textContent = decision === 'reject' ? 'Explain why the request is rejected. Visible to the applicant.' : required ? 'Explain the basis for approving this correction.' : 'Tutor confirmation is recorded. Additional remarks are optional.';
    }
    async function refreshResults(force = false) {
        const region = document.querySelector('[data-correction-results]');
        if (!region || stopped || resultsBusy || document.hidden || posting || detailBusy || modal?.classList.contains('show')) return;
        if (!force && (region.closest('main')?.getAttribute('aria-busy') === 'true' || Date.now() - lastFilterInput < 1200)) return;
        resultsBusy = true; const controller = new AbortController(); resultsAbort = controller;
        const timeout = setTimeout(() => controller.abort(), 12000);
        const target = new URL(location.href); target.searchParams.set('fragment', 'true');
        const atStart = location.href;
        try {
            const response = await fetch(target, { credentials: 'same-origin', cache: 'no-store', signal: controller.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.redirected) throw new Error();
            const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
            const next = doc.querySelector('[data-correction-results]');
            if (!next || doc.querySelector('script')) throw new Error();
            if (stopped || location.href !== atStart || posting || detailBusy || modal?.classList.contains('show') || region.closest('main')?.getAttribute('aria-busy') === 'true') return;
            const signature = next.innerHTML;
            if (force || signature !== lastSignature) {
                const page = region.querySelector('.table-pagination [aria-current=page]')?.textContent?.trim();
                const scroll = region.querySelector('.table-responsive')?.scrollLeft || 0;
                region.replaceWith(next); window.EduSmartTables?.enhance(next);
                if (page) Array.from(next.querySelectorAll('.table-pagination button')).find(b => b.textContent.trim() === page)?.click();
                const table = next.querySelector('.table-responsive'); if (table) table.scrollLeft = scroll;
                lastSignature = signature;
            }
            liveStatus('Requests update automatically while this page is visible. Updates pause while a review is open.');
        } catch { if (!stopped) liveStatus('Updates could not be checked. Your current list is retained; check your connection or sign-in.'); }
        finally { clearTimeout(timeout); resultsBusy = false; }
    }
    async function submitWorkflow(form, submitter) {
        const prompt = form.dataset.confirm || submitter?.dataset.confirm;
        if (prompt && !window.confirm(prompt)) return;
        const data = new FormData(form); if (submitter?.name) data.set(submitter.name, submitter.value);
        const target = new URL(form.action, location.href); if (target.origin !== location.origin) return;
        posting = true;
        const controls = Array.from(modal.querySelectorAll('button,input,select,textarea')).map(el => [el, el.disabled]);
        controls.forEach(([el]) => el.disabled = true);
        const controller = new AbortController(), timeout = setTimeout(() => controller.abort(), 20000);
        try {
            const response = await fetch(target, { method: 'POST', body: data, credentials: 'same-origin', cache: 'no-store', signal: controller.signal, headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' } });
            if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('The result could not be confirmed. Close and reopen the request details and check the request before retrying.');
            const result = await response.json();
            if (!response.ok || result.ok !== true) { modalError(result.message || 'The action could not be completed. Close and reopen the request details and retry.'); return; }
            feedback(result.message); dirty = false; closingAfterSave = true; posting = false;
            instance.hide(); document.dispatchEvent(new CustomEvent('edusmart:unread-changed'));
        } catch (error) { modalError(error.name === 'AbortError' ? 'The request timed out. Close and reopen the request details to check whether it was saved before retrying.' : error.message); }
        finally { clearTimeout(timeout); controls.forEach(([el, disabled]) => el.disabled = disabled); posting = false; }
    }
    document.addEventListener('click', event => {
        const open = event.target.closest('[data-correction-open]');
        if (open) { event.preventDefault(); loadDetails(open.dataset.correctionUrl, open); return; }
        const decision = event.target.closest('button[name=decision]');
        if (decision && modal?.contains(decision)) reviewMode(decision.form, decision.value);
    }, true);
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.matches('.correction-workflow-form') || !modal?.contains(form)) return;
        event.preventDefault(); event.stopImmediatePropagation(); if (posting || detailBusy) return;
        reviewMode(form, event.submitter?.value || 'approve');
        for (const field of form.querySelectorAll('textarea')) field.setCustomValidity(field.required && !field.value.trim() ? 'Please enter remarks.' : '');
        if (!form.reportValidity()) return;
        submitWorkflow(form, event.submitter);
    }, true);
    document.addEventListener('input', event => {
        if (modal?.contains(event.target) && event.target.matches('textarea,select')) { dirty = true; event.target.setCustomValidity?.(''); }
        if (event.target.closest('form[data-live-filter]')) lastFilterInput = Date.now();
    });
    document.addEventListener('change', event => {
        if (event.target.matches('#correction-from,#correction-to')) dateBounds();
        if (modal?.contains(event.target) && event.target.matches('select')) dirty = true;
    });
    document.addEventListener('edusmart:filters-updated', () => { lastSignature = null; dateBounds(); });
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refreshResults(); });
    window.addEventListener('focus', () => refreshResults());
    const timer = setInterval(refreshResults, 8000);
    window.addEventListener('pagehide', () => { stopped = true; clearInterval(timer); resultsAbort?.abort(); detailAbort?.abort(); });
    dateBounds();
});

(() => {
    function init() {
        document.querySelectorAll('[data-grade-form]').forEach(form => {
            const marks = form.querySelector('[name="Form.Marks"]'), feedback = form.querySelector('[name="Form.Feedback"]'), save = form.querySelector('[data-grade-save]');
            if (!marks || !feedback || !save) return;
            const normalizedFeedback = value => (value || '').replace(/\r\n?/g, '\n').trim();
            const original = form.dataset.originalMarks;
            function changed() {
                const value = marks.value.trim();
                return value !== '' && Number.isFinite(Number(value)) &&
                    (original === '' || Number(value) !== Number(original) || normalizedFeedback(feedback.value) !== normalizedFeedback(form.dataset.originalFeedback));
            }
            function update() { save.disabled = !changed(); }
            form.addEventListener('input', update); form.addEventListener('change', update);
            form.addEventListener('submit', event => { if (!changed()) { event.preventDefault(); event.stopImmediatePropagation(); } }, true);
            form.addEventListener('reset', () => setTimeout(update, 0));
            window.addEventListener('pageshow', update); update();
        });

        document.querySelectorAll('[data-assessment-upload]').forEach(zone => {
            const input = zone.querySelector('input[type=file]'), list = zone.querySelector('[data-assessment-file-list]'), drop = zone.querySelector('[data-assessment-drop]'), error = zone.querySelector('[data-assessment-upload-error]');
            if (!input || !list || !drop || !error) return;
            let files = [];
            let saved = Array.from(list.querySelectorAll('[data-saved-answer]'));
            const savedSize = () => saved.reduce((sum, row) => sum + Number(row.dataset.fileSize || 0), 0);
            saved.forEach(row => row.querySelector('[data-remove-saved-answer]')?.addEventListener('click', () => {
                saved = saved.filter(item => item !== row); error.textContent = ''; sync();
            }));
            const previews = new Map();
            function previewType(file) {
                if (/\.pdf$/i.test(file.name) && (!file.type || file.type === 'application/pdf')) return 'application/pdf';
                if (/\.png$/i.test(file.name) && (!file.type || file.type === 'image/png')) return 'image/png';
                if (/\.jpe?g$/i.test(file.name) && (!file.type || file.type === 'image/jpeg')) return 'image/jpeg';
                return null;
            }
            window.addEventListener('pagehide', () => { previews.forEach(url => URL.revokeObjectURL(url)); previews.clear(); });
            const key = f => `${f.name}\u0000${f.size}\u0000${f.lastModified}`;
            function sync() {
                const total = zone.querySelector('[data-assessment-upload-total]');
                if (total) total.textContent = `${files.length + saved.length} ${files.length + saved.length === 1 ? 'file' : 'files'} · ${((savedSize() + files.reduce((sum, f) => sum + f.size, 0)) / 1048576).toFixed(2)} MB`;
                const retained = new Set(files);
                previews.forEach((url, file) => { if (!retained.has(file)) { URL.revokeObjectURL(url); previews.delete(file); } });
                const dt = new DataTransfer(); files.forEach(f => dt.items.add(f)); input.files = dt.files; list.replaceChildren(...saved); drop.hidden = files.length + saved.length > 0; files.forEach((file, index) => {
                    const row = document.createElement('div'); row.className = 'assessment-file-row assessment-upload-card'; const name = document.createElement('span'); name.textContent = file.name; const size = document.createElement('small'); size.textContent = `${(file.size / 1048576).toFixed(2)} MB · New`; name.append(size);
                    const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'outline-button'; remove.textContent = '×'; remove.setAttribute('aria-label', `Remove ${file.name}`); remove.addEventListener('click', () => { files.splice(index, 1); error.textContent = ''; sync(); }); const actions = document.createElement('div'); actions.className = 'material-actions';
                    const type = previewType(file);
                    if (type && window.HTMLDialogElement && typeof URL.createObjectURL === 'function') {
                        if (!previews.has(file)) previews.set(file, URL.createObjectURL(new Blob([file], { type })));
                        const preview = document.createElement('a'); preview.href = previews.get(file); preview.className = 'outline-button'; preview.textContent = 'Preview';
                        preview.setAttribute('data-material-preview', ''); preview.dataset.previewName = file.name; preview.dataset.previewType = type;
                        preview.setAttribute('aria-label', `Preview ${file.name}`); actions.append(preview);
                    }
                    const icon = document.createElement('i'); icon.className = 'bi bi-file-earmark-text assessment-upload-icon'; icon.setAttribute('aria-hidden', 'true');
                    if (type?.startsWith('image/') && previews.has(file)) {
                        const thumb = document.createElement('img'); thumb.src = previews.get(file); thumb.alt = ''; thumb.className = 'assessment-upload-thumb'; row.append(thumb);
                    } else row.append(icon);
                    actions.append(remove); row.append(name, actions); list.append(row);
                });
            }
            function add(incoming) {
                const additions = []; let skipped = 0; const seen = new Set(files.map(key)); for (const file of incoming) { if (seen.has(key(file)) || saved.some(row => row.dataset.fileName === file.name && Number(row.dataset.fileSize) === file.size)) { skipped++; continue; } seen.add(key(file)); additions.push(file); }
                const all = [...files, ...additions];
                if (all.some(f => !f.size || f.size > 10485760)) { error.textContent = 'Each file must contain data and be at most 10 MB.'; sync(); return; }
                if (all.some(f => ! /\.(pdf|docx|pptx|xlsx|png|jpe?g)$/i.test(f.name))) { error.textContent = 'Use PDF, DOCX, PPTX, XLSX, PNG, JPG or JPEG.'; sync(); return; }
                if (all.length + saved.length > 10 || savedSize() + all.reduce((sum, f) => sum + f.size, 0) > 31457280) { error.textContent = 'Use up to 10 files and 30 MB total.'; sync(); return; }
                files = all; error.textContent = skipped ? 'Duplicate selections were skipped.' : ''; sync();
            }
            input.addEventListener('click', () => { input.value = ''; });
            input.addEventListener('change', () => add(Array.from(input.files)));
            input.addEventListener('cancel', sync);
            zone.addEventListener('dragover', e => { if (e.dataTransfer?.types.includes('Files')) { e.preventDefault(); drop.classList.add('is-dragging'); } });
            zone.addEventListener('dragleave', () => drop.classList.remove('is-dragging'));
            zone.addEventListener('drop', e => { if (e.dataTransfer?.files.length) { e.preventDefault(); drop.classList.remove('is-dragging'); add(Array.from(e.dataTransfer.files)); } });
            const form = zone.closest('form');
            form?.addEventListener('submit', e => { sync(); if (form.dataset.assessmentRequiresFile === 'true' && files.length + saved.length === 0) { e.preventDefault(); e.stopImmediatePropagation(); error.textContent = 'Please attach at least one answer file.'; input.focus(); } else if (error.textContent && !error.textContent.startsWith('Duplicate')) { error.textContent = ''; } }, true);
            sync();
        });
        const classes = document.querySelector('select[name=ClassSessionId]');
        if (classes) {
            setInterval(async () => {
                if (document.hidden || document.activeElement === classes) return; try {
                    const r = await fetch(classes.closest('form').dataset.classChoicesUrl, { credentials: 'same-origin', cache: 'no-store' }); if (!r.ok || r.redirected) return; const data = await r.json(); if (!Array.isArray(data.classes)) return;
                    const selected = classes.value; const next = document.createElement('select'); const empty = new Option('Select assigned class', ''); next.append(empty); data.classes.forEach(c => next.append(new Option(c.text, c.id)));
                    if (next.innerHTML !== classes.innerHTML) { classes.replaceChildren(...next.children); classes.value = selected; if (classes.value !== selected) classes.value = ''; }
                } catch {/* Keep form contents and chosen files when an update is unavailable. */ }
            }, 10000);
        }
        let submissions = document.querySelector('[data-submissions-live]');
        if (submissions) {
            let pending = false, stopped = false;
            const signature = node => { const copy = node.cloneNode(true); copy.querySelectorAll('input[name=__RequestVerificationToken]').forEach(x => x.removeAttribute('value')); copy.querySelectorAll('[data-table-pagination]').forEach(x => x.remove()); return copy.innerHTML; };
            let previous = null;
            async function refreshSubmissions() {
                if (pending || stopped || document.hidden || document.querySelector('dialog[open]') || (submissions.contains(document.activeElement) && document.activeElement?.matches('input,textarea,select'))) return;
                pending = true; const abort = new AbortController(); const timeout = setTimeout(() => abort.abort(), 12000);
                try {
                    const response = await fetch(submissions.dataset.refreshUrl, { credentials: 'same-origin', cache: 'no-store', signal: abort.signal });
                    if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html')) return;
                    const doc = new DOMParser().parseFromString(await response.text(), 'text/html'), next = doc.querySelector('[data-submissions-live]');
                    if (!next || doc.querySelector('script') || stopped) return;
                    const value = signature(next);
                    if (value !== previous) {
                        const scroll = submissions.querySelector('.table-responsive')?.scrollLeft || 0;
                        const activePage = submissions.querySelector('.table-pagination [aria-current=page]')?.textContent?.trim();
                        submissions.replaceWith(next); submissions = next; previous = value;
                        window.EduSmartTables?.enhance(submissions);
                        if (activePage) Array.from(submissions.querySelectorAll('.table-pagination button')).find(x => x.textContent.trim() === activePage)?.click();
                        const table = submissions.querySelector('.table-responsive'); if (table) table.scrollLeft = scroll;
                    }
                } catch { /* Keep the current roster when the connection is unavailable. */ }
                finally { clearTimeout(timeout); pending = false; }
            }
            const rosterTimer = setInterval(refreshSubmissions, 10000);
            window.addEventListener('focus', refreshSubmissions);
            document.addEventListener('visibilitychange', () => { if (!document.hidden) refreshSubmissions(); });
            window.addEventListener('pagehide', () => { stopped = true; clearInterval(rosterTimer); });
        }
        // Feedback opens outside the table flow, keeping row heights and column widths stable.
        let feedbackDialog, feedbackBody, feedbackTrigger;
        document.addEventListener('click', event => {
            const button = event.target.closest('[data-progress-feedback]');
            if (!button) return;
            const template = button.parentElement.querySelector('template[data-progress-feedback-content]');
            if (!template) return;
            if (!feedbackDialog) {
                feedbackDialog = document.createElement('dialog');
                feedbackDialog.className = 'assessment-feedback-dialog';
                feedbackDialog.setAttribute('aria-labelledby', 'assessment-feedback-title');
                const header = document.createElement('div'); header.className = 'progress-feedback-heading';
                const title = document.createElement('h2'); title.id = 'assessment-feedback-title'; title.textContent = 'Tutor Feedback';
                const close = document.createElement('button'); close.type = 'button'; close.className = 'outline-button'; close.textContent = 'Close';
                close.addEventListener('click', () => feedbackDialog.close());
                header.append(title, close);
                feedbackBody = document.createElement('div'); feedbackBody.className = 'progress-feedback-body';
                feedbackDialog.append(header, feedbackBody); document.body.append(feedbackDialog);
                feedbackDialog.addEventListener('close', () => { if (feedbackTrigger?.isConnected) feedbackTrigger.focus(); });
                feedbackDialog.addEventListener('click', e => {
                    const bounds = feedbackDialog.getBoundingClientRect();
                    if (e.target === feedbackDialog && (e.clientX < bounds.left || e.clientX > bounds.right || e.clientY < bounds.top || e.clientY > bounds.bottom)) feedbackDialog.close();
                });
            }
            feedbackTrigger = button;
            feedbackBody.replaceChildren(template.content.cloneNode(true));
            if (!feedbackDialog.open) feedbackDialog.showModal();
        });
        let results = document.querySelector('[data-assessment-results]'); const filters = document.querySelector('[data-assessment-filter]'), status = document.querySelector('[data-assessment-refresh]');
        if (!results || !filters) return;
        let current = new URL(location.href), sequence = 0, abort, busy = false, searchTimer, saving = false, feedbackTimer;
        function fingerprint(el) { const copy = el.cloneNode(true); copy.querySelectorAll('input[name=__RequestVerificationToken]').forEach(x => x.removeAttribute('value')); return copy.outerHTML; }
        let previous = fingerprint(results);
        function dropdowns(dispose = false) { results.querySelectorAll('[data-bs-toggle=dropdown]').forEach(button => { const Dropdown = window.bootstrap?.Dropdown; if (!Dropdown) return; if (dispose) Dropdown.getInstance(button)?.dispose(); else Dropdown.getOrCreateInstance(button, { popperConfig: c => ({ ...c, strategy: 'fixed' }) }); }); }
        async function update(url = current, navigate = false, push = true) {
            if (saving) return;
            if (document.querySelector('.assessment-feedback-dialog[open]')) return;
            if (!navigate && (busy || document.hidden || results.querySelector('.dropdown-menu.show') || (results.contains(document.activeElement) && document.activeElement?.matches('input,textarea,select')) || results.querySelector('details[open]'))) return;
            const nextUrl = new URL(url, location.href); if (nextUrl.origin !== location.origin) return;
            abort?.abort(); abort = new AbortController(); const controller = abort; const request = ++sequence; busy = true; const timeout = setTimeout(() => controller.abort(), 12000); nextUrl.searchParams.set('fragment', 'true');
            try {
                const response = await fetch(nextUrl, { credentials: 'same-origin', cache: 'no-store', signal: controller.signal, headers: { Accept: 'text/html' } }); if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html')) throw new Error();
                const parsed = new DOMParser().parseFromString(await response.text(), 'text/html'), next = parsed.querySelector('[data-assessment-results]'); if (!next || parsed.querySelector('script')) throw new Error(); if (request !== sequence || document.querySelector('.assessment-feedback-dialog[open]')) return;
                const signature = fingerprint(next); if (navigate || signature !== previous) { dropdowns(true); results.replaceWith(next); results = next; previous = signature; dropdowns(); }
                nextUrl.searchParams.delete('fragment'); current = nextUrl;
                if (navigate) {
                    if (push) history.pushState(null, '', current);
                    ['Search', 'ClassId', 'StudentId', 'SubjectId', 'Kind', 'Month', 'Status', 'Sort'].forEach(name => { const field = filters.elements?.namedItem(name); if (field) field.value = current.searchParams.get(name) || (name === 'Sort' ? 'due_asc' : name === 'Status' ? 'active' : ''); });
                }
                if (status) status.textContent = 'Updates automatically while visible.';
            } catch { if (request === sequence && status) status.textContent = 'Automatic update unavailable. Try View again; your selections are preserved.'; }
            finally { clearTimeout(timeout); if (request === sequence) busy = false; }
        }
        document.addEventListener('submit', async event => {
            const form = event.target;
            if (!form.matches('[data-assessment-state]')) return;
            event.preventDefault(); event.stopImmediatePropagation();
            if (saving || !window.confirm(form.dataset.confirm)) return;
            const body = new FormData(form);
            if (event.submitter?.name) body.set(event.submitter.name, event.submitter.value);
            const url = new URL(form.action, location.href); url.searchParams.set('json', 'true');
            saving = true; abort?.abort(); ++sequence;
            const button = event.submitter; if (button) button.disabled = true;
            const notice = document.querySelector('[data-assessment-feedback]');
            clearTimeout(feedbackTimer);
            const postAbort = new AbortController(); const postTimeout = setTimeout(() => postAbort.abort(), 20000);
            try {
                const response = await fetch(url, { method: 'POST', body, signal: postAbort.signal, credentials: 'same-origin', headers: { Accept: 'application/json' } });
                if (response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error();
                const result = await response.json(); if (typeof result.message !== 'string') throw new Error();
                if (notice) { clearTimeout(feedbackTimer); notice.hidden = false; notice.textContent = result.message; notice.className = 'alert ' + (response.ok && result.ok ? 'alert-success' : 'alert-warning'); if (response.ok && result.ok) feedbackTimer = setTimeout(() => notice.hidden = true, 5000); }
            } catch { if (notice) { notice.hidden = false; notice.className = 'alert alert-warning'; notice.textContent = 'Could not confirm the change. Check the latest status before trying again.'; } }
            finally { clearTimeout(postTimeout); saving = false; if (button) button.disabled = false; await update(current, true, false); }
        }, true);
        // The results toolbar's select belongs to the filter form via its form attribute.
        document.addEventListener('change', event => {
            if (event.target.matches('[data-progress-sort]') && event.target.form === filters) {
                clearTimeout(searchTimer); update(filterUrl(), true);
            }
        });
        function filterUrl() { const url = new URL(filters.action, location.href); for (const [key, value] of new FormData(filters)) url.searchParams.set(key, value); url.searchParams.set('Page', '1'); return url; }
        filters.addEventListener('submit', e => { e.preventDefault(); clearTimeout(searchTimer); update(filterUrl(), true); });
        filters.addEventListener('change', () => { clearTimeout(searchTimer); update(filterUrl(), true); });
        filters.addEventListener('input', e => { if (e.target.name === 'Search') { clearTimeout(searchTimer); searchTimer = setTimeout(() => update(filterUrl(), true), 450); } });
        document.addEventListener('click', e => { const link = e.target.closest('a[data-assessment-nav]'); if (link && !e.ctrlKey && !e.metaKey && !e.shiftKey && !e.altKey && e.button === 0) { e.preventDefault(); update(link.href, true); } });
        window.addEventListener('popstate', () => update(location.href, true, false));
        document.addEventListener('visibilitychange', () => { if (document.hidden) { abort?.abort(); } else update(); });
        window.addEventListener('focus', () => update()); const timer = setInterval(() => update(), 10000); window.addEventListener('pagehide', () => { clearInterval(timer); clearTimeout(searchTimer); abort?.abort(); }); dropdowns();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();

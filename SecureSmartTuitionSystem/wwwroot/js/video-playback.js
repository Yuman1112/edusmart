(() => {
    'use strict';
    const one = (s, root = document) => root.querySelector(s);
    const headers = { 'X-Requested-With': 'XMLHttpRequest' };
    document.querySelectorAll('[data-video-notice]').forEach(n => setTimeout(() => n.remove(), 5000));
    const form = one('[data-video-form]');
    if (form) {
        const file = one('[name="VideoFile"]', form), save = one('[data-video-save]', form);
        const preview = one('[data-video-local]', form), drop = one('[data-video-drop]', form);
        const classes = one('[data-video-classes]', form);
        const original = ['Title', 'Description', 'RecordingDate'].map(k => (form.elements[k]?.value || '').trim());
        let busy = false, blob = '', choices = [];
        function error(key, message) {
            const span = one(`[data-valmsg-for="${key}"]`, form);
            if (span) { span.textContent = message; span.classList.toggle('field-validation-error', !!message); }
            const input = form.elements[key]; if (input) input.classList.toggle('input-validation-error', !!message);
        }
        function summary(message) { const node = one('[data-video-form-error]', form); if (node) { node.textContent = message; node.hidden = !message; } const footer = one('[data-video-submit-error]', form); if (footer) { footer.textContent = message; footer.hidden = !message; } }
        function dirty() { save.disabled = busy || (typeof started !== 'undefined' && started && queue.some(x => x.state === 'unknown')) || (form.dataset.editing === 'true' && original.every((v, i) => v === form.elements[['Title', 'Description', 'RecordingDate'][i]].value.trim())); }
        form.addEventListener('input', dirty);
        const queue = [], list = one('[data-video-queue]', form);
        const batchStatus = one('[data-video-batch-status]', form);
        let started = false, previewItem = null; dirty();
        const previewPanel = one('[data-video-preview-panel]', form);
        function closePreview() {
            if (!preview) return;
            preview.pause(); preview.removeAttribute('src'); preview.load(); preview.hidden = true;
            if (previewPanel?.open) previewPanel.close();
            if (blob) URL.revokeObjectURL(blob);
            blob = ''; previewItem = null;
        }
        one('[data-video-preview-close]', form)?.addEventListener('click', closePreview);
        previewPanel?.addEventListener('cancel', event => { event.preventDefault(); closePreview(); });
        function renderQueue() {
            if (!list) return;
            list.replaceChildren();
            drop.hidden = started;
            queue.forEach((item, index) => {
                const row = document.createElement('div'); row.className = 'video-batch-row';
                const info = document.createElement('div'); info.className = 'video-batch-info';
                const label = document.createElement('label'); label.textContent = 'Lesson title'; label.htmlFor = `video-title-${index}`;
                const title = document.createElement('input'); title.id = label.htmlFor; title.className = 'form-control'; title.maxLength = 150; title.value = item.title; title.disabled = busy || item.state === 'saved' || item.state === 'unknown';
                title.addEventListener('input', () => { item.title = title.value; });
                const name = document.createElement('small'); name.textContent = `MP4 · ${(item.file.size / 1048576).toFixed(2)} MB`; name.title = `Original file: ${item.file.name}`;
                const status = document.createElement('span'); status.className = `video-batch-state is-${item.state}`; status.textContent = item.message;
                info.append(label, title, name, status);
                const actions = document.createElement('div'); actions.className = 'video-batch-actions';
                const view = document.createElement('button'); view.type = 'button'; view.className = 'outline-button'; view.textContent = 'Preview'; view.disabled = busy;
                view.addEventListener('click', () => { closePreview(); previewItem = item; blob = URL.createObjectURL(item.file); preview.src = blob; preview.hidden = false; const heading = one('[data-video-preview-title]', form); if (heading) heading.textContent = item.title.trim() || 'Video Preview'; previewPanel?.showModal(); });
                actions.append(view);
                if (item.state !== 'saved' && item.state !== 'unknown') {
                    const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'outline-button'; remove.textContent = '×'; remove.setAttribute('aria-label', `Remove ${item.file.name}`); remove.disabled = busy;
                    remove.addEventListener('click', () => { if (previewItem === item) closePreview(); queue.splice(index, 1); renderQueue(); }); actions.append(remove);
                }
                row.append(info, actions); list.append(row);
            });
        }
        function addFiles(files) {
            if (busy || started) return;
            const messages = [];
            for (const f of files) {
                if (!/\.mp4$/i.test(f.name) || !f.size || f.size > 209715200) { messages.push(`${f.name}: choose an MP4 of at most 200 MB.`); continue; }
                if (queue.some(x => x.file.name === f.name && x.file.size === f.size && x.file.lastModified === f.lastModified)) { messages.push(`${f.name} is already selected.`); continue; }
                if (queue.length >= 5) { messages.push('You can select up to 5 videos.'); break; }
                queue.push({ file: f, title: f.name.replace(/\.mp4$/i, '').slice(0, 150), state: 'ready', message: 'Ready to upload' });
            }
            error('VideoFile', messages.join(' ')); file.value = ''; renderQueue();
        }
        if (file) {
            file.addEventListener('change', () => addFiles(Array.from(file.files)));
            ['dragenter', 'dragover'].forEach(e => drop.addEventListener(e, event => { event.preventDefault(); if (!started) drop.classList.add('is-dragging'); }));
            ['dragleave', 'drop'].forEach(e => drop.addEventListener(e, event => { event.preventDefault(); drop.classList.remove('is-dragging'); }));
            drop.addEventListener('drop', event => addFiles(Array.from(event.dataTransfer.files)));
        }
        window.addEventListener('beforeunload', e => { if (busy) { e.preventDefault(); e.returnValue = ''; } });
        async function refreshChoices() {
            if (document.hidden || busy || form.dataset.editing === 'true' || document.activeElement === classes) return;
            try {
                const response = await fetch(form.dataset.choicesUrl, { headers, cache: 'no-store', signal: AbortSignal.timeout(12000) });
                if (!response.ok) return;
                choices = (await response.json()).classes;
                const empty = one('[data-video-no-classes]', form); if (empty) empty.hidden = choices.length > 0;
                const selected = classes.value;
                const next = JSON.stringify(choices);
                if (classes.dataset.version !== next) {
                    classes.replaceChildren(new Option('Select assigned class', ''));
                    choices.forEach(c => classes.add(new Option(c.text, String(c.id))));
                    classes.value = selected;
                    if (selected && !choices.some(c => String(c.id) === selected)) error('ClassSessionId', 'This class is no longer available. Choose another assigned class.');
                    classes.dataset.version = next;
                }
                one('#video-subject-label', form).value = choices.find(c => String(c.id) === classes.value)?.subject || '';
            } catch { /* Preserve the current form and selected file during a connection problem. */ }
        }
        classes.addEventListener('change', () => { error('ClassSessionId', ''); one('#video-subject-label', form).value = choices.find(c => String(c.id) === classes.value)?.subject || ''; });
        refreshChoices(); setInterval(refreshChoices, 15000);
        function send(data, progress) {
            return new Promise(resolve => {
                const request = new XMLHttpRequest(); request.open('POST', form.action); request.timeout = 600000;
                request.setRequestHeader('Accept', 'application/json'); request.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
                request.upload.onprogress = e => { if (progress && e.lengthComputable) { one('progress', progress).value = e.loaded / e.total * 100; one('span', progress).textContent = e.loaded === e.total ? 'Checking and saving the video…' : `Uploading… ${Math.round(e.loaded / e.total * 100)}%`; } };
                request.onload = () => {
                    let result; try { result = JSON.parse(request.responseText); } catch { result = null; }
                    if (request.status === 200 && result?.url) { resolve({ ok: true, url: result.url }); return; }
                    const messages = result?.errors ? Object.values(result.errors).flat().join(' ') : request.status === 413 ? 'The server rejected the upload size. Contact your administrator.' : 'Could not confirm the upload. Check Recorded Lessons before uploading this video again.';
                    resolve({ ok: false, unknown: !result?.errors && request.status !== 413, message: messages, errors: result?.errors });
                };
                request.onerror = request.ontimeout = () => resolve({ ok: false, unknown: true, message: 'Connection interrupted. Check Recorded Lessons before uploading this video again.' });
                request.send(data);
            });
        }
        async function submitVideo(event) {
            event.preventDefault(); if (busy) return;
            try {
                const editing = form.dataset.editing === 'true';
                let valid = true; summary(''); error('VideoFile', '');
                const keys = [['ClassSessionId', 'Please select an assigned class.'], ['RecordingDate', 'Please choose the recording date.']];
                if (editing) keys.push(['Title', 'Please enter a lesson title.']);
                keys.forEach(([key, message]) => { const missing = !form.elements[key].value.trim(); error(key, missing ? message : ''); valid = valid && !missing; });
                const pending = queue.filter(x => x.state === 'ready' || x.state === 'failed');
                if (!editing && (!pending.length || pending.some(x => !x.title.trim()))) { error('VideoFile', pending.length ? 'Enter a lesson title for every video.' : 'Add at least one video to upload.'); valid = false; }
                for (const key of ['RecordingDate', 'Description', ...(editing ? ['Title'] : [])]) {
                    const input = form.elements[key];
                    if (input && !input.disabled && input.validity && !input.validity.valid) { error(key, input.validationMessage || 'Check this value.'); valid = false; }
                }
                if (!valid) { summary('Please check the highlighted fields and video titles before uploading.'); one('.input-validation-error', form)?.focus(); return; }
                closePreview();
                busy = true; dirty();
                const progress = one('[data-video-upload-progress]', form);
                if (progress) { progress.hidden = false; one('progress', progress).value = 0; }
                if (editing) {
                    const result = await send(new FormData(form), progress);
                    if (result.ok) { const url = new URL(result.url, location.href); if (url.origin === location.origin) { location.assign(url.href); return; } }
                    if (result.errors) Object.entries(result.errors).forEach(([key, messages]) => key ? error(key, messages.join(' ')) : summary(messages.join(' '))); else summary(result.message);
                } else {
                    started = true;
                    // Keep shared values stable and send one file per request, within the existing server limit.
                    const shared = new FormData(form); shared.delete('VideoFile');
                    ['ClassSessionId', 'RecordingDate', 'Description'].forEach(key => shared.set(key, form.elements[key].value));
                    form.querySelectorAll('select, textarea, input:not([type="hidden"])').forEach(x => { x.disabled = true; });
                    for (const item of pending) {
                        item.state = 'uploading'; item.message = 'Uploading…'; renderQueue();
                        batchStatus.textContent = `Uploading ${pending.indexOf(item) + 1} of ${pending.length}: ${item.file.name}`;
                        one('progress', progress).value = 0;
                        const data = new FormData(); shared.forEach((value, key) => data.append(key, value)); data.set('Title', item.title.trim()); data.set('VideoFile', item.file);
                        const result = await send(data, progress);
                        item.state = result.ok ? 'saved' : result.unknown ? 'unknown' : 'failed';
                        item.message = result.ok ? 'Uploaded successfully' : result.message;
                        if (result.unknown) { summary('An upload could not be confirmed. Remaining videos have not been uploaded. Check Recorded Lessons before continuing.'); break; }
                    }
                    const saved = queue.filter(x => x.state === 'saved').length;
                    batchStatus.textContent = `${saved} of ${queue.length} videos uploaded. Each uploaded video is saved separately.`;
                    one('[data-video-done]', form).hidden = false;
                }
                busy = false; if (progress) progress.hidden = true; renderQueue(); dirty();
                if (!editing) { save.textContent = 'Retry Remaining Videos'; save.disabled = queue.some(x => x.state === 'unknown') || !queue.some(x => x.state === 'ready' || x.state === 'failed'); }
            } catch (exception) {
                console.error('Video upload could not continue.', exception);
                summary('The uploader could not continue. Your selected files are still here. Check Recorded Lessons before retrying if an upload had started.');
                if (busy) queue.filter(x => x.state === 'uploading').forEach(x => { x.state = 'unknown'; x.message = 'Upload status could not be confirmed. Check Recorded Lessons.'; });
                busy = false;
                const progress = one('[data-video-upload-progress]', form); if (progress) progress.hidden = true;
                renderQueue(); dirty();
            }
        }
        form.addEventListener('submit', submitVideo);
        if (form.dataset.editing !== 'true') save.addEventListener('click', submitVideo);
    }
    const filter = one('[data-video-filter]');
    if (filter) {
        let loading = false, controller, serial = 0, timer;
        function dropdowns() { document.querySelectorAll('[data-video-results] [data-bs-toggle="dropdown"]').forEach(button => window.bootstrap?.Dropdown.getOrCreateInstance(button, { popperConfig: c => ({ ...c, strategy: 'fixed' }) })); }
        dropdowns();
        const feedback = one('[data-video-feedback]');
        function message(text, success) { feedback.textContent = text; feedback.className = `alert ${success ? 'alert-success' : 'alert-warning'}`; feedback.hidden = false; if (success) setTimeout(() => { if (feedback.textContent === text) feedback.hidden = true; }, 5000); }
        async function update(url, navigate = false, writeHistory = true) {
            if (!navigate && (loading || document.hidden || one('.dropdown-menu.show') || one('[data-video-results]')?.contains(document.activeElement))) return;
            controller?.abort(); controller = new AbortController(); const aborter = controller; const ticket = ++serial; loading = true;
            const clean = new URL(url, location.href); clean.searchParams.delete('fragment');
            const requestUrl = new URL(clean); requestUrl.searchParams.set('fragment', 'true');
            const timeout = setTimeout(() => aborter.abort(), 12000);
            try {
                const response = await fetch(requestUrl, { headers, signal: aborter.signal, cache: 'no-store' });
                if (!response.ok) throw new Error();
                const html = await response.text(); if (ticket !== serial) return;
                const holder = document.createElement('template'); holder.innerHTML = html; const next = one('[data-video-results]', holder.content);
                if (!next) throw new Error();
                const current = one('[data-video-results]');
                const fingerprint = n => n.innerHTML.replace(/value="[^"]*"(?=[^>]*name="__RequestVerificationToken")/g, '').replace(/(<input[^>]*name="__RequestVerificationToken"[^>]*value=")[^"]*/g, '$1');
                if (fingerprint(current) !== fingerprint(next)) { current.querySelectorAll('[data-bs-toggle="dropdown"]').forEach(n => window.bootstrap?.Dropdown.getInstance(n)?.dispose()); current.replaceWith(next); dropdowns(); }
                if (navigate) { if (writeHistory) history.pushState(null, '', clean); for (const element of filter.elements) if (element.name) element.value = clean.searchParams.get(element.name) ?? (element.name === 'Sort' ? 'date_desc' : element.name === 'Status' ? 'active' : element.name === 'Watch' ? 'all' : ''); }
                one('[data-video-refresh]').textContent = 'Updates automatically while visible.';
            } catch (e) { if (e.name !== 'AbortError') one('[data-video-refresh]').textContent = 'Updates paused. Check your connection or sign-in; the current list is retained.'; }
            finally { clearTimeout(timeout); if (ticket === serial) loading = false; }
        }
        function filtered() { const url = new URL(filter.action); url.search = new URLSearchParams(new FormData(filter)); url.searchParams.set('Page', '1'); update(url, true); }
        filter.addEventListener('submit', e => { e.preventDefault(); filtered(); });
        filter.addEventListener('change', filtered);
        one('[name="Search"]', filter).addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(filtered, 450); });
        document.addEventListener('click', e => { const link = e.target.closest('[data-video-nav]'); if (link && !e.ctrlKey && !e.metaKey) { e.preventDefault(); update(link.href, true); } });
        document.addEventListener('submit', async e => {
            const state = e.target.closest('[data-video-state]'); if (!state) return; e.preventDefault();
            if (!confirm('Change this recorded lesson’s availability to students?')) return;
            const button = one('button', state); button.disabled = true;
            try { const response = await fetch(state.action, { method: 'POST', body: new FormData(state), headers: { ...headers, Accept: 'application/json' } }); const result = await response.json(); message(result.message || 'Could not change the lesson status.', response.ok); document.activeElement?.blur(); window.bootstrap?.Dropdown.getInstance(state.closest('.dropdown').querySelector('[data-bs-toggle]'))?.hide(); await update(location.href, true); }
            catch { message('Could not change the lesson status. Check your connection or sign-in.', false); }
            finally { button.disabled = false; }
        });
        window.addEventListener('popstate', () => update(location.href, true, false));
        setInterval(() => update(location.href), 15000);
    }
    const video = one('[data-video-player]');
    if (video) {
        let pending = 0, last = 0, sending = false, denied = false;
        const token = one('[data-video-progress-token] input')?.value;
        const status = one('[data-video-playback-status]');
        function playerError(text) { const node = one('[data-video-player-error]'); node.textContent = text; node.hidden = false; }
        async function access() {
            if (document.hidden || denied) return;
            try { const response = await fetch(video.dataset.accessUrl, { headers, cache: 'no-store', signal: AbortSignal.timeout(12000) }); if ([401, 403, 404].includes(response.status) || response.redirected) { denied = true; video.pause(); video.removeAttribute('src'); video.querySelectorAll('source').forEach(n => n.remove()); video.load(); playerError('This recording is no longer available to your account. Return to the lesson list or sign in again.'); } }
            catch { /* Media requests still enforce access; do not discard buffered playback on a transient network failure. */ }
        }
        function data(seconds) { return new URLSearchParams({ __RequestVerificationToken: token, Id: video.dataset.videoId, Position: String(Number.isFinite(video.currentTime) ? video.currentTime : 0), Played: String(Math.min(60, seconds)) }); }
        async function saveProgress() {
            if (!token || sending || denied || pending <= 0) return;
            const seconds = Math.min(60, pending); pending -= seconds; sending = true;
            try { const response = await fetch(video.dataset.progressUrl, { method: 'POST', body: data(seconds), headers, keepalive: true }); if (!response.ok) throw new Error(); const result = await response.json(); if (result.watched) video.dataset.completed = 'true'; status.textContent = video.dataset.completed === 'true' ? 'Watched' : 'Progress saved'; }
            catch { pending = Math.min(60, pending + seconds); status.textContent = 'Progress could not be saved. Check your connection.'; }
            finally { sending = false; }
        }
        video.addEventListener('loadedmetadata', () => { const resume = Number(video.dataset.resume); if (video.dataset.completed !== 'true' && resume > 0 && resume < video.duration * .95) video.currentTime = resume; last = video.currentTime; });
        video.addEventListener('timeupdate', () => { const delta = video.currentTime - last; if (!video.paused && !video.seeking && delta > 0 && delta < 5) pending = Math.min(60, pending + delta); last = video.currentTime; });
        video.addEventListener('seeking', () => { last = video.currentTime; }); video.addEventListener('seeked', () => { last = video.currentTime; });
        video.addEventListener('play', access); video.addEventListener('pause', saveProgress); video.addEventListener('ended', saveProgress);
        video.addEventListener('error', () => { if (!denied) playerError('The video could not be played. Check your connection. If the problem continues, ask the tutor to export an MP4 using H.264 video and AAC audio.'); });
        one('[data-video-start-over]').addEventListener('click', () => { video.currentTime = 0; last = 0; });
        setInterval(saveProgress, 15000); setInterval(access, 20000);
        document.addEventListener('visibilitychange', () => { if (document.hidden) saveProgress(); else access(); });
        window.addEventListener('pagehide', () => { if (token && pending > 0 && !denied) navigator.sendBeacon(video.dataset.progressUrl, data(pending)); });
    }
})();

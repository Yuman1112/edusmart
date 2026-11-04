(() => {
    function init() {
        document.querySelectorAll('[data-attachment-editor]').forEach(editor => {
            const input = editor.querySelector('input[type="file"]');
            const add = editor.querySelector('[data-add-files]');
            const drop = editor.querySelector('[data-file-drop]');
            const list = editor.querySelector('[data-file-list]');
            const total = editor.querySelector('[data-file-total]');
            const error = editor.querySelector('[data-file-error]');
            const form = editor.closest('form');
            if (!input || !form || typeof DataTransfer === 'undefined') return;
            const notice = document.createElement('p');
            notice.className = 'alert alert-info mt-3 mb-0'; notice.hidden = true;
            notice.setAttribute('role', 'status'); notice.setAttribute('aria-atomic', 'true');
            error.insertAdjacentElement('beforebegin', notice);
            let noticeTimer;
            function showNotice(text = '') {
                clearTimeout(noticeTimer); notice.textContent = text; notice.hidden = !text;
                if (text) noticeTimer = setTimeout(() => { notice.hidden = true; notice.textContent = ''; }, 5000);
            }
            let pending = [];
            const previews = new Map();
            function previewType(name) {
                return { pdf: 'application/pdf', png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg' }[name.split('.').pop().toLowerCase()];
            }
            function previewFor(file, type) {
                if (!previews.has(file)) {
                    const blob = file.slice(0, file.size, type);
                    const entry = { url: URL.createObjectURL(blob), data: null, reader: null, images: [] };
                    previews.set(file, entry);
                    if (type.startsWith('image/') && typeof FileReader !== 'undefined') {
                        const reader = new FileReader(); entry.reader = reader;
                        reader.addEventListener('load', () => {
                            if (!previews.has(file)) return;
                            entry.data = reader.result;
                            entry.images.forEach(img => { img.src = entry.data; }); entry.images = [];
                        });
                        reader.readAsDataURL(blob);
                    }
                }
                return previews.get(file);
            }
            function release(file) {
                const entry = previews.get(file);
                if (entry) { if (entry.reader?.readyState === 1) entry.reader.abort(); URL.revokeObjectURL(entry.url); previews.delete(file); }
            }
            const saved = Array.from(editor.querySelectorAll('[data-saved-file]'));
            const classField = form.querySelector?.('select[name="ClassSessionId"]');
            const titleField = form.querySelector?.('input[name="Title"]');
            let attempted = false;
            function validateField(field, text) {
                if (!field) return false;
                const target = form.querySelector(`[data-valmsg-for="${field.name}"]`);
                if (target) {
                    target.textContent = text;
                    target.classList.toggle('field-validation-error', Boolean(text));
                    target.classList.toggle('field-validation-valid', !text);
                }
                field.classList.toggle('input-validation-error', Boolean(text));
                field.setAttribute('aria-invalid', text ? 'true' : 'false');
                return Boolean(text);
            }
            function requiredFields() {
                const badClass = validateField(classField, classField && (!classField.value || classField.disabled) ? 'Please select an available class.' : '');
                const badTitle = validateField(titleField, titleField && !titleField.value.trim() ? 'Please enter a material title.' : '');
                return badClass ? classField : badTitle ? titleField : null;
            }
            classField?.addEventListener('change', () => { if (attempted) requiredFields(); });
            titleField?.addEventListener('input', () => { if (attempted) requiredFields(); });
            function showError(text = '') { if (text) showNotice(); error.textContent = text; error.hidden = !text; }
            function summary(files = pending) {
                const kept = saved.filter(row => row.dataset.removed !== 'true');
                return {
                    count: kept.length + files.length,
                    bytes: kept.reduce((n, row) => n + Number(row.dataset.size), 0) + files.reduce((n, f) => n + f.size, 0)
                };
            }
            function limits(files, requireFile = false) {
                const size = summary(files);
                if (requireFile && size.count === 0) return 'Keep at least one material file.';
                if (size.count > 10 || size.bytes > 30 * 1024 * 1024) return 'Keep up to 10 files and 30 MB in total, including saved attachments.';
                if (files.some(f => f.size === 0 || f.size > 10 * 1024 * 1024)) return 'Each file must contain data and be at most 10 MB.';
                if (files.some(f => !/\.(pdf|docx|pptx|xlsx|png|jpe?g)$/i.test(f.name))) return 'Choose PDF, DOCX, PPTX, XLSX, PNG or JPG files.';
                return '';
            }
            function sync() {
                const transfer = new DataTransfer();
                pending.forEach(f => transfer.items.add(f));
                input.files = transfer.files;
            }
            function render() {
                previews.forEach((entry, file) => { if (!pending.includes(file)) release(file); });
                list.querySelectorAll('[data-new-file]').forEach(row => row.remove());
                pending.forEach((file, index) => {
                    const row = document.createElement('div'); row.className = 'material-attachment-row'; row.dataset.newFile = '';
                    const info = document.createElement('div'); const icon = document.createElement('i'); icon.className = 'bi bi-file-earmark-text material-attachment-icon'; icon.setAttribute('aria-hidden', 'true'); const name = document.createElement('span'); name.textContent = file.name;
                    const detail = document.createElement('small'); detail.className = 'text-muted d-block'; detail.textContent = `${(file.size / 1024 / 1024).toFixed(2)} MB · New`;
                    info.append(icon, name, detail);
                    const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'outline-button'; remove.textContent = '×'; remove.setAttribute('aria-label', `Remove ${file.name}`);
                    remove.addEventListener('click', () => { pending.splice(index, 1); sync(); showError(); showNotice(); render(); });
                    row.append(info);
                    const type = previewType(file.name);
                    if (type && typeof URL !== 'undefined' && typeof URL.createObjectURL === 'function') {
                        const entry = previewFor(file, type);
                        if (type.startsWith('image/')) {
                            const thumbnail = document.createElement('a'); thumbnail.className = 'material-thumbnail'; thumbnail.href = entry.url; thumbnail.target = '_blank'; thumbnail.rel = 'noopener'; thumbnail.setAttribute('aria-label', `Preview ${file.name}`); thumbnail.dataset.materialPreview = ''; thumbnail.dataset.previewName = file.name; thumbnail.dataset.previewType = type;
                            const img = document.createElement('img'); img.alt = file.name;
                            if (entry.data) img.src = entry.data; else { entry.images = [img]; }
                            thumbnail.append(img); icon.replaceWith(thumbnail);
                        }
                        const preview = document.createElement('a'); preview.className = 'outline-button material-file-preview'; preview.textContent = 'Preview'; preview.href = entry.url; preview.target = '_blank'; preview.rel = 'noopener';
                        preview.setAttribute('aria-label', `Preview ${file.name}`); preview.dataset.materialPreview = ''; preview.dataset.previewName = file.name; preview.dataset.previewType = type; row.append(preview);
                    }
                    row.append(remove); list.append(row);
                });
                const size = summary(); total.hidden = size.count === 0; total.textContent = `${size.count} file${size.count === 1 ? '' : 's'} · ${(size.bytes / 1024 / 1024).toFixed(2)} MB`;
                const empty = editor.querySelector('[data-file-empty]');
                if (empty) empty.hidden = pending.length > 0 || saved.length > 0;
            }
            function addFiles(files) {
                showNotice();
                const next = [...pending];
                let duplicates = 0;
                const selected = Array.from(files);
                selected.forEach(f => {
                    if (next.some(old => old.name === f.name && old.size === f.size && old.lastModified === f.lastModified)) duplicates++;
                    else next.push(f);
                });
                const added = next.length - pending.length;
                const problem = limits(next);
                if (!problem) pending = next;
                sync(); showError(problem); render();
                if (!problem && duplicates) {
                    if (duplicates === 1 && added === 0) showNotice('This file has already been added. The duplicate was skipped.');
                    else showNotice(`${added ? `${added} file${added === 1 ? '' : 's'} added. ` : ''}${duplicates} duplicate file${duplicates === 1 ? '' : 's'} skipped.`);
                }
            }
            input.hidden = true; add.hidden = false;
            add.addEventListener('click', () => { input.value = ''; input.click(); });
            input.addEventListener('cancel', sync);
            input.addEventListener('change', () => addFiles(input.files));
            if (drop) {
                drop.hidden = false;
                let depth = 0;
                const hasFiles = event => Array.from(event.dataTransfer?.types || []).includes('Files');
                drop.addEventListener('dragenter', event => {
                    if (!hasFiles(event)) return;
                    event.preventDefault(); depth++; drop.classList.add('is-dragging');
                });
                drop.addEventListener('dragover', event => {
                    if (!hasFiles(event)) return;
                    event.preventDefault(); event.dataTransfer.dropEffect = 'copy';
                });
                drop.addEventListener('dragleave', event => {
                    if (!hasFiles(event)) return;
                    event.preventDefault(); depth = Math.max(0, depth - 1);
                    if (!depth) drop.classList.remove('is-dragging');
                });
                drop.addEventListener('drop', event => {
                    if (!hasFiles(event)) return;
                    event.preventDefault(); event.stopPropagation(); depth = 0; drop.classList.remove('is-dragging');
                    const files = Array.from(event.dataTransfer.files || []);
                    if (files.length) addFiles(files);
                    else showError('Drop individual files, not folders.');
                });
                // Prevent a dropped file outside the target from replacing the form with a browser file page.
                document.addEventListener('dragover', event => { if (hasFiles(event)) event.preventDefault(); });
                document.addEventListener('drop', event => {
                    if (!hasFiles(event) || drop.contains(event.target)) return;
                    event.preventDefault(); depth = 0; drop.classList.remove('is-dragging');
                    showError('Drop files inside the attachment area, or choose Add Files.');
                });
                if (typeof window !== 'undefined') window.addEventListener('blur', () => { depth = 0; drop.classList.remove('is-dragging'); });
            }
            saved.forEach(row => {
                const button = row.querySelector('[data-toggle-remove]');
                const removal = row.querySelector('input[name="RemovedAttachmentIds"]');
                button.addEventListener('click', () => {
                    const removed = row.dataset.removed !== 'true'; row.dataset.removed = String(removed); removal.disabled = !removed;
                    button.textContent = removed ? 'Undo' : '×'; button.setAttribute('aria-label', removed ? 'Undo attachment removal' : 'Remove attachment');
                    row.querySelector('[data-file-state]').textContent = removed ? 'Pending removal' : 'Saved';
                    showNotice(); showError(limits(pending)); render();
                });
            });
            form.addEventListener('submit', event => {
                attempted = true;
                const firstInvalid = requiredFields();
                const problem = limits(pending, true);
                showError(problem);
                if (problem || firstInvalid) {
                    event.preventDefault(); event.stopImmediatePropagation();
                    if (firstInvalid) { firstInvalid.focus(); firstInvalid.scrollIntoView({ block: 'nearest' }); }
                    else error.scrollIntoView({ block: 'nearest' });
                }
                else sync();
            }, true);
            render();
            if (typeof window !== 'undefined') {
                window.addEventListener('pagehide', () => { showNotice(); Array.from(previews.keys()).forEach(release); });
                window.addEventListener('pageshow', event => { if (event.persisted) render(); });
            }
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

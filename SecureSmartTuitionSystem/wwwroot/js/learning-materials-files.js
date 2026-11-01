(() => {
    function init() {
        document.querySelectorAll('[data-attachment-editor]').forEach(editor => {
            const input = editor.querySelector('input[type="file"]');
            const add = editor.querySelector('[data-add-files]');
            const list = editor.querySelector('[data-file-list]');
            const total = editor.querySelector('[data-file-total]');
            const error = editor.querySelector('[data-file-error]');
            const form = editor.closest('form');
            if (!input || !form || typeof DataTransfer === 'undefined') return;
            let pending = [];
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
            function showError(text = '') { error.textContent = text; error.hidden = !text; }
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
                list.querySelectorAll('[data-new-file]').forEach(row => row.remove());
                pending.forEach((file, index) => {
                    const row = document.createElement('div'); row.className = 'material-attachment-row'; row.dataset.newFile = '';
                    const info = document.createElement('div'); const icon = document.createElement('i'); icon.className = 'bi bi-file-earmark-text material-attachment-icon'; icon.setAttribute('aria-hidden', 'true'); const name = document.createElement('span'); name.textContent = file.name;
                    const detail = document.createElement('small'); detail.className = 'text-muted d-block'; detail.textContent = `${(file.size / 1024 / 1024).toFixed(2)} MB · New`;
                    info.append(icon, name, detail);
                    const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'outline-button'; remove.textContent = '×'; remove.setAttribute('aria-label', `Remove ${file.name}`);
                    remove.addEventListener('click', () => { pending.splice(index, 1); sync(); showError(); render(); });
                    row.append(info, remove); list.append(row);
                });
                const size = summary(); total.textContent = `${size.count} file${size.count === 1 ? '' : 's'} · ${(size.bytes / 1024 / 1024).toFixed(2)} MB`;
                const empty = editor.querySelector('[data-file-empty]');
                if (empty) empty.hidden = pending.length > 0 || saved.length > 0;
            }
            input.hidden = true; add.hidden = false;
            add.addEventListener('click', () => input.click());
            input.addEventListener('change', () => {
                const next = [...pending];
                Array.from(input.files).forEach(f => {
                    if (!next.some(old => old.name === f.name && old.size === f.size && old.lastModified === f.lastModified)) next.push(f);
                });
                const problem = limits(next);
                if (!problem) pending = next;
                sync(); showError(problem); render();
            });
            saved.forEach(row => {
                const button = row.querySelector('[data-toggle-remove]');
                const removal = row.querySelector('input[name="RemovedAttachmentIds"]');
                button.addEventListener('click', () => {
                    const removed = row.dataset.removed !== 'true'; row.dataset.removed = String(removed); removal.disabled = !removed;
                    button.textContent = removed ? 'Undo' : '×'; button.setAttribute('aria-label', removed ? 'Undo attachment removal' : 'Remove attachment');
                    row.querySelector('[data-file-state]').textContent = removed ? 'Pending removal' : 'Saved';
                    showError(limits(pending)); render();
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
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

(() => {
    'use strict';
    const formatDate = value => new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Kuala_Lumpur', day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: true }).format(new Date(value)) + ' MYT';
    function deadlineState(expiresAt, now) {
        const remaining = expiresAt - now;
        if (remaining <= 0) return { warning: true, text: 'Expired. Replacing the video will not reopen student access. Staff can still review it.' };
        const minutes = Math.max(1, Math.ceil(remaining / 60000));
        const days = Math.floor(minutes / 1440), hours = Math.floor(minutes % 1440 / 60), mins = minutes % 60;
        return { warning: remaining <= 86400000, text: `${remaining <= 86400000 ? 'Less than 24 hours remain. ' : ''}Time remaining: ${days}d ${hours}h ${mins}m. Replacing the video does not extend this deadline.` };
    }
    const form = document.querySelector('[data-video-form]');
    let expiresAt = Number(form?.dataset.videoExpiry), serverNow = Number(form?.dataset.videoServerNow), receivedAt = Date.now(), verificationError = '';
    const now = () => serverNow + (Date.now() - receivedAt);
    function display() {
        if (!form || !Number.isFinite(expiresAt) || expiresAt <= 0) return;
        const state = deadlineState(expiresAt, now());
        for (const selector of ['[data-video-time-left]', '[data-video-confirm-time]']) {
            const node = form.querySelector(selector);
            if (node) { const isConfirm = node.matches('[data-video-confirm-time]'); node.textContent = isConfirm && verificationError ? verificationError : state.text; if (isConfirm) node.className = `alert ${state.warning || verificationError ? 'alert-warning' : 'alert-info'}`; }
        }
        const notice = form.querySelector('[data-video-expiry-notice]');
        if (notice) notice.className = `alert ${state.warning ? 'alert-warning' : 'alert-info'}`;
        const selected = form.querySelector('[data-video-selected-deadline]');
        if (selected) { selected.hidden = !form.elements.VideoFile?.files.length; selected.textContent = `Student viewing progress will reset. Access still ends on ${formatDate(expiresAt)}. ${state.text}`; }
        const confirmDeadline = form.querySelector('[data-video-confirm-deadline]');
        if (confirmDeadline) confirmDeadline.textContent = `Access still ends on ${formatDate(expiresAt)}.`;
    }
    async function refreshClock(target) {
        const url = new URL(target.dataset.videoReplacementInfoUrl, location.href);
        url.searchParams.set('id', target.elements.Id.value); url.searchParams.set('rowVersion', target.elements.RowVersion.value);
        const response = await fetch(url, { cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: AbortSignal.timeout(12000) });
        if (!response.ok || response.redirected) throw new Error('Replacement is no longer available. Reload this page and check your sign-in before replacing the video.');
        const data = await response.json();
        if (!Number.isFinite(data.expiresAt) || !Number.isFinite(data.serverNow)) throw new Error('The viewing deadline could not be verified. Please reload this page.');
        expiresAt = data.expiresAt; serverNow = data.serverNow; receivedAt = Date.now(); display();
    }
    async function confirmReplacement(target, selectedFile) {
        const ack = target.elements.ReplacementConfirmed;
        ack.value = 'false';
        verificationError = '';
        await refreshClock(target);
        if (target.elements.VideoFile.files[0] !== selectedFile) return false;
        const dialog = target.querySelector('[data-video-replace-confirm]');
        if (!dialog?.showModal) throw new Error('Use a current browser to review and confirm this replacement.');
        target.querySelector('[data-video-confirm-file]').textContent = selectedFile.name;
        const accept = target.querySelector('[data-video-confirm-accept]');
        const cancel = target.querySelector('[data-video-confirm-cancel]');
        return new Promise(resolve => {
            let finished = false;
            const finish = accepted => {
                if (finished) return; finished = true;
                accept.removeEventListener('click', onAccept); cancel.removeEventListener('click', onCancel); dialog.removeEventListener('cancel', onEscape);
                dialog.close(); ack.value = accepted ? 'true' : 'false'; resolve(accepted);
            };
            const onCancel = () => finish(false);
            const onEscape = event => { event.preventDefault(); finish(false); };
            const onAccept = async () => {
                verificationError = '';
                accept.disabled = true; cancel.disabled = true;
                try {
                    await refreshClock(target);
                    finish(target.elements.VideoFile.files[0] === selectedFile);
                } catch (error) {
                    verificationError = error.message;
                    target.querySelector('[data-video-confirm-time]').textContent = error.message;
                    target.querySelector('[data-video-confirm-time]').className = 'alert alert-warning';
                } finally { accept.disabled = false; cancel.disabled = false; }
            };
            accept.addEventListener('click', onAccept); cancel.addEventListener('click', onCancel); dialog.addEventListener('cancel', onEscape);
            display(); dialog.showModal(); cancel.focus();
        });
    }
    window.eduSmartVideoReplacement = { confirm: confirmReplacement, deadlineState };
    if (form?.dataset.editing === 'true') {
        form.elements.VideoFile?.addEventListener('change', () => { form.elements.ReplacementConfirmed.value = 'false'; display(); });
        display(); setInterval(display, 1000);
        document.addEventListener('visibilitychange', () => { if (!document.hidden) refreshClock(form).catch(() => {}); });
    }
})();

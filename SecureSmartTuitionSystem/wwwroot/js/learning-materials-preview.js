(() => {
    function init() {
        if (!document.querySelector('.learning-materials-page') || !window.HTMLDialogElement) return;
        const dialog = document.createElement('dialog'); dialog.className = 'material-preview-dialog';
        dialog.setAttribute('aria-labelledby', 'material-preview-name');
        const header = document.createElement('div'); header.className = 'material-preview-header';
        const title = document.createElement('h2'); title.id = 'material-preview-name';
        const close = document.createElement('button'); close.type = 'button'; close.className = 'outline-button'; close.textContent = '×'; close.setAttribute('aria-label', 'Close preview');
        header.append(title, close);
        const content = document.createElement('div'); content.className = 'material-preview-content';
        const footer = document.createElement('div'); footer.className = 'material-preview-footer';
        const hint = document.createElement('span'); hint.textContent = 'If the preview does not load, open it in a new tab.';
        const external = document.createElement('a'); external.className = 'outline-button'; external.textContent = 'Open in new tab'; external.target = '_blank'; external.rel = 'noopener';
        footer.append(hint, external); dialog.append(header, content, footer); document.body.append(dialog);
        let trigger, sequence = 0;
        function clear() { ++sequence; content.replaceChildren(); external.removeAttribute('href'); }
        function failure(text) {
            const warning = document.createElement('p'); warning.className = 'alert alert-warning'; warning.setAttribute('role', 'alert'); warning.textContent = text; content.replaceChildren(warning);
        }
        close.addEventListener('click', () => dialog.close());
        dialog.addEventListener('close', () => { clear(); trigger?.focus(); });
        dialog.addEventListener('click', event => {
            if (event.target !== dialog) return;
            const box = dialog.getBoundingClientRect();
            if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) dialog.close();
        });
        document.addEventListener('click', event => {
            const link = event.target.closest?.('a[data-material-preview]');
            if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button > 0) return;
            const type = link.dataset.previewType;
            if (!['application/pdf', 'image/png', 'image/jpeg'].includes(type)) return;
            let url;
            try { url = new URL(link.href, window.location.href); } catch { return; }
            if (!['http:', 'https:', 'blob:'].includes(url.protocol) || url.origin !== window.location.origin) return;
            event.preventDefault(); clear(); trigger = link;
            const request = sequence;
            title.textContent = link.dataset.previewName || 'Material preview'; external.href = url.href;
            if (!dialog.open) dialog.showModal();
            if (type === 'application/pdf') {
                const frame = document.createElement('iframe'); frame.title = title.textContent; frame.className = 'material-preview-pdf';
                frame.addEventListener('load', () => {
                    if (request !== sequence) return;
                    try {
                        const mime = frame.contentDocument?.contentType;
                        if (mime?.startsWith('text/')) failure('This file is unavailable or your session expired. Close the preview and reload the page.');
                    } catch { /* The browser PDF viewer may use an isolated origin. */ }
                });
                frame.src = url.href; content.append(frame);
            } else {
                const img = document.createElement('img'); img.alt = title.textContent; img.className = 'material-preview-image';
                img.addEventListener('error', () => { if (request === sequence) failure('This image could not be loaded. Check your access or reload the page.'); });
                img.src = url.href; content.append(img);
            }
        });
        window.addEventListener('pagehide', () => { if (dialog.open) dialog.close(); clear(); });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

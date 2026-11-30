document.addEventListener("DOMContentLoaded", function () {
    const sidebar = document.getElementById("staffSidebar");
    const toggle = document.getElementById("sidebarToggle");
    const overlay = document.getElementById("sidebarOverlay");

    if (!sidebar || !toggle || !overlay) {
        return;
    }

    sidebar.inert = window.innerWidth <= 768;

    function openSidebar() {
        sidebar.inert = false;
        sidebar.classList.add("open");
        overlay.hidden = false;
        overlay.classList.add("show");
        toggle.setAttribute("aria-expanded", "true");
        toggle.setAttribute("aria-label", "Close navigation");
        sidebar.querySelector("a, button")?.focus();
    }

    function closeSidebar(returnFocus = true) {
        sidebar.classList.remove("open");
        sidebar.inert = window.innerWidth <= 768;
        overlay.classList.remove("show");
        overlay.hidden = true;
        toggle.setAttribute("aria-expanded", "false");
        toggle.setAttribute("aria-label", "Open navigation");
        if (returnFocus) toggle.focus();
    }

    toggle.addEventListener("click", function () {
        if (sidebar.classList.contains("open")) closeSidebar();
        else openSidebar();
    });
    overlay.addEventListener("click", function () { closeSidebar(); });
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && sidebar.classList.contains("open")) closeSidebar();
    });
    window.addEventListener("resize", function () {
        if (window.innerWidth > 768) closeSidebar(false);
        else if (!sidebar.classList.contains("open")) sidebar.inert = true;
    });
});
// Confirmation handlers work with a nonce-based CSP (no inline event attributes).
document.addEventListener("submit", function (event) {
    const form = event.target;
    const prompt = form.dataset?.confirm || event.submitter?.dataset?.confirm;
    if (prompt && !window.confirm(prompt)) event.preventDefault();
});
document.addEventListener("DOMContentLoaded", function () {
    document.getElementById("printReceiptButton")?.addEventListener("click", function () { if (typeof window.printReceipt === "function") window.printReceipt(); else window.print(); });
});

// Shared success feedback: five seconds, or dismiss immediately with X.
(() => {
    const active = new WeakMap();
    function enhance() {
        document.querySelectorAll('.alert-success, [data-success-timeout]').forEach(notice => {
            if (notice.hidden || getComputedStyle(notice).display === 'none') {
                const previous = active.get(notice);
                if (previous) clearTimeout(previous.timer);
                active.delete(notice); return;
            }
            if (!notice.classList.contains('alert-dismissible')) notice.classList.add('alert-dismissible');
            let close = notice.querySelector('.btn-close, [data-bs-dismiss="alert"], .success-close');
            if (!close) {
                close = document.createElement('button'); close.type = 'button';
                close.className = 'success-close'; close.textContent = '×';
                close.setAttribute('aria-label', 'Close'); notice.append(close);
                close.addEventListener('click', () => { notice.hidden = true; });
            }
            const signature = Array.from(notice.childNodes).filter(node => node !== close)
                .map(node => node.textContent).join('');
            const previous = active.get(notice);
            if (previous?.signature === signature) return;
            if (previous) clearTimeout(previous.timer);
            const delay = Number(notice.dataset.successTimeout);
            const timer = setTimeout(() => { notice.hidden = true; },
                Number.isFinite(delay) && delay > 0 ? delay : 5000);
            active.set(notice, { signature, timer });
        });
    }
    document.addEventListener('DOMContentLoaded', () => {
        enhance();
        const observer = new MutationObserver(enhance);
        observer.observe(document.body, { childList:true, subtree:true,
            characterData:true, attributes:true, attributeFilter:['class','hidden','style'] });
        window.addEventListener('pagehide', () => observer.disconnect(), { once:true });
    });
})();

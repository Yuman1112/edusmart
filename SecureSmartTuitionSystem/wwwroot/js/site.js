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

// Opt-in success messages disappear after five seconds.
document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-success-timeout]").forEach(alert => {
        const delay = Number(alert.dataset.successTimeout);
        window.setTimeout(() => alert.remove(), Number.isFinite(delay) && delay > 0 ? delay : 5000);
    });
});

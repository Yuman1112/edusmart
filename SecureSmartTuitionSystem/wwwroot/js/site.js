document.addEventListener("DOMContentLoaded", function () {
    const sidebar = document.getElementById("staffSidebar");
    const toggle = document.getElementById("sidebarToggle");
    const overlay = document.getElementById("sidebarOverlay");

    if (!sidebar || !toggle || !overlay) {
        return;
    }

    function openSidebar() {
        sidebar.classList.add("open");
        overlay.classList.add("show");
    }

    function closeSidebar() {
        sidebar.classList.remove("open");
        overlay.classList.remove("show");
    }

    toggle.addEventListener("click", openSidebar);
    overlay.addEventListener("click", closeSidebar);

    window.addEventListener("resize", function () {
        if (window.innerWidth > 991) {
            closeSidebar();
        }
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

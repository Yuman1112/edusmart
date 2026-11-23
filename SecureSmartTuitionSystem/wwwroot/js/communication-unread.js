document.addEventListener("DOMContentLoaded", () => {
    const endpoint = document.querySelector("script[data-communication-counts-url]")?.dataset.communicationCountsUrl;
    if (!endpoint) return;
    let pending = false;
    function count(selector) { return Number(document.querySelector(selector)?.dataset.unreadCount || 0); }
    function badge(selector, value, label, state = "unread") {
        value = Math.max(0, Number(value) || 0);
        document.querySelectorAll(selector).forEach(el => {
            el.hidden = value === 0; el.dataset.unreadCount = String(value);
            el.textContent = value > 99 ? "99+" : String(value);
            el.title = `${value} ${state} ${label}`; el.setAttribute("aria-label", el.title);
        });
    }
    function total() {
        const unread = count("[data-announcement-unread-total]") +
            count("[data-notification-unread-total]") + count("[data-message-unread-total]");
        const pendingEnquiries = count("[data-guest-enquiry-pending]");
        badge("[data-communication-unread-total]", unread + pendingEnquiries, "communication item(s)", "new");
    }
    async function refresh() {
        if (pending || document.hidden) return;
        pending = true;
        try {
            const response = await fetch(endpoint, { cache: "no-store", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) return;
            const result = await response.json();
            badge("[data-announcement-unread-total]", result.announcements, "announcement(s)");
            badge("[data-notification-unread-total]", result.notifications, "notification(s)");
            badge("[data-guest-enquiry-pending]", result.guestEnquiries, "guest enquiry(s)", "unresolved");
            badge('[data-admin-work="replacements"]', result.replacements, "replacement(s)", "pending");
            badge('[data-admin-work="enrollments"]', result.enrollments, "enrollment request(s)", "pending");
            badge('[data-admin-work="withdrawals"]', result.withdrawals, "withdrawal(s)", "unreviewed");
            badge('[data-admin-work="total"]', result.replacements + result.enrollments + result.withdrawals, "class and enrollment item(s)", "pending");
            total();
        } catch { /* Retain the last known counts during a temporary connection failure. */ }
        finally { pending = false; }
    }
    document.addEventListener("edusmart:message-counts-updated", total);
    document.addEventListener("edusmart:unread-changed", refresh);
    document.addEventListener("visibilitychange", refresh);
    refresh(); window.setInterval(refresh, 5000);
});

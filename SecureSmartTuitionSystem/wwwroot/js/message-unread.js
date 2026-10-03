document.addEventListener("DOMContentLoaded", function () {
    const script = document.querySelector("script[data-unread-url]");
    const endpoint = script?.dataset.unreadUrl;
    if (!endpoint) return;
    let pending = false;

    function setBadge(badge, count) {
        badge.hidden = count === 0;
        badge.textContent = String(count);
        badge.title = `${count} unread message(s)`;
        badge.setAttribute("aria-label", badge.title);
    }

    async function refresh() {
        if (pending || document.hidden) return;
        pending = true;
        try {
            const response = await fetch(endpoint, {
                cache: "no-store", headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) return;
            const result = await response.json();
            document.querySelectorAll("[data-message-unread-total]").forEach(
                badge => setBadge(badge, result.totalUnreadCount));
            const counts = new Map(result.conversations.map(item => [String(item.conversationId), item.unreadCount]));
            document.querySelectorAll(".conversation-item[data-conversation-id]").forEach(item => {
                const badge = item.querySelector("[data-conversation-unread]");
                if (badge) setBadge(badge, counts.get(item.dataset.conversationId) || 0);
            });
        } catch {
            // Preserve the last known counts during a temporary connection failure.
        } finally {
            pending = false;
        }
    }

    refresh();
    window.setInterval(refresh, 5000);
    document.addEventListener("visibilitychange", refresh);
    document.addEventListener("edusmart:unread-changed", refresh);
});

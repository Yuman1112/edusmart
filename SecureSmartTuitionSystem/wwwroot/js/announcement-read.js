document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("announcementReadForm");
    let acknowledged = false;
    async function acknowledge() {
        if (!form || document.hidden || acknowledged) return;
        try {
            const response = await fetch(form.action, {
                method: "POST", body: new FormData(form), credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }, cache: "no-store"
            });
            if (response.ok) acknowledged = true;
        } catch { /* Do not clear unread state when acknowledgement fails. */ }
    }
    acknowledge();
    document.addEventListener("visibilitychange", acknowledge);
});

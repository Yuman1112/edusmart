document.addEventListener("DOMContentLoaded", () => {
    const key = "eduSmartFilterFocus";
    try { const saved = JSON.parse(sessionStorage.getItem(key) || "null"); sessionStorage.removeItem(key); if (saved?.path === location.pathname) { const input = document.getElementById(saved.id); input?.focus(); if (input && typeof input.setSelectionRange === "function" && ["text", "search"].includes(input.type)) input.setSelectionRange(saved.start, saved.end); } } catch { /* Storage can be unavailable. */ }
    document.querySelectorAll("form[data-live-filter]").forEach(form => {
        form.classList.add("live-filter-form"); let timer;
        function schedule(delay) { clearTimeout(timer); timer = setTimeout(() => { if (form.checkValidity()) form.requestSubmit(); }, delay); }
        form.addEventListener("input", event => { if (["text", "search", "number"].includes(event.target.type)) schedule(350); });
        form.addEventListener("change", () => schedule(0));
        form.addEventListener("submit", () => {
            clearTimeout(timer);
            const input = document.activeElement;
            try { if (input?.id && form.contains(input)) sessionStorage.setItem(key, JSON.stringify({ path: location.pathname, id: input.id, start: input.selectionStart, end: input.selectionEnd })); } catch { /* Continue filtering. */ }
        });
    });
});

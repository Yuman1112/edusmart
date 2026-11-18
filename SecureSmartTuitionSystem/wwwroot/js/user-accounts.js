(() => {
    "use strict";
    const form = document.getElementById("account-filter-form");
    const page = form?.closest(".account-list-page");
    const feedback = document.getElementById("account-filter-feedback");
    if (!form || !page || !feedback || !window.fetch || !window.AbortController) return;
    let enabled = true;
    let timer;
    let pending;
    let revision = 0;
    page.classList.add("account-filters-enhanced");
    document.getElementById("account-filter-hint").textContent = "The account list updates automatically.";

    function cancel() {
        clearTimeout(timer);
        pending?.abort();
        revision += 1;
        return revision;
    }
    function filterUrl() {
        const url = new URL(form.action, location.href);
        for (const [key, value] of new FormData(form)) {
            const text = String(value).trim();
            if (text) url.searchParams.set(key, text);
        }
        url.searchParams.set("page", "1");
        return url;
    }
    async function update(url, id, focusResults = false) {
        if (!enabled || id !== revision) return;
        pending = new AbortController();
        document.getElementById("account-results")?.setAttribute("aria-busy", "true");
        feedback.textContent = "Updating accounts…";
        try {
            const response = await fetch(url, {
                credentials: "same-origin",
                signal: pending.signal,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (id !== revision) return;
            // Follow real authentication/authorization redirects instead of inserting a login page.
            if (response.redirected) {
                const destination = new URL(response.url);
                if (destination.origin !== location.origin) throw new Error("Unexpected redirect");
                location.assign(destination.href);
                return;
            }
            if (!response.ok) throw new Error("Account list request failed");
            const html = await response.text();
            if (id !== revision) return;
            const doc = new DOMParser().parseFromString(html, "text/html");
            const replacement = doc.getElementById("account-results");
            if (!replacement) throw new Error("Account list response missing");
            const current = document.getElementById("account-results");
            current.replaceWith(document.importNode(replacement, true));
            form.elements.namedItem("sort").value = url.searchParams.get("sort") || "id_asc";
            history.replaceState(null, "", url.pathname + url.search);
            feedback.textContent = "Accounts updated.";
            if (focusResults) {
                const heading = document.getElementById("accountsHeading");
                heading.setAttribute("tabindex", "-1");
                heading.focus({ preventScroll: true });
            }
        } catch (error) {
            if (error.name === "AbortError" || id !== revision) return;
            enabled = false;
            page.classList.remove("account-filters-enhanced");
            feedback.textContent = "Could not update accounts. Use Search to reload the results.";
            document.getElementById("account-filter-hint").textContent = "Choose filters and select Search.";
        } finally {
            if (id === revision) document.getElementById("account-results")?.removeAttribute("aria-busy");
        }
    }
    form.addEventListener("submit", event => {
        if (!enabled) return; // Native GET remains available when automatic filtering fails.
        event.preventDefault();
        const id = cancel();
        void update(filterUrl(), id);
    });
    form.addEventListener("input", event => {
        if (!enabled || event.target.name !== "search") return;
        const id = cancel();
        timer = setTimeout(() => void update(filterUrl(), id), 300);
    });
    form.addEventListener("change", event => {
        if (!enabled || event.target.tagName !== "SELECT") return;
        const id = cancel();
        void update(filterUrl(), id);
    });
    page.addEventListener("click", event => {
        const link = event.target.closest("a[data-account-nav]");
        if (!enabled || !link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        const id = cancel();
        void update(new URL(link.href), id, true);
    });
    window.addEventListener("pagehide", cancel);
})();

document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("securityFilters"), results = document.getElementById("securityResults"), status = document.getElementById("securityFilterStatus");
    if (!form || !results || !status) return;
    let timer, controller;
    async function update(url) {
        controller?.abort();
        const current = new AbortController(); controller = current;
        results.setAttribute("aria-busy", "true"); status.hidden = true; status.textContent = "";
        try {
            const response = await fetch(url, { signal: current.signal, cache: "no-store", credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) throw new Error("Request failed");
            const html = new DOMParser().parseFromString(await response.text(), "text/html");
            const next = html.getElementById("securityResults");
            if (!next) throw new Error("Results missing");
            if (current.signal.aborted) return;
            results.replaceChildren(...Array.from(next.childNodes));
            window.history.replaceState(null, "", url);
            status.hidden = true; status.textContent = "";
            window.EduSmartTables?.enhance(results);
        } catch (error) { if (error.name !== "AbortError" && !current.signal.aborted) { status.hidden = false; status.textContent = "Could not update results. Refresh the page to retry or sign in again."; } }
        finally { if (controller === current) results.setAttribute("aria-busy", "false"); }
    }
    function filteredUrl() {
        const url = new URL(form.dataset.filterUrl, window.location.origin);
        for (const [key, value] of new FormData(form)) if (String(value).trim()) url.searchParams.set(key, String(value).trim());
        url.searchParams.set("pageNumber", "1"); return url;
    }
    function schedule() { clearTimeout(timer); controller?.abort(); timer = setTimeout(() => update(filteredUrl()), 300); }
    form.addEventListener("input", schedule); form.addEventListener("change", schedule);
    form.addEventListener("submit", event => { event.preventDefault(); clearTimeout(timer); update(filteredUrl()); });
    form.querySelector("[data-security-reset]")?.addEventListener("click", event => {
        event.preventDefault(); clearTimeout(timer);
        form.querySelectorAll("input,select").forEach(input => { input.value = ""; }); update(filteredUrl());
    });
    results.addEventListener("click", event => {
        const link = event.target.closest(".table-pagination a");
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault(); clearTimeout(timer); update(new URL(link.href));
    });
});

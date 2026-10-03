document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("replacementForm"), status = document.getElementById("replacementCheckStatus"), save = document.getElementById("saveReplacement");
    if (!form || !status || !save) return;
    let timer, controller, valid = false, version = 0;
    const names = ["ChangeId", "Date", "Start", "End", "Venue"];
    function schedule() {
        clearTimeout(timer); controller?.abort(); valid = false; save.disabled = true; version++;
        form.querySelector(".validation-summary-errors")?.setAttribute("hidden", "");
        status.className = "replacement-check-status text-muted mt-3"; status.textContent = "Checking availability…";
        const currentVersion = version;
        timer = setTimeout(() => check(currentVersion), 350);
    }
    async function check(currentVersion) {
        const values = new FormData(form), url = new URL(form.dataset.checkUrl, location.origin);
        if (names.some(name => !String(values.get(name) || "").trim())) { status.textContent = "Choose a date, start/end time and Google Meet link."; return; }
        names.forEach(name => url.searchParams.set(name, String(values.get(name)).trim()));
        controller = new AbortController(); const current = controller;
        try {
            const response = await fetch(url, { credentials: "same-origin", cache: "no-store", signal: current.signal });
            if (!response.ok) throw new Error("Check failed");
            const result = await response.json();
            if (current.signal.aborted || currentVersion !== version) return;
            valid = result.valid === true; save.disabled = !valid;
            status.className = `replacement-check-status mt-3 ${valid ? "text-success" : "text-danger"}`;
            status.textContent = valid ? "Available. No tutor or affected-student conflict was found. Availability will be checked again when saving." : (result.messages || ["This replacement is unavailable."]).join(" ");
        } catch (error) { if (error.name !== "AbortError" && currentVersion === version) { status.className = "replacement-check-status text-danger mt-3"; status.textContent = "Could not verify availability. Change a field to retry; check your connection or sign in again."; } }
    }
    names.filter(x => x !== "ChangeId").forEach(name => { const input = form.elements.namedItem(name); input?.addEventListener("input", schedule); input?.addEventListener("change", schedule); });
    form.addEventListener("submit", event => { if (!valid) { event.preventDefault(); schedule(); } });
    schedule();
});

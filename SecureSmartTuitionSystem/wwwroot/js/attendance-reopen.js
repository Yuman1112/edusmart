(() => {
    "use strict";
    const filter = document.getElementById("reopen-filter-form");
    filter?.querySelectorAll("select").forEach(select => select.addEventListener("change", () => filter.requestSubmit()));
    const modal = document.getElementById("reopen-review-modal");
    if (!modal) return;
    const byId = id => document.getElementById(id);
    let controller, generation = 0, historyUrl = "", submitting = false, canReview = false, activeTab = "details";
    const reviewButtons = [...byId("reopen-decision-actions").querySelectorAll('button[type="submit"]')];
    function showTab(name, focus = false) {
        activeTab = name;
        for (const tab of ["details", "history"]) {
            const button = byId(`reopen-${tab}-tab`), selected = tab === name;
            button.setAttribute("aria-selected", String(selected)); button.tabIndex = selected ? 0 : -1;
            byId(`reopen-${tab}-panel`).hidden = !selected;
            if (focus && selected) button.focus();
        }
        byId("reopen-decision-actions").hidden = !canReview || name !== "details";
        byId("reopen-review-footer").hidden = !canReview || name !== "details";
    }
    for (const name of ["details", "history"]) {
        const tab = byId(`reopen-${name}-tab`);
        tab.addEventListener("click", () => showTab(name));
        tab.addEventListener("keydown", event => {
            if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
            event.preventDefault();
            showTab(event.key === "Home" ? "details" : event.key === "End" ? "history" : name === "details" ? "history" : "details", true);
        });
    }
    async function history() {
        controller?.abort(); controller = new AbortController(); const token = ++generation;
        byId("reopen-history-section").hidden = false;
        byId("reopen-history-load-status").hidden = false;
        byId("reopen-history-load-status").textContent = "Loading request history…";
        const content = byId("reopen-history-content"); content.textContent = "Loading request history…"; content.setAttribute("aria-busy", "true");
        byId("reopen-history-retry").hidden = true; reviewButtons.forEach(button => { button.disabled = true; });
        try {
            const response = await fetch(historyUrl, { credentials: "same-origin", cache: "no-store", signal: controller.signal });
            if (!response.ok || response.redirected) throw new Error("History unavailable");
            const html = await response.text(); if (token !== generation) return;
            const parsed = new DOMParser().parseFromString(html, "text/html");
            const meta = parsed.querySelector("[data-history-loaded]");
            if (!meta) throw new Error("Invalid history response");
            const count = Number(meta.dataset.historyCount || 0);
            byId("reopen-current-attempt").textContent = `Request #${meta.dataset.attempt}`;
            byId("reopen-history-tab").textContent = `History (${count})`;
            byId("reopen-history-load-status").hidden = true;
            byId("reopen-repeat-warning").hidden = meta.dataset.missedWindow !== "true";
            content.replaceChildren(...[...parsed.body.childNodes].map(node => document.importNode(node, true)));
            reviewButtons.forEach(button => { button.disabled = !canReview; });
        } catch (error) {
            if (token !== generation || error.name === "AbortError") return;
            content.textContent = "Request history could not be loaded. Retry before reviewing this request.";
            byId("reopen-history-retry").hidden = false;
            byId("reopen-history-load-status").textContent = "History could not be loaded. Open History and retry before reviewing.";
        } finally { if (token === generation) content.removeAttribute("aria-busy"); }
    }
    document.addEventListener("click", event => {
        const button = event.target.closest("[data-reopen-review]"); if (!button) return;
        const data = button.dataset; canReview = data.status === "Pending"; submitting = false;
        byId("reopen-review-title").textContent = canReview ? "Review Reopening Request" : "Reopening Request Details";
        byId("reopen-review-id").value = data.id; byId("reopen-review-version").value = data.version;
        for (const key of ["lesson", "date", "tutor", "reason", "notes", "status", "requested"]) byId(`reopen-review-${key}`).textContent = data[key] || "—";
        byId("reopen-review-status").className = `tutor-management-badge ${canReview ? "tutor-badge-pending" : data.status === "Approved" ? "tutor-badge-success" : "tutor-badge-inactive"}`;
        byId("reopen-review-deadline").textContent = data.deadline || "";
        byId("reopen-review-deadline-row").hidden = !data.deadline;
        byId("reopen-existing-review").hidden = canReview; byId("reopen-decision-fields").hidden = !canReview; byId("reopen-decision-actions").hidden = !canReview;
        const remark = byId("reopen-review-remark"); remark.value = ""; remark.required = canReview; remark.disabled = !canReview;
        byId("reopen-history-tab").textContent = "History";
        showTab("details");
        byId("reopen-repeat-warning").hidden = true;
        byId("reopen-current-attempt").textContent = "";
        historyUrl = data.historyUrl; history(); bootstrap.Modal.getOrCreateInstance(modal).show();
    });
    byId("reopen-history-retry").addEventListener("click", history);
    byId("reopen-review-form").addEventListener("submit", event => {
        if (activeTab !== "details") { event.preventDefault(); showTab("details", true); return; }
        if (!canReview || reviewButtons.every(button => button.disabled)) { event.preventDefault(); return; }
        submitting = true;
    });
    byId("reopen-review-form").addEventListener("invalid", () => showTab("details"), true);
    modal.addEventListener("hide.bs.modal", event => {
        if (!submitting && byId("reopen-review-remark").value.trim() && !confirm("Close this review and discard your unsaved notes?")) event.preventDefault();
    });
    modal.addEventListener("hidden.bs.modal", () => { generation++; controller?.abort(); });
    modal.addEventListener("shown.bs.modal", () => { const field = byId("reopen-review-remark"); if (!field.disabled) field.focus(); });
})();

(() => {
    const panel = document.querySelector("[data-teaching-live]");
    if (!panel) return;
    const table = panel.querySelector("table");
    const body = table.tBodies[0];
    const message = panel.querySelector("[data-live-status]");
    const endpoint = new URL(panel.dataset.liveUrl, location.href);
    if (endpoint.origin !== location.origin) return;
    let busy = false;
    let active = true;
    let abort;
    let timer;
    let unavailableVersion = document.getElementById("teaching-subject-rows")?.dataset.unavailableCombinations;

    function updateText(current, next) {
        if (!current || !next) return;
        if (current.textContent !== next.textContent) {
            // Text-node edits preserve the shared table's current page and focus.
            if (current.childNodes.length === 1 && current.firstChild.nodeType === Node.TEXT_NODE) {
                current.firstChild.nodeValue = next.textContent;
            } else {
                current.textContent = next.textContent;
            }
        }
        if (current.className !== next.className) current.className = next.className;
    }

    function applyState(state) {
        const incoming = Array.from(state.querySelectorAll("[data-live-rows] > tr"));
        const byId = new Map(Array.from(body.rows).map(row => [row.dataset.requestId, row]));
        let changed = false;
        for (const next of [...incoming].reverse()) {
            const current = byId.get(next.dataset.requestId);
            byId.delete(next.dataset.requestId);
            if (!current) {
                body.prepend(document.importNode(next, true));
                changed = true;
                continue;
            }
            if (current.dataset.requestVersion === next.dataset.requestVersion) continue;
            for (const selector of ["[data-request-status]", "[data-review-notes]", "[data-reviewed-on]", "[data-review-label]"]) {
                updateText(current.querySelector(selector), next.querySelector(selector));
            }
            current.dataset.requestVersion = next.dataset.requestVersion;
            changed = true;
        }
        for (const removed of byId.values()) {
            removed.remove();
            changed = true;
        }
        panel.querySelector("[data-request-count]").textContent = incoming.length
            + (incoming.length === 1 ? " request" : " requests");
        panel.querySelector("[data-request-empty]").classList.toggle("d-none", incoming.length > 0);

        const currentApproved = document.getElementById("teaching-approved-panel");
        const nextApproved = state.querySelector("#teaching-approved-panel");
        if (currentApproved && nextApproved
            && currentApproved.dataset.approvedVersion !== nextApproved.dataset.approvedVersion) {
            currentApproved.replaceWith(document.importNode(nextApproved, true));
            changed = true;
        }
        if (unavailableVersion !== state.dataset.unavailableCombinations) {
            const combinations = JSON.parse(state.dataset.unavailableCombinations || "[]");
            document.dispatchEvent(new CustomEvent("teaching:approvals-updated", { detail: combinations }));
            unavailableVersion = state.dataset.unavailableCombinations;
        }
        if (changed) {
            message.textContent = "Requests updated " + new Date().toLocaleTimeString() + ".";
        } else if (message.dataset.error) {
            message.textContent = "Automatic updates resumed.";
        }
        delete message.dataset.error;
    }

    async function refresh() {
        if (!active || document.hidden || busy) return;
        busy = true;
        abort = new AbortController();
        const timeout = setTimeout(() => abort.abort(), 12000);
        try {
            const response = await fetch(endpoint.href, {
                credentials: "same-origin", cache: "no-store", signal: abort.signal,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok || response.redirected) throw new Error("Session unavailable");
            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const state = page.querySelector("[data-live-state]");
            if (!state || !state.querySelector("[data-live-rows]")) throw new Error("Unexpected response");
            if (active) applyState(state);
        } catch {
            if (active) {
                message.dataset.error = "true";
                message.textContent = "Automatic updates are temporarily unavailable. Retrying shortly.";
            }
        } finally {
            clearTimeout(timeout);
            busy = false;
        }
    }
    function start() {
        clearInterval(timer);
        active = true;
        timer = setInterval(refresh, 5000);
    }
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) refresh(); });
    window.addEventListener("pagehide", () => {
        active = false;
        clearInterval(timer);
        abort?.abort();
    });
    window.addEventListener("pageshow", event => {
        if (event.persisted) {
            start();
            refresh();
        }
    });
    start();
})();

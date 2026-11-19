(() => {
    "use strict";
    const root = document.querySelector("[data-qualification-live-url]");
    if (!root) return;
    const endpoint = new URL(root.dataset.qualificationLiveUrl, location.href);
    if (endpoint.origin !== location.origin) return;
    const list = root.querySelector("[data-qualification-request-list]");
    const table = list?.querySelector("table");
    const body = table?.tBodies[0];
    const requestForm = root.querySelector("#qualification-request-form");
    const review = root.hasAttribute("data-qualification-review");
    const decisionForm = root.querySelector('[data-qualification-decision-form]');
    const initialReviewNotes = decisionForm?.querySelector('textarea')?.value || "";
    const initialReviewCheck = decisionForm?.querySelector('[name="QualificationVerified"]')?.checked || false;
    const message = list?.querySelector(".teaching-live-status") || document.createElement("p");
    if (!list) message.className = "form-text mt-3 mb-0";
    message.setAttribute("role", "status");
    message.setAttribute("aria-live", "polite");
    message.dataset.qualificationLiveStatus = "";
    message.textContent = "";
    message.classList.add("d-none");
    if (!list) {
        const profileSection = root.querySelector(".profile-teaching-card .class-details-section")
            || root.querySelector("[data-qualification-pending-note]")?.closest(".class-details-section");
        if (profileSection) profileSection.append(message);
        else root.querySelector(".page-header")?.after(message);
    }
    let busy = false, active = true, authExpired = false, timer, abort;
    const filter = root.querySelector('[data-qualification-filter]');
    let filterVersion = 0, queuedFilter = false, filterDebounce;
    const filterNames = ['search', 'status', 'from', 'to'];
    if (filter) {
        const query = new URL(location.href).searchParams;
        filterNames.forEach(name => { if(query.has(name)) { filter.elements.namedItem(name).value=query.get(name); endpoint.searchParams.set(name,query.get(name)); } });
        const applyFilter = () => {
            const from = filter.elements.namedItem('from'), to = filter.elements.namedItem('to');
            to.setCustomValidity(from.value && to.value && from.value > to.value ? 'Choose an end date on or after the start date.' : '');
            if(!filter.reportValidity()) return;
            let any = false;
            const address = new URL(location.href);
            filterNames.forEach(name => {
                const value=filter.elements.namedItem(name).value.trim();
                if(value){endpoint.searchParams.set(name,value);address.searchParams.set(name,value);any=true;}
                else {endpoint.searchParams.delete(name);address.searchParams.delete(name);}
            });
            filter.querySelector('[data-qualification-clear]')?.classList.toggle('d-none',!any);
            history.replaceState(null,'',address.href);
            filterVersion++;queuedFilter=true;abort?.abort();refresh();
        };
        filter.addEventListener('submit', event=>{event.preventDefault();event.stopPropagation();clearTimeout(filterDebounce);applyFilter();});
        filter.addEventListener('change', ()=>{clearTimeout(filterDebounce);applyFilter();});
        filter.elements.namedItem('search').addEventListener('input',()=>{clearTimeout(filterDebounce);filterDebounce=setTimeout(applyFilter,300);});
        filter.querySelector('[data-qualification-clear]')?.addEventListener('click',event=>{event.preventDefault();filterNames.forEach(name=>{filter.elements.namedItem(name).value='';});applyFilter();});
    }

    function copyText(current, value) {
        if (!current || current.textContent === value) return false;
        // The shared table observer does not watch character-data edits, so
        // status updates keep its current page and sort selection intact.
        if (current.childNodes.length === 1 && current.firstChild.nodeType === Node.TEXT_NODE)
            current.firstChild.nodeValue = value;
        else current.textContent = value;
        return true;
    }
    function copyElement(current, next) {
        if (!current || !next) return;
        copyText(current, next.textContent);
        if (current.className !== next.className) current.className = next.className;
    }
    function applyRows(incoming) {
        if (!body) return;
        const existing = new Map(Array.from(body.rows, row => [row.dataset.requestId, row]));
        for (const next of [...incoming].reverse()) {
            const current = existing.get(next.dataset.requestId);
            existing.delete(next.dataset.requestId);
            if (!current) { body.prepend(document.importNode(next, true)); continue; }
            if (current.dataset.requestVersion === next.dataset.requestVersion) continue;
            for (const selector of ["[data-qualification-status]", "[data-qualification-notes]", "[data-qualification-review-label]"])
                copyElement(current.querySelector(selector), next.querySelector(selector));
            for (const selector of ["[data-qualification-cancel]", "[data-qualification-no-action]"]) {
                const control = current.querySelector(selector), replacement = next.querySelector(selector);
                if (control && replacement) control.classList.toggle("d-none", replacement.classList.contains("d-none"));
            }
            current.dataset.requestVersion = next.dataset.requestVersion;
            current.dataset.requestStatus = next.dataset.requestStatus;
        }
        for (const removed of existing.values()) removed.remove();
        copyText(list.querySelector("[data-qualification-count]"), `${incoming.length} request(s)`);
        const limit = list.querySelector("[data-qualification-limit]");
        if (limit) limit.classList.toggle("d-none", incoming.length < Number(limit.dataset.qualificationLimit));
        list.querySelector("[data-qualification-empty]")?.classList.toggle("d-none", incoming.length > 0);
        copyText(list.querySelector("[data-qualification-empty]"), filter && filterNames.some(name=>endpoint.searchParams.has(name)) ? "No requests match your filters." : "No requests have been submitted yet.");
    }
    function applyProfile(profile) {
        if (!profile) return;
        root.querySelector("[data-qualification-new]")?.classList.toggle("d-none", !!profile.pending);
        root.querySelector("[data-qualification-pending-list]")?.classList.toggle("d-none", !profile.pending);
        for (const name of ["qualification", "fieldOfStudy", "areaOfExpertise"])
            root.querySelectorAll(`[data-qualification-field='${name}']`).forEach(node => copyText(node, profile[name]?.trim() || "Not provided"));
        root.querySelectorAll("[data-qualification-verified]").forEach(badge => {
            copyText(badge, profile.verified ? "Verified" : "Not verified");
            if (badge.classList.contains("profile-verification-badge")) badge.classList.toggle("is-verified", profile.verified);
            else {
                badge.classList.toggle("tutor-badge-success", profile.verified);
                badge.classList.toggle("qualification-badge-neutral", !profile.verified);
            }
        });
        root.querySelectorAll("[data-qualification-pending-note]").forEach(note => {
            note.classList.toggle("d-none", !profile.pending);
            copyText(note.querySelector("[data-qualification-pending-copy]"), profile.pending
                ? `${profile.pending.qualification} · ${profile.pending.fieldOfStudy}` : "");
        });
        if (requestForm) {
            const pending = !!profile.pending;
            requestForm.dataset.pending = String(pending);
            // Only toggle availability. Never replace an input, its value,
            // its validation message, or the user's selected FileList.
            requestForm.querySelectorAll("input:not([type='hidden']), textarea, button[type='submit']")
                .forEach(control => { control.disabled = pending; });
            requestForm.querySelector("[data-qualification-pending-form]")?.classList.toggle("d-none", !pending);
            requestForm.dispatchEvent(new Event("qualification:availability"));
        }
        if (!Array.isArray(profile.documents)) return;
        root.querySelectorAll("[data-qualification-document]").forEach(card => {
            const document = profile.documents.find(file => file.kind === Number(card.dataset.qualificationDocument));
            copyText(card.querySelector("[data-qualification-file-name]"), document?.fileName || "Not uploaded");
            const link = card.querySelector("[data-qualification-file-link]");
            if (link) {
                link.classList.toggle("d-none", !document);
                if (document) {
                    const target = new URL(card.dataset.documentUrl, location.href);
                    if (target.origin === location.origin) { target.searchParams.set("id", document.id); link.href = target.href; }
                }
            }
            card.querySelector("[data-qualification-no-file]")?.classList.toggle("d-none", !!document);
        });
    }
    function applyReview(incoming) {
        if (!review) return;
        const current = incoming[0];
        const status = current?.querySelector("[data-qualification-status]");
        copyElement(root.querySelector("[data-qualification-review-status]"), status);
        if (!current || current.dataset.requestStatus === "0") return;
        const draft = decisionForm?.querySelector('textarea')?.value || "";
        const checked = decisionForm?.querySelector('[name="QualificationVerified"]')?.checked || false;
        const dirty = !!decisionForm && (draft !== initialReviewNotes || checked !== initialReviewCheck);
        decisionForm?.querySelectorAll("input:not([type='hidden']), textarea, button").forEach(control => { control.disabled = true; });
        decisionForm?.classList.add('d-none');
        root.querySelector('[data-qualification-decision-result]')?.classList.remove('d-none');
        copyElement(root.querySelector('[data-qualification-decision-status]'), status);
        copyText(root.querySelector('[data-qualification-decision-time]'), current.dataset.reviewedAt || "—");
        const notes = current.querySelector('[data-qualification-notes]')?.textContent;
        copyText(root.querySelector('[data-qualification-decision-notes]'), notes && notes !== "—" ? notes : "No review notes.");
        const notice = root.querySelector("[data-qualification-review-notice]");
        if (notice) {
            notice.classList.toggle('d-none', !dirty);
            copyText(notice, dirty ? "This request was processed while you were reviewing it. Your unsaved changes were not submitted." : "");
        }
        const draftPanel = root.querySelector('[data-qualification-review-draft]');
        draftPanel?.classList.toggle('d-none', !dirty || !draft.trim());
        copyText(draftPanel?.querySelector('p'), draft);
    }
    async function refresh() {
        if (!active || authExpired || busy || document.hidden) return;
        busy = true;
        queuedFilter = false;
        const version = filterVersion;
        abort = new AbortController();
        const timeout = setTimeout(() => abort.abort(), 12000);
        try {
            const response = await fetch(endpoint.href, { credentials: "same-origin", cache: "no-store",
                headers: { "X-Requested-With": "XMLHttpRequest" }, signal: abort.signal });
            if (!active || version !== filterVersion) return;
            if (response.redirected || response.status === 401 || response.status === 403) {
                authExpired = true;
                clearInterval(timer);
                copyText(message, "Automatic updates stopped. Sign in again to continue.");
                message.classList.remove("d-none");
                return;
            }
            if (!response.ok) throw new Error("Update unavailable");
            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const state = page.querySelector("[data-qualification-live-state]");
            if (!state || !state.querySelector("tbody")) throw new Error("Unexpected response");
            const profile = JSON.parse(state.dataset.profile);
            const incoming = Array.from(state.querySelectorAll("tbody > tr"));
            const ids = incoming.map(row => row.dataset.requestId);
            if (new Set(ids).size !== ids.length || incoming.some(row => !/^\d+$/.test(row.dataset.requestId) || !/^[0-3]$/.test(row.dataset.requestStatus)))
                throw new Error("Unexpected rows");
            if (!active || version !== filterVersion) return;
            applyRows(incoming);
            applyProfile(profile);
            applyReview(incoming);
            if (root.hasAttribute("data-qualification-details")) {
                const row = incoming[0];
                copyElement(root.querySelector("[data-qualification-review-status]"), row?.querySelector("[data-qualification-status]"));
                const pending = row?.dataset.requestStatus === "0";
                copyText(root.querySelector('[data-qualification-detail-time]'),row?.dataset.reviewedAt || "—");
                copyText(root.querySelector("[data-qualification-detail-notes]"), row?.querySelector("[data-qualification-notes]")?.textContent || (pending ? "Awaiting review" : "No review notes."));
                root.querySelector("[data-qualification-detail-cancel]")?.classList.toggle("d-none", !pending);
            }
            copyText(message, "");
            message.classList.add("d-none");
            delete message.dataset.error;
        } catch {
            if (active && !authExpired && version === filterVersion) {
                message.dataset.error = "true";
                message.classList.remove("d-none");
            }
        } finally { clearTimeout(timeout); busy = false; if(queuedFilter && active && !authExpired) refresh(); }
    }
    function start() { active = true; clearInterval(timer); timer = setInterval(refresh, 5000); }
    root.addEventListener("submit", event => {
        if (event.target.matches("[data-qualification-filter]")) return;
        if (!event.defaultPrevented) { active = false; clearInterval(timer); abort?.abort(); }
    });
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) refresh(); });
    window.addEventListener("pagehide", () => { active = false; clearInterval(timer); abort?.abort(); });
    window.addEventListener("pageshow", event => { if (event.persisted && !authExpired) { start(); refresh(); } });
    start();
    refresh();
})();

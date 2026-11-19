(() => {
    const panel = document.querySelector("[data-teaching-live]");
    if (!panel) return;
    const table = panel.querySelector("table");
    const body = table?.tBodies[0];
    const message = panel.querySelector("[data-live-status]") || document.createElement('p');
    if(!message.parentElement){message.className='form-text d-none';message.setAttribute('role','status');message.setAttribute('aria-live','polite');message.dataset.liveStatus='';panel.prepend(message);}
    const endpoint = new URL(panel.dataset.liveUrl, location.href);
    if (endpoint.origin !== location.origin) return;
    let busy = false;
    let active = true;
    let abort;
    let timer, authExpired=false, filterVersion=0, queuedFilter=false, debounce;
    const filter=document.querySelector('[data-teaching-filter]');
    const names=['search','status','from','to'];
    if(filter){
        const params=new URL(location.href).searchParams;
        names.forEach(name=>{if(params.has(name)){filter.elements.namedItem(name).value=params.get(name);endpoint.searchParams.set(name,params.get(name));}});
        function applyFilter(){
            const from=filter.elements.namedItem('from'),to=filter.elements.namedItem('to');
            to.setCustomValidity(from.value&&to.value&&from.value>to.value?'Choose an end date on or after the start date.':'');
            if(!filter.reportValidity())return;
            const address=new URL(location.href);let any=false;
            names.forEach(name=>{const value=filter.elements.namedItem(name).value.trim();if(value){any=true;endpoint.searchParams.set(name,value);address.searchParams.set(name,value);}else{endpoint.searchParams.delete(name);address.searchParams.delete(name);}});
            filter.querySelector('[data-teaching-clear]')?.classList.toggle('d-none',!any);
            history.replaceState(null,'',address.href);filterVersion++;queuedFilter=true;abort?.abort();refresh();
        }
        filter.addEventListener('submit',event=>{event.preventDefault();clearTimeout(debounce);applyFilter();});
        filter.addEventListener('change',()=>{clearTimeout(debounce);applyFilter();});
        filter.elements.namedItem('search').addEventListener('input',()=>{clearTimeout(debounce);debounce=setTimeout(applyFilter,300);});
        filter.querySelector('[data-teaching-clear]')?.addEventListener('click',event=>{event.preventDefault();names.forEach(name=>{filter.elements.namedItem(name).value='';});applyFilter();});
    }
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
        let changed = false;
        if(body){
        const byId = new Map(Array.from(body.rows).map(row => [row.dataset.requestId, row]));
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

        }
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
        message.textContent="";
        message.classList.add('d-none');
        delete message.dataset.error;
    }

    async function refresh() {
        if (!active || authExpired || document.hidden || busy) return;
        busy = true;queuedFilter=false;const version=filterVersion;
        abort = new AbortController();
        const timeout = setTimeout(() => abort.abort(), 12000);
        try {
            const response = await fetch(endpoint.href, {
                credentials: "same-origin", cache: "no-store", signal: abort.signal,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if(version!==filterVersion || !active)return;
            if(response.redirected || response.status===401 || response.status===403){authExpired=true;clearInterval(timer);message.textContent='Automatic updates stopped. Sign in again to continue.';message.classList.remove('d-none');return;}
            if (!response.ok) throw new Error("Update unavailable");
            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const state = page.querySelector("[data-live-state]");
            if (!state || !state.querySelector("[data-live-rows]")) throw new Error("Unexpected response");
            if (active && version===filterVersion) applyState(state);
        } catch {
            if (active && !authExpired && version===filterVersion) {
                message.classList.remove("d-none");
                message.dataset.error = "true";
            }
        } finally {
            clearTimeout(timeout);
            busy = false;
            if(queuedFilter && active && !authExpired)refresh();
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
        if (event.persisted && !authExpired) {
            start();
            refresh();
        }
    });
    panel.addEventListener('submit',event=>{if(event.target.id==='teaching-request-form'&&!event.defaultPrevented){active=false;clearInterval(timer);abort?.abort();}});
    start();
    refresh();
})();

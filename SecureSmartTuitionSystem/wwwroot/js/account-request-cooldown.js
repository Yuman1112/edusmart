(() => {
    "use strict";
    function initialize() {
    const alert = document.querySelector("[data-account-cooldown]");
    if (!alert) return;
    const message = alert.querySelector("[data-cooldown-message]");
    const startWall = Date.now();
    const startMonotonic = performance.now();
    const waits = Object.fromEntries(["password", "send", "verify"].map(operation =>
        [operation, Math.max(0, Number(alert.dataset["wait" + operation[0].toUpperCase() + operation.slice(1)]) || 0)]));
    const forms = Array.from(document.querySelectorAll("form[data-request-limit]"));
    const buttons = forms.flatMap(form => Array.from(form.elements)
        .filter(element => element.tagName === "BUTTON" && element.type === "submit")
        // A Cancel button may target a separate, unrestricted form.
        .filter(button => button.form === form)
        .map(button => ({button, operations: form.dataset.requestLimit.split(/\s+/),
            originalHtml: button.innerHTML, originallyDisabled: button.disabled, active: false})));
    const operations = new Set(buttons.flatMap(entry => entry.operations));
    const inlineSend = !!alert.closest("[data-email-inline-send='true']");
    let hadWait = false;
    let hadVisibleWait = false;
    function remaining(operation) {
        const elapsed = Math.max(Date.now() - startWall, performance.now() - startMonotonic, 0) / 1000;
        return Math.max(0, Math.ceil(waits[operation] - elapsed));
    }
    function update() {
        let longest = 0, limited = null;
        for (const operation of operations) {
            const wait = remaining(operation);
            if (wait > longest) { longest = wait; limited = operation; }
        }
        for (const entry of buttons) {
            const wait = Math.max(0, ...entry.operations.map(remaining));
            if (wait > 0) {
                entry.active = true;
                entry.button.disabled = true;
                entry.button.setAttribute("aria-disabled", "true");
                entry.button.textContent = `${entry.button.dataset.cooldownPrefix || "Try again in"} ${wait}s`;
            } else if (entry.active) {
                entry.active = false;
                entry.button.disabled = entry.originallyDisabled;
                entry.button.removeAttribute("aria-disabled");
                entry.button.innerHTML = entry.originalHtml;
            }
        }
        if (longest > 0) {
            hadWait = true;
            alert.hidden = inlineSend && limited === "send";
            if (!alert.hidden) hadVisibleWait = true;
            const action = limited === "send" ? "requesting another verification email or code"
                : limited === "password" ? "submitting your current password again" : "submitting another verification code";
            message.textContent = `Please wait ${longest} seconds before ${action}.`;
        } else if (hadWait && (!inlineSend || hadVisibleWait)) {
            alert.hidden = false;
            message.textContent = "You can try again now.";
        } else alert.hidden = true;
    }
    forms.forEach(form => form.addEventListener("submit", event => {
        const target = event.submitter?.form || form;
        if (target !== form) return;
        if (form.dataset.requestLimit.split(/\s+/).some(operation => remaining(operation) > 0)) {
            event.preventDefault(); update();
        }
    }));
    update();
    const timer = window.setInterval(update, 250);
    window.addEventListener("pageshow", update);
    document.addEventListener("visibilitychange", update);
    return () => { clearInterval(timer); window.removeEventListener("pageshow", update); document.removeEventListener("visibilitychange", update); };
    }
    let cleanup = initialize();
    document.addEventListener("email:flow-reset", () => { cleanup?.(); cleanup = initialize(); });
})();

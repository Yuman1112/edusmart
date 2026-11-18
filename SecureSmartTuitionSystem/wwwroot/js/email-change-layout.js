(() => {
    "use strict";
    const root = document.querySelector("[data-email-flow]");
    const details = root?.querySelector("[data-email-address-change]");
    const panel = root?.querySelector("[data-email-verification-panel]");
    const summary = details?.querySelector("summary");
    const back = details?.querySelector("[data-email-return]");
    if (!details || !panel || !summary || !back) return;
    let previous;
    function update() {
        const open = details.open;
        if (open === previous) return;
        const initial = previous === undefined;
        previous = open;
        panel.hidden = false;
        summary.setAttribute("aria-expanded", String(open));
        if (initial) return;
        if (open) details.querySelector("input[name='NewEmail']")?.focus();
        else {
            const input = panel.querySelector("#otpCode");
            if (input) input.focus();
            else summary.focus();
        }
    }
    summary.addEventListener("click", event => {
        event.preventDefault();
        details.open = !details.open;
        update();
    });
    back.addEventListener("click", () => {
        details.open = false;
        update();
    });
    details.addEventListener("toggle", update);
    update();
})();

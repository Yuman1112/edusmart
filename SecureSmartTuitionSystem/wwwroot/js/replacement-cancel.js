"use strict";
(() => {
    document.querySelectorAll(".replacement-action-cell").forEach(cell => {
        const buttons = cell.querySelector(".replacement-action-buttons");
        const opener = cell.querySelector("[data-open-replacement-cancel]");
        const form = cell.querySelector(".replacement-cancel-form");
        const reason = form?.querySelector('[name="reason"]');
        if (!buttons || !opener || !form || !reason) return;
        const validate = () => reason.setCustomValidity(reason.value.trim() ? "" : "Please enter a cancellation reason.");
        reason.addEventListener("input", validate);
        validate();
        opener.addEventListener("click", () => {
            buttons.hidden = true; form.hidden = false;
            opener.setAttribute("aria-expanded", "true"); reason.focus();
        });
        form.addEventListener("submit", event => {
            validate();
            if (!reason.checkValidity()) {
                event.preventDefault(); reason.reportValidity(); reason.focus();
            }
        });
        form.querySelector("[data-keep-replacement]")?.addEventListener("click", () => {
            reason.value = ""; validate(); form.hidden = true; buttons.hidden = false;
            opener.setAttribute("aria-expanded", "false"); opener.focus();
        });
    });
})();

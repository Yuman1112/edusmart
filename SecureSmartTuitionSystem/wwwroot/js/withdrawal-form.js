"use strict";
(() => {
    const form = document.getElementById("withdrawalForm");
    if (!form) return;
    const child = document.getElementById("withdrawalChild");
    const enrollment = document.getElementById("exitEnrollment");
    const reason = document.getElementById("exitReason");
    const filter = () => {
        for (const option of enrollment.options) {
            if (!option.value) continue;
            const available = !child || option.dataset.studentId === child.value;
            option.hidden = !available;
            option.disabled = !available;
        }
        if (enrollment.selectedOptions[0]?.disabled) enrollment.value = "";
        enrollment.disabled = !!child && !child.value;
    };
    child?.addEventListener("change", filter);
    const validateReason = () => reason.setCustomValidity(reason.value.trim() ? "" : "Please enter a withdrawal reason.");
    reason.addEventListener("input", validateReason);
    filter(); validateReason();
    form.addEventListener("submit", event => {
        validateReason();
        if (!form.checkValidity()) { event.preventDefault(); form.reportValidity(); }
    });
})();

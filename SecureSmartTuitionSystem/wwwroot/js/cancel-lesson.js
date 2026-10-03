"use strict";
(() => {
    const form = document.getElementById("cancelLessonForm");
    const reason = form?.querySelector('[name="Reason"]');
    if (!form || !reason) return;
    const validate = () => {
        reason.setCustomValidity(reason.value.trim() ? "" : "Please enter a cancellation reason.");
    };
    reason.addEventListener("input", validate);
    validate();
    form.addEventListener("submit", event => {
        validate();
        if (!reason.checkValidity()) {
            event.preventDefault();
            reason.reportValidity();
            reason.focus();
        }
    });
})();

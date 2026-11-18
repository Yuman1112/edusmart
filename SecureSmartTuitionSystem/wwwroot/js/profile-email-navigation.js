(() => {
    "use strict";
    const form = document.querySelector("[data-profile-edit-form]");
    const dialog = document.getElementById("profile-email-prompt");
    if (!form || !dialog) return;
    // Only compare editable personal fields; never store profile data in browser storage.
    const snapshot = () => JSON.stringify(Array.from(form.elements)
        .filter(el => el.name && !el.disabled && !el.readOnly &&
            !["hidden", "submit", "button", "reset"].includes(el.type))
        .map(el => [el.name, el.type === "checkbox" || el.type === "radio" ? el.checked : el.value]));
    const initial = snapshot();
    const serverErrors = form.dataset.profileHasErrors === "true" || form.dataset.hasErrors === "true";
    let selectedLink = null;
    let destination = "email";
    let restoreFocus = true;
    const copy = dialog.querySelector("[data-profile-prompt-copy]");
    const status = document.querySelector("[data-profile-unsaved]");
    const hasChanges = () => serverErrors || snapshot() !== initial;
    const updateStatus = () => { if (status) status.hidden = !hasChanges(); };
    form.addEventListener("input", updateStatus);
    form.addEventListener("change", updateStatus);
    updateStatus();
    const continuation = document.createElement("button");
    continuation.type = "submit";
    continuation.name = "continueToEmail";
    continuation.value = "true";
    continuation.hidden = true;
    form.append(continuation);
    document.querySelectorAll("[data-profile-email-link], [data-profile-destination='teaching'], [data-profile-destination='qualification']").forEach(link => {
        link.addEventListener("click", event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !hasChanges()) return;
            event.preventDefault();
            selectedLink = link;
            restoreFocus = true;
            destination = ["teaching", "qualification"].includes(link.dataset.profileDestination) ? link.dataset.profileDestination : "email";
            if (copy) copy.textContent = destination === "qualification"
                ? "You have unsaved changes. Save them before opening Qualification Requests."
                : destination === "teaching"
                ? "You have unsaved personal information. Save it before opening Teaching Requests."
                : "You have unsaved changes. Save them before continuing to change your email.";
            dialog.showModal();
        });
    });
    dialog.querySelector("[data-profile-stay]").addEventListener("click", () => dialog.close());
    dialog.querySelector("[data-profile-save-continue]").addEventListener("click", () => {
        restoreFocus = false;
        dialog.close();
        // Reuse the existing validation and normal POST; redirect only after server-side success.
        if (!form.reportValidity()) return;
        if (window.jQuery?.fn.valid && !window.jQuery(form).valid()) return;
        // Keep the existing four-role email POST contract; Tutor's teaching destination is allowlisted server-side.
        continuation.name = destination !== "email" ? "continueTo" : "continueToEmail";
        continuation.value = destination !== "email" ? destination : "true";
        form.requestSubmit(continuation);
    });
    dialog.addEventListener("close", () => { if (restoreFocus) selectedLink?.focus(); });
})();

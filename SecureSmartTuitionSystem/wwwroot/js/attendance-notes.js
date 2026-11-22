(() => {
    "use strict";
    const form = document.getElementById("attendance-sheet-form");
    if (!form) return;
    form.querySelectorAll("[data-attendance-note]").forEach(button => {
        const panel = document.getElementById(button.dataset.attendanceNote);
        if (!panel) return;
        const field = panel.querySelector('input[name$=".Remark"]');
        const canEdit = button.dataset.noteEditable === "true";
        function label() {
            button.textContent = !panel.hidden ? (canEdit && !field?.disabled ? "Done" : "Close")
                : canEdit && !field?.disabled ? (field?.value.trim() ? "Edit note" : "+ Add note") : "View note";
            button.setAttribute("aria-expanded", String(!panel.hidden));
        }
        button.addEventListener("click", () => {
            panel.hidden = !panel.hidden;
            label();
            if (!panel.hidden && field && !field.disabled) field.focus();
        });
        label();
    });
})();

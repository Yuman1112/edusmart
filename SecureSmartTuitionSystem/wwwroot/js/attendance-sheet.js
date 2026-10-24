(() => {
    "use strict";
    const form = document.getElementById("attendance-sheet-form");
    const draft = document.getElementById("attendance-save-draft");
    const submit = document.getElementById("attendance-final-submit");
    if (!form || !draft || !submit) return;
    const fields = [...form.querySelectorAll('select[name$=".Status"], input[name$=".Remark"]')];
    const snapshot = () => JSON.stringify(fields.map(field => field.value));
    const original = snapshot();
    const endsAt = Date.parse(form.dataset.endsAt);
    let locked = false;
    const update = () => { draft.disabled = locked || snapshot() === original; };
    const checkEnd = () => {
        if (Date.now() < endsAt || locked) return;
        locked = true;
        fields.forEach(field => { field.disabled = true; });
        draft.disabled = true; submit.disabled = true;
        document.getElementById("attendance-state").textContent = "The lesson has ended. Attendance is now locked. Unsaved changes were not saved.";
    };
    form.addEventListener("input", update);
    form.addEventListener("change", update);
    form.addEventListener("submit", event => {
        checkEnd();
        if (locked) { event.preventDefault(); return; }
        if (event.submitter === submit) {
            const missing = fields.find(field => field.tagName === "SELECT" && !field.value);
            if (missing) { event.preventDefault(); alert("Select attendance for every student before submitting."); missing.focus(); return; }
            if (!confirm("Submit and lock this attendance sheet? You will no longer be able to edit it directly.")) event.preventDefault();
        }
    });
    checkEnd();
    const timer = setInterval(checkEnd, 1000);
    window.addEventListener("pagehide", () => clearInterval(timer), { once: true });
})();
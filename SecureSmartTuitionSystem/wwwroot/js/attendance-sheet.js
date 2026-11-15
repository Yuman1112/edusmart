(() => {
    "use strict";
    const form = document.getElementById("attendance-sheet-form");
    if (!form) return;
    const $ = id => document.getElementById(id);
    const rows = [...form.querySelectorAll("[data-attendance-row]")];
    const editable = form.dataset.editable === "true";
    const draft = $("attendance-save-draft"), submit = $("attendance-final-submit");
    const search = $("attendance-search"), statusFilter = $("attendance-status-filter");
    const pageCheck = $("attendance-select-page"), toolbar = $("attendance-bulk-toolbar");
    const fields = [...form.querySelectorAll('select[name$=".Status"], input[name$=".Remark"], textarea[name="LateSubmissionReason"]')];
    const snapshot = () => JSON.stringify(fields.map(f => f.value));
    const original = snapshot();
    let locked = false, page = 1, visible = [], filtered = [];
    const size = 25;
    const status = row => row.querySelector('select[name$=".Status"]')?.value ?? row.dataset.attendanceStatus ?? "";
    const check = row => row.querySelector(".attendance-student-check");
    const selected = () => rows.filter(row => check(row)?.checked);
    const feedback = message => { const box = $("attendance-form-feedback"); if (box) { box.textContent = message; box.hidden = !message; } };
    function selection() {
        const count = selected().length;
        if (toolbar) toolbar.hidden = count === 0;
        if ($("attendance-selected-count")) $("attendance-selected-count").textContent = `${count} student(s) selected`;
        if (pageCheck) {
            const marked = visible.filter(row => check(row)?.checked).length;
            pageCheck.checked = visible.length > 0 && marked === visible.length;
            pageCheck.indeterminate = marked > 0 && marked < visible.length;
            pageCheck.disabled = locked || visible.length === 0;
        }
    }
    function totals() {
        for (const label of form.querySelectorAll("[data-total]")) label.textContent = String(rows.filter(row => status(row) === label.dataset.total).length);
        if (draft) draft.disabled = locked || snapshot() === original;
    }
    function draw() {
        const query = (search?.value || "").trim().toLocaleLowerCase();
        const filter = statusFilter?.value ?? "all";
        filtered = rows.filter(row => (!query || row.dataset.studentSearch.toLocaleLowerCase().includes(query)) && (filter === "all" || status(row) === filter));
        const pages = Math.max(1, Math.ceil(filtered.length / size));
        page = Math.max(1, Math.min(page, pages));
        visible = filtered.slice((page - 1) * size, page * size);
        rows.forEach(row => { row.hidden = !visible.includes(row); });
        if ($("attendance-no-matches")) $("attendance-no-matches").hidden = filtered.length !== 0;
        const nav = $("attendance-roster-pagination");
        if (nav) {
            nav.hidden = filtered.length <= size;
            nav.replaceChildren();
            if (!nav.hidden) {
                const summary = document.createElement("span"); summary.className = "table-pagination-summary";
                summary.textContent = `${(page - 1) * size + 1}–${Math.min(page * size, filtered.length)} of ${filtered.length} students`;
                const controls = document.createElement("div"); controls.className = "table-pagination-controls";
                function button(text, target, disabled, current = false) {
                    const b = document.createElement("button"); b.type = "button"; b.className = `table-page-button${current ? " active" : ""}`; b.textContent = text; b.disabled = disabled;
                    if (current) b.setAttribute("aria-current", "page");
                    b.addEventListener("click", () => { page = target; draw(); }); controls.append(b);
                }
                button("Previous", page - 1, page === 1);
                for (let n = Math.max(1, page - 2); n <= Math.min(pages, page + 2); n++) button(String(n), n, n === page, n === page);
                button("Next", page + 1, page === pages); nav.append(summary, controls);
            }
        }
        selection(); totals();
    }
    function checkEnd() {
        if (!editable) return;
        if ($("attendance-late-reason") && Date.now() >= Date.parse(form.dataset.lessonEndsAt)) $("attendance-late-reason").hidden = false;
        if (locked || Date.now() < Date.parse(form.dataset.endsAt)) return;
        locked = true;
        fields.forEach(field => { field.disabled = true; });
        form.querySelectorAll('.attendance-student-check, [data-attendance-bulk], #attendance-select-all, #attendance-clear-selection').forEach(field => { field.disabled = true; });
        if (draft) draft.disabled = true;
        if (submit) submit.disabled = true;
        selection();
        $("attendance-state").textContent = "The entry deadline has passed. Attendance is now locked. Unsaved changes were not saved.";
    }
    search?.addEventListener("input", () => { page = 1; draw(); });
    statusFilter?.addEventListener("change", () => { page = 1; draw(); });
    pageCheck?.addEventListener("change", () => { checkEnd(); if (locked) return; visible.forEach(row => { check(row).checked = pageCheck.checked; }); selection(); });
    $("attendance-select-all")?.addEventListener("click", () => { checkEnd(); if (locked) return; rows.forEach(row => { check(row).checked = true; }); selection(); });
    $("attendance-clear-selection")?.addEventListener("click", () => { rows.forEach(row => { check(row).checked = false; }); selection(); });
    form.querySelectorAll("[data-attendance-bulk]").forEach(button => button.addEventListener("click", () => {
        checkEnd(); if (locked) return;
        const chosen = selected();
        chosen.forEach(row => { row.querySelector('select[name$=".Status"]').value = button.dataset.attendanceBulk; check(row).checked = false; });
        feedback(`Attendance updated for ${chosen.length} student(s). Save a draft or submit to keep these changes.`); draw();
    }));
    form.addEventListener("change", event => {
        if (event.target.matches(".attendance-student-check")) selection();
        else if (event.target.matches('select[name$=".Status"]')) draw();
        else totals();
    });
    form.addEventListener("input", totals);
    form.addEventListener("submit", event => {
        if (!editable) { event.preventDefault(); return; }
        checkEnd(); if (locked) { event.preventDefault(); return; }
        if (event.submitter !== submit) return;
        const missing = rows.filter(row => !status(row));
        if (missing.length) {
            event.preventDefault(); search.value = ""; statusFilter.value = ""; page = 1; draw();
            feedback(`${missing.length} student(s) still need attendance. Select Present, Absent or Late before submitting.`);
            missing[0].querySelector("select")?.focus(); return;
        }
        const reason = form.querySelector('[name="LateSubmissionReason"]');
        if (form.dataset.approvedReason !== "true" && Date.now() >= Date.parse(form.dataset.lessonEndsAt) && !reason?.value.trim()) {
            event.preventDefault(); $("attendance-late-reason").hidden = false;
            feedback("Enter a late submission reason before submitting."); reason?.focus(); return;
        }
        const count = value => rows.filter(row => status(row) === value).length;
        if (!confirm(`Submit and lock attendance?\nPresent: ${count("1")} · Absent: ${count("2")} · Late: ${count("3")}\nYou will no longer be able to edit it directly.`)) event.preventDefault();
    });
    form.addEventListener("keydown", event => { if (event.key === "Enter" && event.target.matches('input[type="search"]')) event.preventDefault(); });
    draw(); checkEnd();
    if (editable) { const timer = setInterval(checkEnd, 1000); window.addEventListener("pagehide", () => clearInterval(timer), { once: true }); }
})();

(() => {
    "use strict";

    const form = document.getElementById("tutor-availability-form");
    if (!form) return;

    const saveButton = document.getElementById("saveTutorAvailability");
    if (!saveButton) return;

    // Include all pages, because pagination only hides the rows.
    const boxes = Array.from(
        form.querySelectorAll('input[name="SelectedSlots"]')
    );

    const selected = () =>
        boxes
            .filter(box => box.checked)
            .map(box => Number(box.value));

    const signature = () =>
        selected().sort((a, b) => a - b).join(",")
        + "|legacy:" + Boolean(form.querySelector('input[name="RemoveLegacySlots"]:checked'));

    let original = signature();

    let saveFailed = form.dataset.saveFailed === "true";

    const hasChanges = () =>
        saveFailed || signature() !== original;

    let submitting = false;
    let saving = false;
    const saveResult = form.querySelector('[data-availability-save-result]');
    function showResult(message, kind = 'danger') {
        if (!saveResult) return;
        saveResult.textContent = message;
        saveResult.className = 'alert alert-' + kind;
    }

    const hours = document.getElementById("availability-hours");
    const source = document.getElementById("availability-source");
    const target = document.getElementById("availability-target");
    const message = document.getElementById("availability-tool-message");
    const total = document.getElementById("availability-total");
    const state = document.getElementById("availability-save-state");

    const rows = Array.from(
        form.querySelectorAll("tr[data-slot]")
    );

    const time = minutes =>
        `${String(Math.floor(minutes / 60)).padStart(2, "0")}:` +
        `${String(minutes % 60).padStart(2, "0")}`;

    function describe(day, values) {
        const slots = values
            .filter(value => Math.floor(value / 48) === day)
            .map(value => value % 48)
            .sort((a, b) => a - b);

        const ranges = [];

        for (let i = 0; i < slots.length; i++) {
            const start = slots[i];
            let end = start;

            while (
                i + 1 < slots.length &&
                slots[i + 1] === end + 1
            ) {
                end = slots[++i];
            }

            ranges.push(
                `${time(start * 30)}–${time((end + 1) * 30)}`
            );
        }

        return ranges.length
            ? ranges.join(", ")
            : "Not available";
    }

    const dailyTotals = document.getElementById("availability-day-totals");
    const limitMessage = document.getElementById("availability-limit-message");
    const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    const maxDailySlots = 16;
    function countsByDay(values) {
        const counts = Array(7).fill(0);
        values.forEach(value => counts[Math.floor(value / 48)]++);
        return counts;
    }

    function update() {
        const values = selected();
        const changed = hasChanges();

        const counts = countsByDay(values);
        const overLimit = counts.map((count, day) => count > maxDailySlots ? dayNames[day] : null).filter(Boolean);
        saveButton.disabled = values.length > 90 || saving || form.dataset.stale === "true" || !changed || overLimit.length > 0;
        boxes.forEach(box => {
            box.disabled = !box.checked && (counts[Math.floor(Number(box.value) / 48)] >= maxDailySlots || values.length >= 90);
        });
        if (dailyTotals) {
            dailyTotals.replaceChildren(...dayNames.slice(1).map((day, index) => {
                const chip = document.createElement("span");
                chip.className = "availability-day-total" + (counts[index + 1] > maxDailySlots ? " is-over-limit" : "");
                chip.textContent = `${day}: ${counts[index + 1] / 2} / 8 h`;
                chip.dataset.dailyTotal = String(index + 1);
                return chip;
            }));
        }
        if (limitMessage) {
            limitMessage.hidden = overLimit.length === 0 && values.length <= 90;
            limitMessage.textContent = values.length > 90 ? "Select no more than 45 hours per week." : overLimit.length ? `Select no more than eight hours per day. Review: ${overLimit.join(", ")}.` : "";
        }

        form.querySelectorAll("[data-summary-day]").forEach(cell => {
            cell.textContent = describe(
                Number(cell.dataset.summaryDay),
                values
            );
        });

        if (total) {
            total.textContent =
                `${values.length / 2} hours per week`;
        }

        if (state) {
            state.textContent = changed
                ? "You have unsaved changes. Select Save Availability to keep them."
                : "No unsaved changes.";
        }
    }

    function showHours() {
        if (!hours) return;

        rows.forEach(row => {
            const slot = Number(row.dataset.slot);

            row.hidden =
                hours.value !== "all" &&
                (slot < 16 || slot >= 46);
        });
    }

    function revealSelectedOutsideDay() {
        if (
            hours &&
            selected().some(value =>
                value % 48 < 16 || value % 48 >= 46
            )
        ) {
            hours.value = "all";
        }

        showHours();
    }

    // Optional tools: the basic timetable also works without them.
    const tools = document.getElementById("availability-tools");

    if (tools) {
        tools.hidden = false;
    }

    hours?.addEventListener("change", showHours);

    form.addEventListener("change", event => {
        if (
            event.target.matches('input[name="SelectedSlots"], input[name="RemoveLegacySlots"]')
        ) {
            update();
        }
    });

    document.getElementById("availability-copy")
        ?.addEventListener("click", () => {
            if (!source || !target) return;

            if (source.value === target.value) {
                if (message) {
                    message.textContent =
                        "Choose a different target day.";
                }

                return;
            }

            const copied = new Set(
                selected()
                    .filter(value =>
                        Math.floor(value / 48) === Number(source.value)
                    )
                    .map(value => value % 48)
            );

            boxes
                .filter(box =>
                    Math.floor(Number(box.value) / 48) ===
                    Number(target.value)
                )
                .forEach(box => {
                    box.checked = copied.has(Number(box.value) % 48);
                });

            if (message) {
                message.textContent =
                    "Day copied. Save Availability to keep your changes.";
            }

            revealSelectedOutsideDay();
            update();
        });

    document.getElementById("availability-clear")
        ?.addEventListener("click", () => {
            if (!source) return;

            boxes
                .filter(box =>
                    Math.floor(Number(box.value) / 48) ===
                    Number(source.value)
                )
                .forEach(box => {
                    box.checked = false;
                });

            if (message) {
                message.textContent =
                    "Source day cleared. Save Availability to keep your changes.";
            }

            update();
        });

    document.addEventListener('availability:stale', update);
    form.addEventListener("submit", async event => {
        if (event.defaultPrevented) return;
        if (selected().length > 90 || saving || form.dataset.stale === 'true' || !hasChanges() || countsByDay(selected()).some(count => count > maxDailySlots)) {
            event.preventDefault(); update(); return;
        }
        if (!window.fetch || !window.FormData) { submitting = true; return; }
        event.preventDefault();
        if (!form.reportValidity()) return;
        const submittedSignature = signature();
        const body = new FormData(form);
        saving = true; form.dataset.saving = 'true'; update();
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        try {
            const response = await fetch(form.action, {method:'POST', body, credentials:'same-origin', cache:'no-store', signal:controller.signal, headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (response.redirected || response.status === 401 || response.status === 403) throw new Error('Your session may have expired. Sign in again before saving.');
            const result = await response.json();
            if (!response.ok || !result.success) {
                if (result.stale) {
                    form.dataset.stale = 'true';
                    form.querySelector('[data-availability-stale-notice]')?.classList.remove('d-none');
                }
                throw new Error(result.message || 'Availability could not be saved. Your selections have been kept.');
            }
            original = submittedSignature; saveFailed = false;
            form.elements.namedItem('Version').value = result.version;
            form.dataset.stale = 'false';
            form.querySelector('[data-availability-stale-notice]')?.classList.add('d-none');
            showResult(result.message + (result.warning ? ' ' + result.warning : ''), result.warning ? 'warning' : 'success');
            document.dispatchEvent(new Event('availability:saved'));
        } catch (error) {
            showResult(error.name === 'AbortError' ? 'The response timed out. Your selections are kept; check the latest availability before retrying.' : error.message);
        } finally {
            clearTimeout(timeout); saving = false; form.dataset.saving = 'false'; update();
        }
    });

    window.addEventListener("beforeunload", event => {
        if (!submitting && hasChanges()) {
            event.preventDefault();
            event.returnValue = "";
        }
    });

    window.addEventListener("pageshow", () => {
        submitting = false;
        update();
    });

    revealSelectedOutsideDay();
    update();
})();
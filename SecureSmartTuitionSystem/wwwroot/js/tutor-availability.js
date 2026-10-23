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
        selected()
            .sort((a, b) => a - b)
            .join(",");

    const original = signature();

    const hasChanges = () => signature() !== original;

    let submitting = false;

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

    function update() {
        const values = selected();
        const changed = hasChanges();

        saveButton.disabled = !changed;

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
            event.target.matches('input[name="SelectedSlots"]')
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

    form.addEventListener("submit", event => {
        if (!hasChanges()) {
            event.preventDefault();
            update();
            return;
        }

        submitting = true;
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
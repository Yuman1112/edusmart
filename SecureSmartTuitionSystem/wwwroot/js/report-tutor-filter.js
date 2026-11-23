document.addEventListener("DOMContentLoaded", () => {
    "use strict";

    const pickerSelector = "[data-report-tutor-picker]";
    const enhanced = new WeakSet();

    function updateOptions(picker, search) {
        const needle = search.trim().toLocaleLowerCase();
        const options = [...picker.querySelectorAll("[data-tutor-option]")];
        for (const option of options) {
            option.hidden = !option.textContent.trim().toLocaleLowerCase().includes(needle);
        }
        const matches = options.filter(option => !option.hidden);
        picker.querySelector("[data-tutor-empty]").hidden = matches.length > 0;
        return matches;
    }

    function enhance() {
        for (const picker of document.querySelectorAll(pickerSelector)) {
            const select = picker.querySelector("[data-tutor-select]");
            const dropdown = picker.querySelector("[data-tutor-dropdown]");
            const search = picker.querySelector("[data-tutor-search]");
            select.hidden = true;
            dropdown.hidden = false;

            if (enhanced.has(picker)) continue;
            enhanced.add(picker);

            dropdown.addEventListener("toggle", () => {
                if (dropdown.open) search.focus();
            });
            // Typing searches the menu. Selecting a tutor updates the report.
            search.addEventListener("input", event => {
                event.stopPropagation();
                updateOptions(picker, search.value);
            });
            search.addEventListener("change", event => event.stopPropagation());
            dropdown.addEventListener("keydown", event => {
                if (event.key === "Escape") {
                    dropdown.open = false;
                    dropdown.querySelector("summary").focus();
                    event.preventDefault();
                } else if (event.target === search && event.key === "Enter") {
                    event.preventDefault();
                    updateOptions(picker, search.value)[0]?.click();
                }
            });
            dropdown.addEventListener("click", event => {
                const choice = event.target.closest("[data-tutor-option]");
                if (!choice) return;
                select.value = choice.dataset.tutorOption;
                dropdown.querySelector("[data-tutor-value]").textContent = choice.textContent.trim();
                for (const option of dropdown.querySelectorAll("[data-tutor-option]")) {
                    option.setAttribute("aria-pressed", String(option === choice));
                }
                dropdown.open = false;
                search.value = "";
                updateOptions(picker, "");
                dropdown.querySelector("summary").focus();
                select.dispatchEvent(new Event("change", { bubbles: true }));
            });
        }
    }

    document.addEventListener("click", event => {
        for (const dropdown of document.querySelectorAll("[data-tutor-dropdown][open]")) {
            if (!dropdown.contains(event.target)) dropdown.open = false;
        }
    });
    document.addEventListener("edusmart:filters-updated", enhance);
    enhance();
});

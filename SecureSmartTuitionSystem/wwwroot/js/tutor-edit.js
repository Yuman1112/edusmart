
(() => {
    "use strict";

    const form = document.getElementById("edit-tutor-form");
    if (!form) return;

    // Match the phone formatting used by the enrolment form.
    const phoneInput = form.elements.namedItem("PhoneNumber");

    function formatTutorPhone(value) {
        let digits = String(value ?? "").replace(/\D/g, "");

        // +60123456789 / 60123456789 → 0123456789
        if (digits.startsWith("60")) {
            digits = "0" + digits.slice(2);
        }

        digits = digits.slice(0, 11);

        return digits.length > 3
            ? digits.slice(0, 3) + "-" + digits.slice(3)
            : digits;
    }

    if (phoneInput) {
        // Allow pasting a full international number before formatting.
        phoneInput.maxLength = 20;
        phoneInput.inputMode = "tel";

        // Capture runs before the existing form validation/change handlers.
        phoneInput.addEventListener("input", () => {
            const before = phoneInput.value;
            const position = phoneInput.selectionStart;

            const formatted = formatTutorPhone(before);

            if (before !== formatted) {
                phoneInput.value = formatted;

                if (position !== null) {
                    const nextPosition =
                        formatTutorPhone(before.slice(0, position)).length;

                    phoneInput.setSelectionRange(
                        nextPosition,
                        nextPosition
                    );
                }
            }
        }, true);
    }

    // Validate after leaving a field.
    // Once checked, validate again while the user edits it.
    if (window.jQuery) {
        window.jQuery(function ($) {
            if (!$.validator || !$.validator.unobtrusive) return;

            const $form = $(form);

            if (!$form.data("validator")) {
                $.validator.unobtrusive.parse(form);
            }

            const validator = $form.data("validator");
            if (!validator) return;

            const checkedFields = new Set();

            // Keep validating fields returned with server-side errors.
            $form.find("[data-valmsg-for]").each(function () {
                if ($(this).hasClass("field-validation-error")) {
                    checkedFields.add(
                        $(this).attr("data-valmsg-for")
                    );
                }
            });

            // Match the email format checked by the Controller.
            $.validator.addMethod(
                "tutorEmailFormat",
                function (value, element) {
                    const email = String(value ?? "").trim();

                    return this.optional(element)
                        || (
                            email.length <= 254
                            && /^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$/.test(email)
                        );
                },
                "Please enter a valid email address."
            );

            $form.find('[name="Email"]').rules("add", {
                normalizer: function (value) {
                    return String(value ?? "").trim();
                },
                tutorEmailFormat: true
            });

            validator.settings.onfocusout = function (element) {
                if (!element.name || this.checkable(element)) return;

                checkedFields.add(element.name);
                this.element(element);
            };

            // Use the input event below instead of keyup.
            validator.settings.onkeyup = false;

            $form.on(
                "input.tutorValidation",
                "input[data-val='true'], textarea[data-val='true']",
                function () {
                    if (checkedFields.has(this.name)) {
                        validator.element(this);
                    }
                }
            );
        });
    }

    function watchSaveButton(hasChanges) {
        const button = document.getElementById("saveTutorChanges");
        function update() {
            if (button) button.disabled = form.dataset.hasErrors !== "true" && !hasChanges();
        }
        form.addEventListener("input", update);
        form.addEventListener("change", update);
        update();
        return update;
    }

    if (form.dataset.personalOnly === "true") {
        const snapshot = () => JSON.stringify([
            form.elements.namedItem("FullName").value.trim(),
            form.elements.namedItem("Email").value.trim().toUpperCase(),
            form.elements.namedItem("PhoneNumber").value.trim()
        ]);
        const initial = snapshot();
        watchSaveButton(() => snapshot() !== initial);
        return;
    }

    const levels = {
        Primary: [
            "YEAR 1", "YEAR 2", "YEAR 3",
            "YEAR 4", "YEAR 5", "YEAR 6"
        ],
        Secondary: [
            "FORM 1", "FORM 2", "FORM 3",
            "FORM 4", "FORM 5"
        ]
    };

    const catalog = {
        "YEAR 1": ["BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI"],
        "YEAR 2": ["BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI"],
        "YEAR 3": ["BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI"],
        "YEAR 4": ["BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI", "SEJ"],
        "YEAR 5": ["BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI", "SEJ"],
        "YEAR 6": ["BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI", "SEJ"],

        "FORM 1": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI",
            "SEJ", "GEO", "RBT", "ASK"
        ],
        "FORM 2": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI",
            "SEJ", "GEO", "RBT", "ASK"
        ],
        "FORM 3": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI",
            "SEJ", "GEO", "RBT", "ASK"
        ],
        "FORM 4": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "AM",
            "BIO", "CHE", "PHY", "SEJ",
            "ACC", "EKO", "PER"
        ],
        "FORM 5": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "AM",
            "BIO", "CHE", "PHY", "SEJ",
            "ACC", "EKO", "PER"
        ]
    };

    const names = {
        BC: "Chinese",
        BMP: "Bahasa Melayu Pemahaman",
        BMK: "Bahasa Melayu Karangan",
        BIP: "English Comprehension",
        BIK: "English Essay",
        MM: "Mathematics",
        SCI: "Science",
        SEJ: "Sejarah",
        GEO: "Geography",
        RBT: "Design and Technology",
        ASK: "Basic Computer Science",
        AM: "Additional Mathematics",
        BIO: "Biology",
        CHE: "Chemistry",
        PHY: "Physics",
        ACC: "Accounting",
        EKO: "Economics",
        PER: "Perniagaan"
    };

    const selected = new Map();

    const initial = document.getElementById(
        "initial-teaching-levels"
    );

    try {
        const saved = JSON.parse(initial?.textContent || "[]");

        for (const item of saved) {
            const level = (
                item.AcademicLevel ||
                item.academicLevel ||
                ""
            ).toUpperCase();

            if (catalog[level]) {
                const codes =
                    item.SubjectCodes ||
                    item.subjectCodes ||
                    [];

                selected.set(
                    level,
                    new Set(codes.filter(code => catalog[level].includes(code)))
                );
            }
        }
    } catch {
        // Server-side validation still protects saving.
    }

    const stageTabs = document.querySelectorAll(
        "#teaching-stage-tabs [data-stage]"
    );

    const levelContainer = document.getElementById(
        "teaching-levels-container"
    );

    const subjectContainer = document.getElementById(
        "teaching-subjects-container"
    );

    const summary = document.getElementById(
        "teaching-selection-summary"
    );

    const count = document.getElementById(
        "teaching-level-count"
    );

    const hidden = document.getElementById(
        "teaching-levels-hidden"
    );

    const error = document.getElementById(
        "teaching-levels-error"
    );

    let stage = [...selected.keys()][0]?.startsWith("FORM")
        ? "Secondary"
        : "Primary";

    let active = [...selected.keys()][0] || null;

    function element(tag, className, text) {
        const el = document.createElement(tag);

        if (className) {
            el.className = className;
        }

        if (text !== undefined) {
            el.textContent = text;
        }

        return el;
    }

    function sync() {
        hidden.replaceChildren();
        summary.replaceChildren();

        const all = [
            ...levels.Primary,
            ...levels.Secondary
        ].filter(level => selected.has(level));

        count.textContent =
            `${all.length} ${all.length === 1 ? "level" : "levels"}`;

        if (all.length === 0) {
            summary.append(
                element(
                    "span",
                    "text-muted small",
                    "No academic levels selected."
                )
            );
        }

        all.forEach((level, index) => {
            const subjectCount = selected.get(level).size;

            summary.append(
                element(
                    "span",
                    "tutor-selection-badge",
                    `${level} · ${subjectCount} ` +
                    `${subjectCount === 1 ? "subject" : "subjects"}`
                )
            );

            function addHiddenInput(key, value) {
                const input = element("input");

                input.type = "hidden";
                input.name = `TeachingLevels[${index}].${key}`;
                input.value = value;

                hidden.append(input);
            }

            addHiddenInput(
                "EducationStage",
                level.startsWith("YEAR") ? "Primary" : "Secondary"
            );

            addHiddenInput("AcademicLevel", level);

            catalog[level]
                .filter(code => selected.get(level).has(code))
                .forEach((code, subjectIndex) => {
                    addHiddenInput(
                        `SubjectCodes[${subjectIndex}]`,
                        code
                    );
                });
        });
    }

    function render() {
        stageTabs.forEach(tab => {
            const isActive = tab.dataset.stage === stage;
            tab.classList.toggle("active", isActive);
            tab.setAttribute("aria-selected", String(isActive));
            tab.tabIndex = isActive ? 0 : -1;
            const panel = document.getElementById("teaching-stage-content");
            if (isActive && panel) panel.setAttribute("aria-labelledby", tab.id);
        });

        levelContainer.replaceChildren();
        for (const level of levels[stage]) {
            const cell = element("div", "col-12 col-sm-6 col-lg-4");
            const card = element("div", "tutor-level-card");
            const isSelected = selected.has(level);
            card.classList.toggle("selected", isSelected);
            card.classList.toggle("viewing", isSelected && active === level);

            const row = element("div", "tutor-level-main");
            const checkbox = element("input", "form-check-input");
            checkbox.type = "checkbox";
            checkbox.id = `edit-level-${level.replace(/\s/g, "-")}`;
            checkbox.checked = isSelected;
            checkbox.setAttribute("aria-label", `Select ${level}`);

            const label = element("label", "tutor-level-name", level);
            label.htmlFor = checkbox.id;
            const detail = element("span", "tutor-level-subcount");
            if (isSelected) {
                const n = selected.get(level).size;
                detail.textContent = `${n} ${n === 1 ? "subject" : "subjects"} selected`;
            } else {
                detail.textContent = "Not selected";
            }

            checkbox.addEventListener("change", () => {
                if (checkbox.checked) {
                    if (!selected.has(level)) selected.set(level, new Set());
                    active = level;
                } else {
                    selected.delete(level);
                    if (active === level) {
                        active = levels[stage].find(x => selected.has(x)) || null;
                    }
                }
                error.textContent = "";
                sync();
                render();
            });

            label.addEventListener("click", event => {
                if (!selected.has(level)) return;
                event.preventDefault();
                active = level;
                render();
            });

            card.addEventListener("click", event => {
                if (event.target.closest("input, label")) return;
                if (selected.has(level)) {
                    active = level;
                    render();
                } else {
                    checkbox.click();
                }
            });

            row.append(checkbox, label);
            card.append(row, detail);
            cell.append(card);
            levelContainer.append(cell);
        }

        subjectContainer.replaceChildren();
        if (!active || !selected.has(active) || !levels[stage].includes(active)) {
            subjectContainer.append(
                element("p", "text-muted small mb-0",
                    "Select an academic level above to choose its subjects.")
            );
            return;
        }

        subjectContainer.append(
            element("h3", "tutor-panel-title", `${active} — Teaching Subjects`)
        );
        const grid = element("div", "row g-3");
        for (const code of catalog[active]) {
            const cell = element("div", "col-12 col-sm-6 col-lg-4");
            const label = element("label", "form-check d-flex align-items-start gap-2");
            const checkbox = element("input", "form-check-input");
            checkbox.type = "checkbox";
            checkbox.checked = selected.get(active).has(code);
            checkbox.addEventListener("change", () => {
                const subjectSet = selected.get(active);
                if (!subjectSet) return;
                if (checkbox.checked) subjectSet.add(code);
                else subjectSet.delete(code);
                error.textContent = "";
                sync();
                // Refresh card counters without replacing subject checkboxes.
                refreshLevelCards();
            });
            const words = element("span", "form-check-label");
            words.append(
                element("span", "tutor-subject-name", names[code] || code),
                element("small", "d-block text-muted", code)
            );
            label.append(checkbox, words);
            cell.append(label);
            grid.append(cell);
        }
        subjectContainer.append(grid);
    }

    function refreshLevelCards() {
        levelContainer.querySelectorAll(".tutor-level-card").forEach(card => {
            const checkbox = card.querySelector('input[type="checkbox"]');
            if (!checkbox) return;
            const level = checkbox.getAttribute("aria-label").replace(/^Select /, "");
            const detail = card.querySelector(".tutor-level-subcount");
            if (!detail || !selected.has(level)) return;
            const n = selected.get(level).size;
            detail.textContent = `${n} ${n === 1 ? "subject" : "subjects"} selected`;
        });
    }

    stageTabs.forEach(tab => {
        tab.addEventListener("click", () => {
            stage = tab.dataset.stage;

            active = levels[stage].find(
                level => selected.has(level)
            ) || null;

            render();
        });
    });

    form.addEventListener("submit", event => {
        const invalid =
            selected.size === 0 ||
            [...selected.values()].some(
                subjects => subjects.size === 0
            );

        if (invalid) {
            event.preventDefault();

            error.textContent =
                "Select at least one academic level and " +
                "at least one subject for each selected level.";

            error.scrollIntoView({
                behavior: "smooth",
                block: "center"
            });

            return;
        }

        sync();
    });

    sync();
    render();


    // =====================================================
    // EDIT TUTOR - CHANGE DETECTION
    // =====================================================

    const trackedFields = [
        "EmploymentType",
        "Email",
        "FullName",
        "PhoneNumber",
        "Qualification",
        "FieldOfStudy",
        "AreaOfExpertise",
        "TeachingExperience"
    ];

    function normalizeField(value, fieldName) {
        const normalized = String(value ?? "").trim();

        return fieldName === "Email"
            ? normalized.toUpperCase()
            : normalized;
    }

    const originalFields = {};

    for (const fieldName of trackedFields) {
        const field = form.elements.namedItem(fieldName);

        originalFields[fieldName] = normalizeField(
            field?.value,
            fieldName
        );
    }

    function getTeachingSelectionSnapshot() {
        return JSON.stringify(
            [...selected.entries()]
                .map(([level, subjects]) => ({
                    level: level.toUpperCase(),
                    subjects: [...subjects]
                        .map(code => code.toUpperCase())
                        .sort()
                }))
                .sort((a, b) => a.level.localeCompare(b.level))
        );
    }

    const originalTeachingSelection =
        getTeachingSelectionSnapshot();

    function hasTutorChanges() {
        const fieldsChanged = trackedFields.some(fieldName => {
            const field = form.elements.namedItem(fieldName);

            return normalizeField(field?.value, fieldName)
                !== originalFields[fieldName];
        });

        const teachingChanged =
            getTeachingSelectionSnapshot()
            !== originalTeachingSelection;

        const documentsChanged = ["Certificate", "Resume"].some(
            name => form.elements.namedItem(name)?.files?.length > 0
        );
        const verificationConfirmed = form.querySelector('input[type="checkbox"][name="QualificationVerified"]')?.checked;

        return fieldsChanged || teachingChanged || documentsChanged || verificationConfirmed;
    }

    const updateSaveChangesState = watchSaveButton(hasTutorChanges);

    const teachingHiddenContainer =
        document.getElementById("teaching-levels-hidden");

    if (teachingHiddenContainer) {
        const observer = new MutationObserver(
            updateSaveChangesState
        );

        observer.observe(teachingHiddenContainer, {
            childList: true
        });
    }

})();

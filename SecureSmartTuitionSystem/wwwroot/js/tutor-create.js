
(() => {
    "use strict";

    // ============================================
    // FORM ELEMENTS
    // ============================================

    const form = document.getElementById("create-tutor-form");
    if (!form) return;

    const fieldNames = [
        "FullName",
        "Email",
        "PhoneNumber",
        "Qualification",
        "FieldOfStudy",
        "QualificationVerified",
        "TeachingExperience",
        "AreaOfExpertise"
    ];

    const fields = fieldNames
        .map(name => name === "QualificationVerified"
            ? form.querySelector('input[type="checkbox"][name="QualificationVerified"]')
            : form.elements.namedItem(name))
        .filter(Boolean);

    const email = form.elements.namedItem("Email");
    const phone = form.elements.namedItem("PhoneNumber");

    const status = document.getElementById(
        "email-check-status"
    );

    const submitButton = form.querySelector(
        'button[type="submit"]'
    );

    if (!email || !phone || !status || !submitButton) {
        return;
    }

    // ============================================
    // FIELD VALIDATION
    // ============================================

    const phonePattern = /^01[0-9]-[0-9]{7,8}$/;
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    const touchedFields = new Set();

    let emailCheckVersion = 0;
    let emailCheckTimer = null;
    let activeRequest = null;
    let submitting = false;

    function showError(field, message) {
        const span = form.querySelector(
            `[data-valmsg-for="${field.name}"]`
        );

        if (span) {
            span.textContent = message;
            span.id = `${field.id}-error`;
            span.setAttribute("aria-live", "polite");

            span.classList.toggle(
                "field-validation-error",
                Boolean(message)
            );

            span.classList.toggle(
                "field-validation-valid",
                !message
            );

            const ids = new Set(
                (field.getAttribute("aria-describedby") || "")
                    .split(/\s+/)
                    .filter(Boolean)
            );

            ids.add(span.id);

            if (field === email) {
                ids.add("email-check-status");
            }

            field.setAttribute(
                "aria-describedby",
                [...ids].join(" ")
            );
        }

        field.classList.toggle(
            "is-invalid",
            Boolean(message)
        );

        field.setAttribute(
            "aria-invalid",
            message ? "true" : "false"
        );

        return !message;
    }

    function validate(field) {
        const value = field.value.trim();
        let message = "";

        if (field.name === "QualificationVerified" && !field.checked) {
            message = "Confirm that the tutor's qualifications have been verified for the selected subjects and levels.";
        } else if (field.dataset.valRequired && !value) {
            message = field.dataset.valRequired;
        } else if (
            field.maxLength > 0 &&
            field.value.length > field.maxLength
        ) {
            message =
                `Please enter no more than ${field.maxLength} characters.`;
        } else if (
            field === email &&
            value &&
            !emailPattern.test(value)
        ) {
            message = "Please enter a valid email address.";
        } else if (
            field === phone &&
            value &&
            !phonePattern.test(value)
        ) {
            message =
                "Please enter a valid Malaysian phone number.";
        }

        return showError(field, message);
    }

    // ============================================
    // PHONE AUTO FORMAT
    // ============================================

    function formatPhoneNumber(value) {
        let digits = value.replace(/\D/g, "");

        if (digits.startsWith("60")) {
            digits = "0" + digits.slice(2);
        }

        digits = digits.slice(0, 11);

        if (digits.length > 3) {
            return digits.slice(0, 3) +
                "-" +
                digits.slice(3);
        }

        return digits;
    }

    phone.addEventListener("input", () => {
        const formatted = formatPhoneNumber(phone.value);

        if (phone.value !== formatted) {
            phone.value = formatted;
        }

        if (touchedFields.has(phone.name)) {
            validate(phone);
        }
    });

    // ============================================
    // EMAIL AVAILABILITY CHECK
    // ============================================

    function resetEmailCheck() {
        clearTimeout(emailCheckTimer);
        emailCheckTimer = null;

        emailCheckVersion++;

        if (activeRequest) {
            activeRequest.abort();
            activeRequest = null;
        }

        status.textContent = "";
        status.className = "form-text";
    }

    async function checkEmail() {
        resetEmailCheck();

        const currentVersion = emailCheckVersion;
        const checkedEmail = email.value.trim();

        if (!validate(email) || !checkedEmail) {
            return false;
        }

        const antiForgeryToken = form.elements.namedItem(
            "__RequestVerificationToken"
        );

        const checkEmailUrl = form.dataset.checkEmailUrl;

        if (!antiForgeryToken || !checkEmailUrl) {
            showError(
                email,
                "Unable to check this email. Please try again."
            );
            return false;
        }

        const controller = new AbortController();
        activeRequest = controller;

        const timeout = setTimeout(
            () => controller.abort(),
            10000
        );

        status.textContent =
            "Checking email availability...";

        try {
            const body = new URLSearchParams({
                email: checkedEmail,
                __RequestVerificationToken:
                    antiForgeryToken.value
            });

            const response = await fetch(
                checkEmailUrl,
                {
                    method: "POST",
                    body,
                    credentials: "same-origin",
                    headers: {
                        "Accept": "application/json"
                    },
                    signal: controller.signal
                }
            );

            if (response.redirected || !response.ok) {
                throw new Error(
                    "Email check unavailable"
                );
            }

            const result = await response.json();

            if (
                typeof result.available !== "boolean" ||
                typeof result.message !== "string"
            ) {
                throw new Error(
                    "Unexpected response"
                );
            }

            if (
                currentVersion !== emailCheckVersion ||
                email.value.trim() !== checkedEmail
            ) {
                return false;
            }

            status.textContent = result.available
                ? result.message
                : "";

            status.className = result.available
                ? "form-text text-success"
                : "form-text";

            showError(
                email,
                result.available
                    ? ""
                    : result.message
            );

            return result.available;
        } catch (error) {
            if (currentVersion !== emailCheckVersion) {
                return false;
            }

            status.textContent = "";
            status.className = "form-text";

            showError(
                email,
                "Unable to check this email. Please try again."
            );

            return false;
        } finally {
            clearTimeout(timeout);

            if (activeRequest === controller) {
                activeRequest = null;
            }
        }
    }

    // ============================================
    // LIVE FIELD VALIDATION
    // ============================================

    fields.forEach(field => {
        field.addEventListener("blur", () => {
            touchedFields.add(field.name);

            if (field === email) {
                if (!submitting) {
                    void checkEmail();
                }
            } else {
                validate(field);
            }
        });

        field.addEventListener("input", () => {
            if (field === email) {
                resetEmailCheck();
                showError(email, "");

                if (!submitting) {
                    emailCheckTimer = setTimeout(() => {
                        touchedFields.add("Email");
                        void checkEmail();
                    }, 500);
                }
            } else if (touchedFields.has(field.name)) {
                validate(field);
            }
        });
    });

    // ============================================
    // TEACHING SUBJECTS & LEVELS
    // ============================================

    const teachingContainer = document.getElementById(
        "teaching-levels-container"
    );

    const teachingSubjectsContainer = document.getElementById(
        "teaching-subjects-container"
    );

    const teachingHidden = document.getElementById(
        "teaching-levels-hidden"
    );

    const teachingError = document.getElementById(
        "teaching-levels-error"
    );

    const teachingSummary = document.getElementById(
        "teaching-selection-summary"
    );

    const teachingLevelCount = document.getElementById(
        "teaching-level-count"
    );

    const initialTeachingData = document.getElementById(
        "initial-teaching-levels"
    );

    const teachingStageContent = document.getElementById(
        "teaching-stage-content"
    );

    const teachingTabs = document.querySelectorAll(
        "#teaching-stage-tabs [data-stage]"
    );

    // ============================================
    // ACADEMIC LEVELS
    // ============================================

    const teachingStages = {
        Primary: [
            "YEAR 1",
            "YEAR 2",
            "YEAR 3",
            "YEAR 4",
            "YEAR 5",
            "YEAR 6"
        ],
        Secondary: [
            "FORM 1",
            "FORM 2",
            "FORM 3",
            "FORM 4",
            "FORM 5"
        ]
    };

    // ============================================
    // SUBJECT CATALOG
    // ============================================


    // ============================================
    // SUBJECT CATALOG
    // ============================================

    const teachingCatalog = {
        "YEAR 1": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI"
        ],
        "YEAR 2": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI"
        ],
        "YEAR 3": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI"
        ],
        "YEAR 4": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI", "SEJ"
        ],
        "YEAR 5": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI", "SEJ"
        ],
        "YEAR 6": [
            "BC", "BMP", "BMK", "BIP", "BIK", "MM", "SCI", "SEJ"
        ],

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

    // ============================================
    // SUBJECT DISPLAY NAMES
    // ============================================

    const subjectDisplayNames = {
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

    // Key: Academic Level
    // Value: Set of selected Subject Codes
    const selectedTeaching = new Map();

    let activeTeachingStage = "Primary";

    const activeTeachingLevel = {
        Primary: null,
        Secondary: null
    };

    function getTeachingStage(level) {
        return level.startsWith("YEAR")
            ? "Primary"
            : "Secondary";
    }

    function setTeachingError(message) {
        if (!teachingError) return;

        teachingError.textContent = message;

        teachingError.classList.toggle(
            "field-validation-error",
            Boolean(message)
        );
    }

    // ============================================
    // CREATE CHECKBOX
    // ============================================

    let teachingCheckboxCounter = 0;

    function createTeachingCheckbox(label, checked, onChange) {
        const wrapper = document.createElement("div");
        wrapper.className = "form-check";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.className = "form-check-input";
        checkbox.checked = checked;

        const checkboxId =
            `tutor-teaching-checkbox-${++teachingCheckboxCounter}`;

        checkbox.id = checkboxId;

        const checkboxLabel = document.createElement("label");
        checkboxLabel.className = "form-check-label";
        checkboxLabel.htmlFor = checkboxId;
        checkboxLabel.textContent = label;

        checkbox.addEventListener("change", () => {
            onChange(checkbox.checked);
        });

        wrapper.append(checkbox, checkboxLabel);

        return wrapper;
    }

    // ============================================
    // RENDER PRIMARY / SECONDARY TABS
    // ============================================

    function renderTeachingTabs() {
        teachingTabs.forEach(tab => {
            const active =
                tab.dataset.stage === activeTeachingStage;

            tab.classList.toggle("active", active);

            tab.setAttribute(
                "aria-selected",
                String(active)
            );

            tab.tabIndex = active ? 0 : -1;

            if (active && teachingStageContent) {
                teachingStageContent.setAttribute(
                    "aria-labelledby",
                    tab.id
                );
            }
        });
    }

    // ============================================
    // RENDER ACADEMIC LEVELS
    // ============================================

    function renderTeachingLevels() {
        if (!teachingContainer) return;
        teachingContainer.replaceChildren();
        const levels = teachingStages[activeTeachingStage];

        levels.forEach(level => {
            const cell = document.createElement("div");
            cell.className = "col-12 col-sm-6 col-lg-4";
            const card = document.createElement("div");
            card.className = "tutor-level-card";
            const isSelected = selectedTeaching.has(level);
            const isViewing = isSelected &&
                activeTeachingLevel[activeTeachingStage] === level;
            card.classList.toggle("selected", isSelected);
            card.classList.toggle("viewing", isViewing);

            const row = document.createElement("div");
            row.className = "tutor-level-main";
            const checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.className = "form-check-input";
            checkbox.id = `create-level-${level.replace(/\s/g, "-")}`;
            checkbox.checked = isSelected;
            checkbox.setAttribute("aria-label", `Select ${level}`);

            const label = document.createElement("label");
            label.htmlFor = checkbox.id;
            label.className = "tutor-level-name";
            label.textContent = level;

            const detail = document.createElement("span");
            detail.className = "tutor-level-subcount";
            if (isSelected) {
                const n = selectedTeaching.get(level).size;
                detail.textContent = `${n} ${n === 1 ? "subject" : "subjects"} selected`;
            } else {
                detail.textContent = "Not selected";
            }

            checkbox.addEventListener("change", () => {
                if (checkbox.checked) {
                    if (!selectedTeaching.has(level)) selectedTeaching.set(level, new Set());
                    activeTeachingLevel[activeTeachingStage] = level;
                } else {
                    selectedTeaching.delete(level);
                    if (activeTeachingLevel[activeTeachingStage] === level) {
                        activeTeachingLevel[activeTeachingStage] =
                            levels.find(x => selectedTeaching.has(x)) || null;
                    }
                }
                setTeachingError("");
                renderTeachingLevels();
                renderTeachingSubjects();
                renderTeachingSummary();
                syncTeachingInputs();
            });

            // Clicking the name of an already selected level opens its subjects.
            // The checkbox itself remains the explicit deselection control.
            label.addEventListener("click", event => {
                if (!selectedTeaching.has(level)) return;
                event.preventDefault();
                activeTeachingLevel[activeTeachingStage] = level;
                renderTeachingLevels();
                renderTeachingSubjects();
            });

            card.addEventListener("click", event => {
                if (event.target.closest("input, label")) return;
                if (selectedTeaching.has(level)) {
                    activeTeachingLevel[activeTeachingStage] = level;
                    renderTeachingLevels();
                    renderTeachingSubjects();
                } else {
                    checkbox.click();
                }
            });
            row.append(checkbox, label);
            card.append(row, detail);
            cell.append(card);
            teachingContainer.append(cell);
        });
    }

    // ============================================
    // RENDER SUBJECTS
    // ============================================

    function renderTeachingSubjects() {
        if (!teachingSubjectsContainer) return;

        teachingSubjectsContainer.replaceChildren();

        const level =
            activeTeachingLevel[activeTeachingStage];

        const heading = document.createElement("h3");

        if (!level || !selectedTeaching.has(level)) {
            heading.textContent = "Teaching Subjects";

            const message = document.createElement("p");
            message.className = "text-muted small mb-0";
            message.textContent =
                "Select an academic level above to choose its subjects.";

            teachingSubjectsContainer.append(
                heading,
                message
            );

            return;
        }

        heading.textContent = `${level} — Subjects`;

        teachingSubjectsContainer.appendChild(heading);

        const grid = document.createElement("div");
        grid.className = "row g-3";

        teachingCatalog[level].forEach(subject => {
            const cell = document.createElement("div");
            cell.className = "col-6 col-md-4 col-lg-3";


            const checkbox = createTeachingCheckbox(
                subject,
                selectedTeaching.get(level).has(subject),
                checked => {
                    const subjects = selectedTeaching.get(level);

                    if (!subjects) return;

                    if (checked) {
                        subjects.add(subject);
                    } else {
                        subjects.delete(subject);
                    }

                    setTeachingError("");
                    renderTeachingLevels();
                    renderTeachingSummary();
                    syncTeachingInputs();
                }
            );

            // Display Full Subject Name + Subject Code
            const label = checkbox.querySelector(
                ".form-check-label"
            );

            if (label) {
                label.replaceChildren();

                const nameSpan = document.createElement("span");
                nameSpan.className = "tutor-subject-name";
                nameSpan.textContent =
                    subjectDisplayNames[subject] || subject;

                const codeSpan = document.createElement("span");
                codeSpan.className = "tutor-subject-code";
                codeSpan.textContent = subject;

                label.append(nameSpan, codeSpan);
            }

            cell.appendChild(checkbox);
            grid.appendChild(cell);
        });

        teachingSubjectsContainer.appendChild(grid);
    }

    // ============================================
    // SELECTED TEACHING LEVELS SUMMARY
    // ============================================

    function renderTeachingSummary() {
        if (!teachingSummary || !teachingLevelCount) {
            return;
        }

        teachingSummary.replaceChildren();

        const count = selectedTeaching.size;

        teachingLevelCount.textContent =
            `${count} ${count === 1 ? "level" : "levels"}`;

        if (count === 0) {
            const empty = document.createElement("span");
            empty.className = "text-muted small";
            empty.textContent =
                "No academic levels selected.";

            teachingSummary.appendChild(empty);
            return;
        }

        Object.values(teachingStages)
            .flat()
            .filter(level => selectedTeaching.has(level))
            .forEach(level => {
                const subjectCount =
                    selectedTeaching.get(level).size;

                const badge = document.createElement("span");

                badge.className =
                    "tutor-selection-badge";

                badge.textContent =
                    `${level} · ${subjectCount} ` +
                    `${subjectCount === 1 ? "subject" : "subjects"}`;

                teachingSummary.appendChild(badge);
            });
    }

    // ============================================
    // ASP.NET CORE MODEL BINDING
    // ============================================

    function syncTeachingInputs() {
        if (!teachingHidden) return;

        teachingHidden.replaceChildren();

        function addHiddenInput(name, value) {
            const input = document.createElement("input");

            input.type = "hidden";
            input.name = name;
            input.value = value;

            teachingHidden.appendChild(input);
        }

        let index = 0;

        Object.values(teachingStages)
            .flat()
            .filter(level => selectedTeaching.has(level))
            .forEach(level => {
                const subjects =
                    selectedTeaching.get(level);

                addHiddenInput(
                    `TeachingLevels[${index}].EducationStage`,
                    getTeachingStage(level)
                );

                addHiddenInput(
                    `TeachingLevels[${index}].AcademicLevel`,
                    level
                );

                teachingCatalog[level]
                    .filter(code => subjects.has(code))
                    .forEach((code, subjectIndex) => {
                        addHiddenInput(
                            `TeachingLevels[${index}].SubjectCodes[${subjectIndex}]`,
                            code
                        );
                    });

                index++;
            });
    }

    // ============================================
    // SWITCH PRIMARY / SECONDARY
    // ============================================

    function switchTeachingStage(stage) {
        if (!Object.hasOwn(teachingStages, stage)) {
            return;
        }

        activeTeachingStage = stage;

        renderTeachingTabs();
        renderTeachingLevels();
        renderTeachingSubjects();
    }

    // ============================================
    // TEACHING LEVEL VALIDATION
    // ============================================

    function validateTeachingLevels() {
        if (selectedTeaching.size === 0) {
            setTeachingError(
                "Please select at least one academic level."
            );
            return false;
        }

        for (const [level, subjects] of selectedTeaching) {
            if (subjects.size === 0) {
                const stage = getTeachingStage(level);

                activeTeachingLevel[stage] = level;
                switchTeachingStage(stage);

                setTeachingError(
                    `Please select at least one subject for ${level}.`
                );

                return false;
            }
        }

        setTeachingError("");
        syncTeachingInputs();

        return true;
    }

    // ============================================
    // RESTORE AFTER SERVER VALIDATION ERROR
    // ============================================

    if (initialTeachingData) {
        try {
            const previous = JSON.parse(
                initialTeachingData.textContent || "[]"
            );

            if (Array.isArray(previous)) {
                previous.forEach(item => {
                    const level = String(
                        item.AcademicLevel ||
                        item.academicLevel ||
                        ""
                    ).trim().toUpperCase();

                    const stage = String(
                        item.EducationStage ||
                        item.educationStage ||
                        ""
                    ).trim();

                    const subjects =
                        item.SubjectCodes ||
                        item.subjectCodes ||
                        [];

                    if (
                        Object.hasOwn(teachingCatalog, level) &&
                        stage.toLowerCase() ===
                        getTeachingStage(level).toLowerCase() &&
                        Array.isArray(subjects)
                    ) {
                        const validSubjects = subjects
                            .map(code =>
                                String(code).trim().toUpperCase()
                            )
                            .filter(code =>
                                teachingCatalog[level].includes(code)
                            );

                        selectedTeaching.set(
                            level,
                            new Set(validSubjects)
                        );
                    }
                });
            }
        } catch {
            // Keep the form usable if JSON is invalid.
        }
    }

    // ============================================
    // INITIALIZE TABS
    // ============================================

    teachingTabs.forEach(tab => {
        tab.addEventListener("click", () => {
            switchTeachingStage(
                tab.dataset.stage
            );
        });
    });

    // Open the first selected level when restoring.
    if (selectedTeaching.size > 0) {
        const firstLevel =
            selectedTeaching.keys().next().value;

        const firstStage =
            getTeachingStage(firstLevel);

        activeTeachingStage = firstStage;
        activeTeachingLevel[firstStage] = firstLevel;
    }

    renderTeachingTabs();
    renderTeachingLevels();
    renderTeachingSubjects();
    renderTeachingSummary();
    syncTeachingInputs();

    // ============================================
    // FORM SUBMISSION
    // ============================================

    form.addEventListener("submit", async event => {
        event.preventDefault();

        if (submitting) return;

        submitting = true;
        submitButton.disabled = true;

        const originalText = submitButton.innerHTML;
        submitButton.textContent = "Checking...";

        let posting = false;

        try {
            let valid = true;

            // Validate tutor fields.
            fields.forEach(field => {
                touchedFields.add(field.name);

                if (!validate(field)) {
                    valid = false;
                }
            });

            // Validate selected levels and subjects.
            if (!validateTeachingLevels()) {
                valid = false;
            }

            // Verify email availability.
            const emailAvailable = await checkEmail();

            // Revalidate after async request.
            fields.forEach(field => {
                if (
                    field !== email &&
                    !validate(field)
                ) {
                    valid = false;
                }
            });

            if (!valid || !emailAvailable) {
                const invalidField = form.querySelector(
                    '[aria-invalid="true"]'
                );

                if (invalidField) {
                    invalidField.focus();
                } else if (
                    teachingError &&
                    teachingError.textContent.trim()
                ) {
                    teachingError.scrollIntoView({
                        behavior: "smooth",
                        block: "center"
                    });
                }

                return;
            }

            submitButton.textContent =
                "Creating tutor...";

            // Normal ASP.NET Core POST.
            // Server-side validation still applies.
            HTMLFormElement.prototype.submit.call(form);

            posting = true;
        } finally {
            if (!posting) {
                submitting = false;
                submitButton.disabled = false;
                submitButton.innerHTML = originalText;
            }
        }
    });

})();

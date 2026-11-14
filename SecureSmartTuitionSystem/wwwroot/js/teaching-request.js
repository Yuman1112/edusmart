(() => {
    const form = document.getElementById("teaching-request-form");
    if (!form) return;

    const rows = document.getElementById("teaching-subject-rows");
    const template = document.getElementById("teaching-subject-template");
    const addButton = document.getElementById("add-teaching-subject");
    const evidence = document.getElementById("Evidence");
    const fileList = document.getElementById("teaching-evidence-list");
    const fileStatus = document.getElementById("teaching-evidence-status");
    const references = Array.from(form.querySelectorAll("[data-evidence-size]"));
    const subjectCount = document.getElementById("teaching-subject-count");
    const addEvidence = document.getElementById("add-teaching-evidence");
    const dropZone = document.getElementById("teaching-evidence-drop");
    const previewUrls = new Map();
    const canManageFiles = typeof DataTransfer === "function";
    const megabyte = 1024 * 1024;
    const confirmation = document.getElementById("ConfirmAccuracy");
    const validationNotice = form.querySelector("[data-teaching-validation]");
    let selectedFiles = Array.from(evidence.files);
    const unavailable = new Set(JSON.parse(rows.dataset.unavailableCombinations || "[]"));

    function filterSubjects(row, selected) {
        const level = row.querySelector("[data-teaching-level]");
        const subject = row.querySelector("[data-teaching-subject]");
        for (const option of subject.options) {
            if (!option.value) continue;
            const key = level.value + "/" + option.value;
            const available = Boolean(level.value)
                && option.dataset.levels.split(",").includes(level.value)
                && !unavailable.has(key)
                && (!selected.has(key) || subject.value === option.value);
            option.hidden = !available;
            option.disabled = !available;
        }
        if (subject.selectedOptions[0]?.disabled) subject.value = "";
        subject.disabled = !level.value;
        subject.options[0].textContent = !level.value ? "Choose an academic level first"
            : Array.from(subject.options).some(option => option.value && !option.disabled)
                ? "Select a subject" : "No additional subjects available at this level";
    }

    function refreshSubjectOptions() {
        const currentRows = Array.from(rows.querySelectorAll("[data-subject-row]"));
        const selected = new Set();
        for (const row of currentRows) {
            const level = row.querySelector("[data-teaching-level]").value;
            const subject = row.querySelector("[data-teaching-subject]");
            const option = subject.selectedOptions[0];
            const key = level + "/" + subject.value;
            if (subject.value && (!level || !option?.dataset.levels.split(",").includes(level)
                || unavailable.has(key) || selected.has(key))) {
                subject.value = "";
            }
            if (subject.value) selected.add(level + "/" + subject.value);
        }
        currentRows.forEach(row => filterSubjects(row, selected));
    }

    function updateRows() {
        const currentRows = Array.from(rows.querySelectorAll("[data-subject-row]"));
        currentRows.forEach((row, index) => {
            row.querySelectorAll("[name], [id], [for], [data-valmsg-for]").forEach(element => {
                for (const attribute of ["name", "id", "for", "data-valmsg-for"]) {
                    const value = element.getAttribute(attribute);
                    if (value) {
                        element.setAttribute(attribute, value
                            .replace(/Items\[(?:\d+|__index__)\]/g, "Items[" + index + "]")
                            .replace(/Items_(?:\d+|__index__)__/g, "Items_" + index + "__"));
                    }
                }
            });
            row.querySelector("[data-remove-subject]").disabled = currentRows.length === 1;
        });
        refreshSubjectOptions();
        addButton.disabled = currentRows.length >= 10;
        subjectCount.textContent = currentRows.length + " / 10 items";
        if (window.jQuery?.validator?.unobtrusive) {
            const wrapped = window.jQuery(form);
            wrapped.off(".validate");
            wrapped.removeData("validator").removeData("unobtrusiveValidation");
            window.jQuery.validator.unobtrusive.parse(form);
        }
    }

    addButton.addEventListener("click", () => {
        if (rows.querySelectorAll("[data-subject-row]").length >= 10) return;
        const fragment = template.content.cloneNode(true);
        fragment.querySelectorAll("select").forEach(select => {
            select.value = "";
            select.classList.remove("input-validation-error");
        });
        fragment.querySelectorAll("[data-valmsg-for]").forEach(message => {
            message.textContent = "";
            message.classList.remove("field-validation-error");
            message.classList.add("field-validation-valid");
        });
        rows.append(fragment);
        confirmation.checked = false;
        updateRows();
        rows.lastElementChild.querySelector("select").focus();
    });

    rows.addEventListener("click", event => {
        const button = event.target.closest("[data-remove-subject]");
        if (!button || rows.querySelectorAll("[data-subject-row]").length === 1) return;
        button.closest("[data-subject-row]").remove();
        confirmation.checked = false;
        updateRows();
    });
    rows.addEventListener("change", event => {
        const row = event.target.closest("[data-subject-row]");
        if (row) refreshSubjectOptions();
    });

    function validateEvidence() {
        const checked = references.filter(input => input.checked);
        let message = "";
        const size = selectedFiles.reduce((sum, file) => sum + file.size, 0)
            + checked.reduce((sum, input) => sum + Number(input.dataset.evidenceSize), 0);
        if (selectedFiles.length + checked.length > 5) {
            message = "Choose at most five files, including existing documents.";
        } else if (selectedFiles.some(file => file.size === 0 || file.size > 10 * megabyte)) {
            message = "Each file must be non-empty and no larger than 10 MB.";
        } else if (selectedFiles.some(file => !/\.(pdf|png|jpe?g)$/i.test(file.name))) {
            message = "Choose PDF, PNG or JPG files.";
        } else if (size > 25 * megabyte) {
            message = "The combined evidence must not exceed 25 MB.";
        }
        evidence.setCustomValidity(message);
        const count = selectedFiles.length + checked.length;
        fileStatus.textContent = message || count
            + (count === 1 ? " file · " : " files · ") + (size / megabyte).toFixed(2) + " MB total";
        fileStatus.classList.toggle("text-danger", Boolean(message));
        return !message;
    }

    function updateFiles() {
        if (canManageFiles) {
            const transfer = new DataTransfer();
            selectedFiles.forEach(file => transfer.items.add(file));
            evidence.files = transfer.files;
        }
        for (const [file, url] of previewUrls) {
            if (!selectedFiles.includes(file)) {
                URL.revokeObjectURL(url);
                previewUrls.delete(file);
            }
        }
        fileList.replaceChildren();
        selectedFiles.forEach((file, index) => {
            const item = document.createElement("div");
            item.className = "material-attachment-row";
            const details = document.createElement("div");
            const icon = document.createElement("i");
            icon.className = "bi bi-file-earmark-text material-attachment-icon";
            icon.setAttribute("aria-hidden", "true");
            const label = document.createElement("span");
            label.textContent = file.name;
            const size = document.createElement("small");
            size.className = "text-muted";
            size.textContent = (file.size / megabyte).toFixed(2) + " MB · Ready to upload";
            details.append(icon, label, size);
            item.append(details);
            const type = previewType(file.name);
            if (type && file.size > 0 && file.size <= 10 * megabyte) {
                if (!previewUrls.has(file)) {
                    previewUrls.set(file, URL.createObjectURL(new Blob([file], { type })));
                }
                const preview = document.createElement("a");
                preview.href = previewUrls.get(file);
                preview.target = "_blank";
                preview.rel = "noopener";
                preview.className = "outline-button material-file-preview";
                preview.textContent = "Preview";
                preview.dataset.materialPreview = "";
                preview.dataset.previewName = file.name;
                preview.dataset.previewType = type;
                item.append(preview);
                if (type.startsWith("image/")) {
                    const thumbnail = preview.cloneNode(false);
                    thumbnail.className = "material-thumbnail";
                    thumbnail.setAttribute("aria-label", "Preview " + file.name);
                    const image = document.createElement("img");
                    image.src = preview.href;
                    image.alt = file.name;
                    thumbnail.append(image);
                    icon.replaceWith(thumbnail);
                }
            }
            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "outline-button";
            remove.textContent = "×";
            remove.disabled = !canManageFiles;
            remove.setAttribute("aria-label", "Remove " + file.name);
            remove.addEventListener("click", () => {
                selectedFiles.splice(index, 1);
                confirmation.checked = false;
                updateFiles();
            });
            item.append(remove);
            fileList.append(item);
        });
        validateEvidence();
    }

    function previewType(name) {
        const extension = name.split(".").pop().toLowerCase();
        if (extension === "pdf") return "application/pdf";
        if (extension === "png") return "image/png";
        if (extension === "jpg" || extension === "jpeg") return "image/jpeg";
        return null;
    }

    function addFiles(files) {
        confirmation.checked = false;
        for (const file of files) {
            const duplicate = selectedFiles.some(current => current.name === file.name
                && current.size === file.size && current.lastModified === file.lastModified);
            if (!duplicate) selectedFiles.push(file);
        }
        updateFiles();
    }

    evidence.addEventListener("change", () => {
        if (!canManageFiles) {
            selectedFiles = Array.from(evidence.files);
            updateFiles();
            return;
        }
        addFiles(evidence.files);
    });
    if (canManageFiles) {
        addEvidence.hidden = false;
        dropZone.hidden = false;
        evidence.classList.add("visually-hidden");
        addEvidence.addEventListener("click", () => evidence.click());
        let dragDepth = 0;
        dropZone.addEventListener("dragenter", event => {
            event.preventDefault();
            dragDepth++;
            dropZone.classList.add("is-dragging");
        });
        dropZone.addEventListener("dragover", event => event.preventDefault());
        dropZone.addEventListener("dragleave", () => {
            dragDepth--;
            if (dragDepth <= 0) dropZone.classList.remove("is-dragging");
        });
        dropZone.addEventListener("drop", event => {
            event.preventDefault();
            dragDepth = 0;
            dropZone.classList.remove("is-dragging");
            if (event.dataTransfer) addFiles(event.dataTransfer.files);
        });
    }
    window.addEventListener("pagehide", event => {
        if (event.persisted) return;
        for (const url of previewUrls.values()) URL.revokeObjectURL(url);
        previewUrls.clear();
    });
    references.forEach(input => input.addEventListener("change", validateEvidence));
    document.addEventListener("teaching:approvals-updated", event => {
        unavailable.clear();
        event.detail.forEach(key => unavailable.add(key));
        rows.dataset.unavailableCombinations = JSON.stringify(event.detail);
        refreshSubjectOptions();
        confirmation.checked = false;
    });
    function showValidationNotice() {
        validationNotice.hidden = false;
    }
    form.addEventListener("invalid", showValidationNotice, true);
    if (window.jQuery) {
        window.jQuery(form).on("invalid-form.teachingConfirmation", showValidationNotice);
    }
    function resetConfirmation(event) {
        if (event.target !== confirmation) confirmation.checked = false;
    }
    form.addEventListener("input", resetConfirmation);
    form.addEventListener("change", resetConfirmation);
    form.addEventListener("submit", event => {
        if (!confirmation.checked) {
            event.preventDefault();
            showValidationNotice();
            confirmation.focus();
        }
        if (!validateEvidence()) {
            event.preventDefault();
            showValidationNotice();
            evidence.reportValidity();
        }
    });
    updateRows();
    validateEvidence();
})();

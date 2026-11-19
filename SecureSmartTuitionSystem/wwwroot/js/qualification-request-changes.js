(() => {
    "use strict";
    const form = document.getElementById("qualification-request-form");
    if (!form) return;
    const qualification = form.elements.namedItem("Qualification");
    const field = form.elements.namedItem("FieldOfStudy");
    const expertise = form.elements.namedItem("AreaOfExpertise");
    const certificate = form.elements.namedItem("Certificates");
    const submit = form.querySelector("button[type='submit']");
    const required = form.querySelector("[data-certificate-required]");
    if (!qualification || !field || !expertise || !certificate || !submit) return;
    const normalize = value => (value || "").trim();
    function hasChanges() {
        return normalize(qualification.value) !== normalize(form.dataset.originalQualification)
            || normalize(field.value) !== normalize(form.dataset.originalField)
            || normalize(expertise.value) !== normalize(form.dataset.originalExpertise)
            || certificate.files.length > 0;
    }
    function update() {
        const degreeChanged = normalize(qualification.value) !== normalize(form.dataset.originalQualification);
        certificate.required = degreeChanged;
        required?.classList.toggle("d-none", !degreeChanged);
        submit.disabled = form.dataset.pending === "true" || !hasChanges();
    }
    form.addEventListener("input", update);
    form.addEventListener("change", update);
    form.addEventListener("qualification:availability", () => {
        const add = form.querySelector('[data-add-files]');
        if(add) add.disabled = form.dataset.pending === "true";
        update();
    });
    form.addEventListener("attachment:changed", update);
    form.addEventListener("reset", () => setTimeout(update, 0));
    form.addEventListener("submit", event => {
        if (form.dataset.pending === "true" || !hasChanges()) {
            event.preventDefault();
            update();
        }
    });
    update();
})();

(() => {
    const level = document.getElementById("AcademicLevel");
    const subject = document.getElementById("SubjectCode");
    if (!level || !subject) return;

    function filterSubjects() {
        for (const option of subject.options) {
            if (!option.value) continue;
            const available = !level.value || option.dataset.levels.split(",").includes(level.value);
            option.hidden = !available;
            option.disabled = !available;
        }
    }

    level.addEventListener("change", () => {
        filterSubjects();
        if (subject.selectedOptions[0]?.disabled) subject.value = "";
    });
    filterSubjects();
})();

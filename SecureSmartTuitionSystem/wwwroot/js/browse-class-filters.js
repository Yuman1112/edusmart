document.addEventListener("DOMContentLoaded", () => {
    const year = document.getElementById("yearFilter");
    const stage = document.getElementById("stageFilter");
    const level = document.getElementById("levelFilter");
    const subject = document.getElementById("subjectFilter");
    if (!stage || !level || !subject) return;
    // Clear dependent selections before the shared live-filter handler submits the form.
    year?.addEventListener("change", () => { subject.value = ""; });
    stage.addEventListener("change", () => { level.value = ""; subject.value = ""; });
    level.addEventListener("change", () => { subject.value = ""; });
});

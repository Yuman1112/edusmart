(() => {
    "use strict";
    const tutor = document.getElementById("TutorUserId");
    const link = document.getElementById("review-tutor-availability");
    if (!tutor || !link) return;
    function update() {
        const available = Boolean(tutor.value);
        link.classList.toggle("d-none", !available);
        if (!available) { link.removeAttribute("href"); return; }
        const url = new URL(link.dataset.baseUrl, window.location.origin);
        url.searchParams.set("userId", tutor.value);
        link.href = url.href;
    }
    tutor.addEventListener("change", update);
    update();
})();
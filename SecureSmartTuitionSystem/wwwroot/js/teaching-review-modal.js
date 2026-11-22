(() => {
    "use strict";
    const dialog = document.getElementById("teachingDecisionModal");
    if (!dialog || !window.bootstrap?.Modal) return;
    const form = document.getElementById("teachingDecisionForm");
    const decision = document.getElementById("teachingApproveDecision");
    const block = document.getElementById("teachingQualificationBlock");
    const qualification = document.getElementById("QualificationVerified");
    const confirm = document.getElementById("teachingConfirmDecision");
    const modal = window.bootstrap.Modal.getOrCreateInstance(dialog);
    function prepare(approve) {
        decision.value = String(approve);
        document.getElementById("teachingDecisionTitle").textContent = approve ? "Approve Request" : "Reject Request";
        document.getElementById("teachingDecisionQuestion").textContent = approve ? "Are you sure you want to approve this teaching request?" : "Are you sure you want to reject this teaching request?";
        document.getElementById("teachingDecisionEffect").textContent = approve ? "Approval adds the requested subject and level to this tutor's approved teaching subjects." : "The requested teaching change will not be applied.";
        confirm.textContent = approve ? "Confirm Approval" : "Confirm Rejection";
        confirm.classList.toggle("btn-success", approve);
        confirm.classList.toggle("btn-danger", !approve);
        if (block) block.hidden = !approve;
        if (qualification) { qualification.disabled = !approve; qualification.required = approve; }
        // Clear stale client-side messages when switching decisions; preserve notes and checkbox values.
        if (window.jQuery && window.jQuery(form).data("validator")) window.jQuery(form).validate().resetForm();
    }
    document.querySelectorAll("[data-teaching-decision]").forEach(button => {
        button.addEventListener("click", () => {
            if (button.disabled) return;
            prepare(button.dataset.teachingDecision === "true");
            modal.show();
        });
    });
    if (dialog.dataset.reviewErrors === "true") {
        // Keep server validation messages visible on a failed POST.
        modal.show();
    }
})();

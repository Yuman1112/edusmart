(() => {
    "use strict";
    function init() {
        const buttons = Array.from(document.querySelectorAll("[data-guardian-resend]"));
        function update() {
            const now = Math.floor(Date.now() / 1000);
            buttons.forEach(button => {
                const remaining = Math.max(0, Number(button.dataset.resendAt || 0) - now);
                const label = button.querySelector("span");
                if (label) label.textContent = remaining > 0
                    ? `${button.dataset.idleLabel} (${remaining}s)` : button.dataset.idleLabel;
                button.disabled = remaining > 0;
                button.setAttribute("aria-disabled", String(button.disabled));
            });
        }
        update();
        if (buttons.length) window.setInterval(update, 1000);
        document.addEventListener("visibilitychange", update);
        window.addEventListener("pageshow", update);
        const ic = document.getElementById("ExistingParentIdentificationLast4");
        ic?.addEventListener("input", () => { ic.value = ic.value.replace(/\D/g, "").slice(0, 4); });
        const otp = document.getElementById("otpCode");
        const form = document.getElementById("guardianForm");
        const codeError = document.getElementById("guardian-code-error");
        function clearCodeError() {
            otp?.setCustomValidity("");
            otp?.classList.remove("input-validation-error");
            if (codeError) { codeError.hidden = true; codeError.textContent = ""; }
        }
        form?.addEventListener("submit", event => {
            if (event.submitter?.getAttribute("formaction")?.includes("ConfirmGuardianOtp") && otp) {
                otp.setCustomValidity("");
                otp.value = otp.value.replace(/\D/g, "").slice(0, 6);
                if (!/^[0-9]{6}$/.test(otp.value)) {
                    event.preventDefault();
                    otp.setCustomValidity("Enter the complete 6-digit code from your email.");
                    otp.classList.add("input-validation-error");
                    if (codeError) { codeError.hidden = false; codeError.textContent = "Enter the complete 6-digit code from your email."; }
                    otp.focus();
                }
            }
        });
        otp?.addEventListener("input", clearCodeError);
        otp?.addEventListener("paste", clearCodeError);
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();


(() => {
    "use strict";

    const card = document.getElementById("tutor-details-card");
    const email = document.getElementById("tutor-details-email");
    const pending = document.getElementById(
        "tutor-details-pending-email"
    );
    const notice = document.getElementById(
        "tutor-details-email-notice"
    );

    if (!card || !email || !pending || !notice) return;

    const statusUrl = card.dataset.emailStatusUrl;
    if (!statusUrl) return;

    let currentEmail = email.textContent.trim();
    let stopped = false;
    let busy = false;
    let timer = null;

    function showNotice(message, success) {
        notice.className = success
            ? "alert alert-success"
            : "alert alert-info";

        notice.textContent = message;
        notice.hidden = false;
    }

    function clearPreviousMessages() {
        document.getElementById("tutor-details-success")?.remove();
        document.getElementById("tutor-details-warning")?.remove();
    }

    function scheduleCheck() {
        if (stopped) return;

        window.clearTimeout(timer);
        timer = window.setTimeout(checkStatus, 5000);
    }

    async function checkStatus() {
        if (stopped || busy) return;

        if (document.hidden) {
            scheduleCheck();
            return;
        }

        busy = true;

        const controller = new AbortController();
        const timeout = window.setTimeout(
            () => controller.abort(),
            8000
        );

        try {
            const response = await fetch(statusUrl, {
                credentials: "same-origin",
                cache: "no-store",
                headers: {
                    "Accept": "application/json"
                },
                signal: controller.signal
            });

            if (response.redirected
                || response.status === 401
                || response.status === 403
                || response.status === 404) {
                stopped = true;

                showNotice(
                    "Automatic updates are unavailable. "
                    + "Reload this page to check the latest status.",
                    false
                );

                return;
            }

            if (!response.ok) return;

            const contentType =
                response.headers.get("content-type") || "";

            if (!contentType.includes("application/json")) {
                stopped = true;

                showNotice(
                    "Automatic updates are unavailable. "
                    + "Reload this page to check the latest status.",
                    false
                );

                return;
            }

            const status = await response.json();
            const latestEmail = String(status.email ?? "");
            const emailChanged = latestEmail !== currentEmail;

            email.textContent = latestEmail;
            currentEmail = latestEmail;

            if (emailChanged) {
                clearPreviousMessages();

                showNotice(
                    `Email verified. The account email is now ${latestEmail}.`,
                    true
                );
            }

            if (!status.requestId) {
                pending.remove();

                if (!emailChanged) {
                    clearPreviousMessages();

                    showNotice(
                        "The email change request is no longer pending. "
                        + `The current email is ${latestEmail}.`,
                        false
                    );
                }

                stopped = true;
                return;
            }

            const pendingAddress = document.getElementById(
                "tutor-details-pending-address"
            );

            if (pendingAddress) {
                pendingAddress.textContent =
                    String(status.pendingEmail ?? "");
            }

            for (const formId of [
                "tutor-details-resend-form",
                "tutor-details-cancel-form"
            ]) {
                const form = document.getElementById(formId);
                const requestInput =
                    form?.elements.namedItem("requestId");

                if (requestInput) {
                    requestInput.value = status.requestId;
                }
            }

            const resendButton = document.querySelector(
                "#tutor-details-resend-form button"
            );

            if (resendButton) {
                resendButton.disabled = !status.isActive;
            }

            const description = document.getElementById(
                "tutor-details-pending-description"
            );

            if (description) {
                description.textContent = status.pendingExpired
                    ? "The verification link has expired. "
                    + "Resend it to issue a new link."
                    : "The current account email remains unchanged "
                    + "until the new address is verified.";
            }
        } catch {
            // Retry temporary network failures.
        } finally {
            window.clearTimeout(timeout);
            busy = false;
            scheduleCheck();
        }
    }

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden && !stopped) {
            window.clearTimeout(timer);
            checkStatus();
        }
    });

    window.addEventListener("pagehide", () => {
        stopped = true;
        window.clearTimeout(timer);
    });

    scheduleCheck();
})();
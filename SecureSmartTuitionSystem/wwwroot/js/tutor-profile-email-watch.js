(() => {
    "use strict";

    const card = document.getElementById("own-profile-card");
    const pending = document.getElementById("own-profile-pending-email");
    const expected = document.getElementById("own-profile-pending-address");
    const notice = document.getElementById("own-email-watch-notice");
    const message = document.getElementById("own-email-watch-message");
    const signIn = document.getElementById("own-email-sign-in");

    if (!card || !pending || !expected || !notice || !message || !signIn) return;
    if (!card.dataset.emailWatchUrl || !card.dataset.emailWatchToken) return;

    const expectedEmail = expected.textContent.trim();
    const url = new URL(card.dataset.emailWatchUrl, window.location.href);
    if (url.origin !== window.location.origin) return;
    url.searchParams.set("statusToken", card.dataset.emailWatchToken);

    let stopped = false;
    let busy = false;
    let timer = null;

    function show(text, success, requireSignIn) {
        document.getElementById("own-profile-success")?.remove();
        document.getElementById("own-profile-warning")?.remove();
        notice.className = success ? "alert alert-success" : "alert alert-info";
        message.textContent = text;
        signIn.hidden = !requireSignIn;
        signIn.classList.toggle("d-none", !requireSignIn);
        notice.hidden = false;
    }

    function disableOldActions() {
        const edit = document.getElementById("own-profile-edit-link");
        if (edit) {
            edit.hidden = true;
            edit.classList.add("d-none");
        }
        pending.querySelectorAll("button").forEach(button => {
            button.disabled = true;
        });
    }

    function schedule() {
        if (stopped) return;
        window.clearTimeout(timer);
        timer = window.setTimeout(check, 5000);
    }

    async function check() {
        if (stopped || busy) return;
        if (document.hidden) {
            schedule();
            return;
        }

        busy = true;
        const controller = new AbortController();
        const timeout = window.setTimeout(() => controller.abort(), 8000);

        try {
            const response = await fetch(url.toString(), {
                // Use only the narrow request proof, not the old login cookie.
                credentials: "omit",
                cache: "no-store",
                referrerPolicy: "no-referrer",
                headers: { "Accept": "application/json" },
                signal: controller.signal
            });

            if (response.status === 429 || response.status >= 500) return;

            if (!response.ok || response.redirected) {
                stopped = true;
                show("Automatic checking has ended. Sign in again to view your latest profile.", false, true);
                return;
            }

            if (!(response.headers.get("content-type") || "").includes("application/json")) {
                stopped = true;
                show("Unable to check this request. Sign in again to view your latest profile.", false, true);
                return;
            }

            const result = await response.json();

            if (result.state === "pending") return;

            if (result.state === "confirmed") {
                disableOldActions();
                const email = document.getElementById("own-profile-email");
                if (email) email.textContent = expectedEmail;
                pending.remove();
                show("Your email has been updated successfully. Please sign in again to continue.", true, true);
                stopped = true;
            } else if (result.state === "expired") {
                const description = document.getElementById("own-profile-pending-description");
                if (description) description.textContent = "The verification link has expired. Resend it to issue a new link.";
                stopped = true;
            } else if (result.state === "cancelled") {
                pending.remove();
                show("The email change request was cancelled. Your account email is unchanged.", false, false);
                stopped = true;
            } else if (result.state === "replaced") {
                pending.querySelectorAll("button").forEach(button => {
                    button.disabled = true;
                });
                show("A newer email change request exists. Reload My Profile to view the latest request.", false, false);
                stopped = true;
            } else {
                disableOldActions();
                show("This account status is unavailable. Please sign in again.", false, true);
                stopped = true;
            }
        } catch {
            // A temporary network failure will be retried.
        } finally {
            window.clearTimeout(timeout);
            busy = false;
            schedule();
        }
    }

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden && !stopped) {
            window.clearTimeout(timer);
            check();
        }
    });
    window.addEventListener("focus", () => {
        if (!stopped) {
            window.clearTimeout(timer);
            check();
        }
    });
    window.addEventListener("pagehide", () => {
        stopped = true;
        window.clearTimeout(timer);
    });

    schedule();
})();

(() => {
    "use strict";
    // Delegation also handles the form restored after cancelling an email request.
    document.addEventListener("click", event => {
        const button = event.target.closest?.(".email-password-toggle");
        if (!button) return;
        const input = button.closest(".email-password-wrapper")?.querySelector("input");
        if (!input) return;
        const revealed = input.type === "password";
        input.type = revealed ? "text" : "password";
        button.setAttribute("aria-label", revealed ? "Hide password" : "Show password");
        button.setAttribute("aria-pressed", String(revealed));
        const icon = button.querySelector("i");
        icon?.classList.toggle("bi-eye", !revealed);
        icon?.classList.toggle("bi-eye-slash", revealed);
    });
    if (!window.fetch || !window.FormData || !window.DOMParser) return;
    let busy = false;
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.id !== "email-cancel-form" || event.defaultPrevented) return;
        const root = form.closest("[data-email-flow]");
        if (!root) return;
        event.preventDefault();
        if (busy) return;
        const endpoint = new URL(form.action, location.href);
        if (endpoint.origin !== location.origin) return;
        busy = true;
        const buttons = Array.from(form.elements).filter(x => x.tagName === "BUTTON");
        const error = root.querySelector("[data-email-cancel-error]");
        const data = new FormData(form);
        if (event.submitter?.name) data.set(event.submitter.name, event.submitter.value);
        buttons.forEach(button => button.disabled = true);
        error?.classList.add("d-none");
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        try {
            const response = await fetch(endpoint, { method:"POST", body:data, credentials:"same-origin", cache:"no-store", signal:controller.signal, headers:{"X-Requested-With":"XMLHttpRequest"} });
            if (response.redirected || response.status === 401 || response.status === 403) throw new Error("Your session may have expired. Reload this page and sign in again.");
            if (!response.ok) {
                let message = "The request could not be cancelled. Reload this page before trying again.";
                try { message = (await response.json()).message || message; } catch {}
                throw new Error(message);
            }
            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const next = page.querySelector("[data-email-flow]");
            if (!next) throw new Error("The form could not be displayed. Reload this page to check your request.");
            const replacement = document.importNode(next, true);
            root.replaceWith(replacement);
            document.dispatchEvent(new Event("email:flow-reset"));
            replacement.querySelector("input[name='NewEmail']")?.focus();
        } catch (failure) {
            if (error) { error.textContent = failure.name === "AbortError" ? "The response timed out. Reload this page to check your request." : failure.message; error.classList.remove("d-none"); }
        } finally {
            clearTimeout(timeout); busy = false;
            buttons.forEach(button => button.disabled = false);
        }
    });
})();

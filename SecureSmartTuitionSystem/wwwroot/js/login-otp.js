
document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("otpCode");
    const container = document.querySelector(".otp-input-container");
    const digits = Array.from(document.querySelectorAll(".otp-digit"));

    if (!input || !container || digits.length !== 6) {
        return;
    }

    const form = input.closest("form");
    let submitting = false;

    function submitCompleteCode() {
        if (form && !submitting && /^[0-9]{6}$/.test(input.value)) {
            form.requestSubmit();
        }
    }

    function render() {
        const value = input.value;
        const focused = document.activeElement === input;

        digits.forEach(function (digit, index) {
            digit.textContent = value[index] || "";

            digit.classList.toggle(
                "is-filled",
                index < value.length
            );

            digit.classList.toggle(
                "is-active",
                focused && index === Math.min(value.length, 5)
            );
        });
    }

    input.addEventListener("input", function () {
        input.value = input.value.replace(/\D/g, "").slice(0, 6);
        render();
        submitCompleteCode();
    });

    input.addEventListener("paste", function (event) {
        const text = event.clipboardData.getData("text");
        const code = text.replace(/\D/g, "").slice(0, 6);

        if (code.length > 0) {
            event.preventDefault();
            input.value = code;
            render();
            submitCompleteCode();
        }
    });

    input.addEventListener("focus", render);
    input.addEventListener("blur", render);

    container.addEventListener("click", function () {
        input.focus();
    });

    if (form) {
        form.addEventListener("submit", function (event) {
            if (submitting) {
                event.preventDefault();
                return;
            }

            submitting = true;
            queueMicrotask(() => {
                if (event.defaultPrevented) submitting = false;
            });
        });
    }

    render();
});

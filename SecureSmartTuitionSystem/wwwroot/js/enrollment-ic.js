(() => {
    "use strict";
    const messages = {
        required: "Please enter the IC number.",
        format: "Please enter a valid IC number (000000-00-0000).",
        date: "The IC number contains an invalid date of birth.",
        birthRequired: "Please enter a valid IC number to fill the date of birth automatically.",
        genderRequired: "Please enter a valid IC number to fill the gender automatically.",
        future: "The date of birth must be earlier than today.",
        age: "The student's age is not within the supported range for Year 1 to Form 5.",
        duplicate: "A student with this IC number already exists. Please sign in to EduSmart to enroll in additional subjects."
    };
    function today() {
        const myt = new Date(Date.now() + 8 * 60 * 60 * 1000);
        return new Date(Date.UTC(myt.getUTCFullYear(), myt.getUTCMonth(), myt.getUTCDate()));
    }
    function format(value) {
        const digits = String(value ?? "").replace(/\D/g, "").slice(0, 12);
        return digits.length > 8 ? digits.slice(0, 6) + "-" + digits.slice(6, 8) + "-" + digits.slice(8)
            : digits.length > 6 ? digits.slice(0, 6) + "-" + digits.slice(6) : digits;
    }
    function parse(value) {
        value = String(value ?? "").trim();
        if (!value) return { error: messages.required };
        if (!/^\d{6}-\d{2}-\d{4}$/.test(value)) return { error: messages.format };
        const digits = value.replace(/-/g, "");
        const yy = Number(digits.slice(0, 2)); const mm = Number(digits.slice(2, 4)); const dd = Number(digits.slice(4, 6));
        const year = yy <= today().getUTCFullYear() % 100 ? 2000 + yy : 1900 + yy;
        const date = new Date(Date.UTC(year, mm - 1, dd));
        if (date.getUTCFullYear() !== year || date.getUTCMonth() !== mm - 1 || date.getUTCDate() !== dd)
            return { error: messages.date };
        return {
            error: "", date, iso: `${year}-${String(mm).padStart(2, "0")}-${String(dd).padStart(2, "0")}`,
            gender: Number(digits[11]) % 2 === 0 ? "Female" : "Male"
        };
    }
    function birthError(iso) {
        if (!iso) return messages.birthRequired;
        const date = new Date(iso + "T00:00:00Z"); const now = today();
        if (Number.isNaN(date.getTime()) || date >= now) return messages.future;
        let age = now.getUTCFullYear() - date.getUTCFullYear();
        if (now.getUTCMonth() < date.getUTCMonth() || (now.getUTCMonth() === date.getUTCMonth() && now.getUTCDate() < date.getUTCDate())) age--;
        return age < 5 || age > 19 ? messages.age : "";
    }
    window.EduSmartIc = { format, parse, birthError, messages };
    function init() {
        const $ = window.jQuery;
        if (!$ || !$.validator || !$.validator.unobtrusive) return;
        $.validator.addMethod("edusmartIc", function (value, element) {
            return this.optional(element) || !parse(value).error;
        }, (_parameters, element) => parse(element.value).error || messages.format);
        $.validator.addMethod("edusmartBirth", function (value, element) {
            return this.optional(element) || !birthError(value);
        }, (_parameters, element) => birthError(element.value));
        const input = document.getElementById("studentIdentificationNumber") || document.getElementById("guardianIc");
        if (!input) return;
        const form = input.form; const $form = $(form);
        if (!$form.data("validator")) $.validator.unobtrusive.parse(form);
        const validator = $form.data("validator");
        if (!validator) return;
        const student = input.id === "studentIdentificationNumber";
        const checkbox = document.getElementById("UseExistingParentAccount");
        $(input).rules("add", {
            required: { depends: () => student || !checkbox?.checked },
            edusmartIc: true,
            messages: { required: messages.required, regex: messages.format, maxlength: messages.format }
        });
        input.setAttribute("aria-describedby", student ? "studentIdentificationNumberValidation" : "guardianIcValidation");
        if (!student) {
            const minimumAge = Number(input.dataset.minParentAge || 18);
            const maximumAge = Number(input.dataset.maxParentAge || 80);
            $.validator.addMethod("edusmartGuardianBirth", function (value, element) {
                const result = parse(value);
                return this.optional(element) || !!result.error || result.date < today();
            }, messages.future);
            $(input).rules("add", { edusmartGuardianBirth: true });
            $.validator.addMethod("edusmartGuardianAge", function (value, element) {
                if (this.optional(element)) return true;
                const result = parse(value);
                if (result.error || result.date >= today()) return true;
                const now = today();
                let age = now.getUTCFullYear() - result.date.getUTCFullYear();
                if (now.getUTCMonth() < result.date.getUTCMonth()
                    || now.getUTCMonth() === result.date.getUTCMonth() && now.getUTCDate() < result.date.getUTCDate())
                    age--;
                return age >= minimumAge && age <= maximumAge;
            }, "The IC number indicates an age outside the allowed range of " + minimumAge + "–" + maximumAge + ".");
            $(input).rules("add", { edusmartGuardianAge: true });
            input.addEventListener("input", () => { input.value = format(input.value); validator.element(input); });
            input.addEventListener("blur", () => validator.element(input));
            if (input.value && !checkbox?.checked) validator.element(input);
            return;
        }
        const birth = document.getElementById("studentDateOfBirth");
        const gender = document.getElementById("studentGender");
        const next = document.getElementById("nextButton");
        const email = document.getElementById("studentEmail");
        const noEmail = document.getElementById("studentHasNoEmail");
        const emailHelp = document.getElementById("studentEmailHelp");
        $(birth).rules("add", { edusmartBirth: true, messages: { required: messages.birthRequired } });
        $(gender).rules("add", { messages: { required: messages.genderRequired } });
        gender.style.pointerEvents = "none"; gender.tabIndex = -1; gender.setAttribute("aria-disabled", "true");
        let duplicate = ""; let busy = false; let version = 0; let request; let timer;
        $.validator.addMethod("edusmartStudentIcAvailable", value => value !== duplicate || !value, messages.duplicate);
        $(input).rules("add", { edusmartStudentIcAvailable: true });
        function updateEmail() {
            if (!email || !noEmail || !emailHelp) return;
            if (noEmail.checked) email.value = "";
            email.disabled = noEmail.checked;
            email.classList.toggle("student-email-disabled", noEmail.checked);
            emailHelp.hidden = !noEmail.checked;
        }
        noEmail?.addEventListener("change", updateEmail); updateEmail();
        function clearFieldError(field) {
            delete validator.invalid[field.name]; delete validator.submitted[field.name];
            $(field).removeClass("input-validation-error is-invalid").removeAttr("aria-invalid");
            const span = form.querySelector('[data-valmsg-for="' + field.name + '"]');
            if (span) { span.textContent = ""; span.classList.remove("field-validation-error"); span.classList.add("field-validation-valid"); }
            validator.errors().filter((_, el) => el.getAttribute("for") === field.id).remove();
        }
        function populate(validate) {
            const result = parse(input.value);
            birth.value = result.error ? "" : result.iso;
            gender.value = result.error ? "" : result.gender;
            if (validate) {
                if (result.error) { clearFieldError(birth); clearFieldError(gender); }
                else { validator.element(birth); validator.element(gender); }
            }
            return !result.error && !birthError(birth.value);
        }
        function updateButton() {
            next.disabled = busy || (input.value !== "" && (!!parse(input.value).error || !!birthError(birth.value))) || (duplicate === input.value && !!duplicate);
        }
        async function checkExisting(checkVersion) {
            const value = input.value;
            if (parse(value).error || birthError(birth.value)) { updateButton(); return; }
            busy = true; updateButton(); request = new AbortController();
            try {
                const url = new URL(input.dataset.checkUrl, location.origin); url.searchParams.set("icNumber", value);
                const response = await fetch(url, { signal: request.signal, cache: "no-store", credentials: "same-origin" });
                if (!response.ok) return;
                const data = await response.json();
                if (checkVersion !== version || input.value !== value) return;
                duplicate = data.exists ? value : "";
                validator.element(input);
            } catch { /* Submission repeats the duplicate check on the server. */ }
            finally { if (checkVersion === version) { busy = false; updateButton(); } }
        }
        input.addEventListener("input", () => {
            version++; clearTimeout(timer); request?.abort(); busy = false; duplicate = "";
            input.value = format(input.value);
            const valid = populate(true); validator.element(input); updateButton();
            if (valid) { const current = version; busy = true; updateButton(); timer = setTimeout(() => checkExisting(current), 300); }
        });
        input.addEventListener("blur", () => validator.element(input));
        const initiallyValid = populate(false);
        if (input.value) {
            validator.element(input);
            if (!parse(input.value).error) validator.element(birth);
        }
        if (initiallyValid) checkExisting(version); else updateButton();
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();

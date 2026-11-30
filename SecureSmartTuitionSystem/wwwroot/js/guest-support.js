(function () {
    'use strict';
    document.addEventListener('DOMContentLoaded', function () {
        const form = document.getElementById('guestSupportForm');
        if (!form) return;
        const name = form.querySelector('[name="Name"]');
        const email = form.querySelector('[name="Email"]');
        const $ = window.jQuery;
        if ($ && $.validator && $.validator.unobtrusive) $.validator.unobtrusive.parse(form);
        if ($ && $.validator) [name, email].filter(Boolean).forEach(function (field) {
            $(field).rules('add', { normalizer: function (value) { return value.trim() ? value : ''; } });
        });
        function validate(field) {
            if ($ && $.validator) { $(field).valid(); return; }
            // Field-level fallback if validation libraries are unavailable.
            const value = field.value;
            const pattern = field.getAttribute('data-val-regex-pattern');
            const isEmail = field === email;
            let message = '';
            if (!value.trim()) message = isEmail ? 'Email field is required.' : 'Name field is required.';
            else if (pattern && !new RegExp('^(?:' + pattern + ')$').test(value))
                message = isEmail ? 'Please enter a valid email address.' : 'Name can only contain A-Z, a-z, spaces, / and -.';
            else if (isEmail && field.validity.typeMismatch) message = 'Please enter a valid email address.';
            else if (field.maxLength > 0 && value.length > field.maxLength) message = 'This field is too long.';
            const target = form.querySelector('[data-valmsg-for="' + field.name + '"]');
            if (target) {
                target.textContent = message;
                target.classList.toggle('field-validation-error', Boolean(message));
                target.classList.toggle('field-validation-valid', !message);
            }
            field.setCustomValidity(message);
        }
        [name, email].filter(Boolean).forEach(function (field) {
            field.addEventListener('input', function () { validate(field); });
            field.addEventListener('blur', function () { validate(field); });
        });
    });
})();

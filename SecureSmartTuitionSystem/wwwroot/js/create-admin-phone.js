(() => {
    "use strict";
    const phone = document.querySelector(".create-admin-page input[name='PhoneNumber']");
    if (!phone) return;
    function format(value) {
        let digits = value.replace(/\D/g, "");
        if (digits.startsWith("60")) digits = "0" + digits.slice(2);
        digits = digits.slice(0, 11);
        return digits.length > 3 ? digits.slice(0, 3) + "-" + digits.slice(3) : digits;
    }
    phone.addEventListener("input", () => { phone.value = format(phone.value); });
    // Handle the whole paste before maxlength could cut off an international prefix.
    phone.addEventListener("paste", event => {
        if (!event.clipboardData) return;
        event.preventDefault();
        const start = phone.selectionStart ?? phone.value.length;
        const end = phone.selectionEnd ?? start;
        phone.value = format(phone.value.slice(0, start) + event.clipboardData.getData("text") + phone.value.slice(end));
        phone.dispatchEvent(new Event("input", { bubbles: true }));
    });
    phone.addEventListener("blur", () => { phone.value = format(phone.value); });
})();

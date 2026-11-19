(() => {
    "use strict";
    const form = document.getElementById("qualification-request-form");
    if (!form) return;
    let submitting = false;
    const initial = () => Array.from(form.querySelectorAll("input:not([type='hidden']):not([type='file']),textarea"), item => item.value).join("\u0000");
    const baseline = initial();
    const dirty = () => initial() !== baseline || form.elements.namedItem("Certificates").files.length > 0;
    form.addEventListener("submit", event => { if (!event.defaultPrevented && form.checkValidity()) submitting = true; });
    document.addEventListener("click", event => {
        const link = event.target.closest?.("a[href]");
        if (!link || event.defaultPrevented || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || link.target === "_blank" || !dirty()) return;
        const target = new URL(link.href, location.href);
        if (target.href === location.href || target.origin !== location.origin) return;
        if (!confirm("You have unsaved changes. Leave without submitting?")) { event.preventDefault(); event.stopImmediatePropagation(); }
        else submitting = true;
    });
    window.addEventListener("beforeunload", event => { if (!submitting && dirty()) { event.preventDefault(); event.returnValue = ""; } });
    window.addEventListener("pageshow", () => { submitting = false; });
})();

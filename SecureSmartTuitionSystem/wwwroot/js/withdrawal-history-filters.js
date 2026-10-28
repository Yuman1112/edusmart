"use strict";
document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("withdrawalHistoryFilters");
    if (!form) return;
    const level = form.querySelector("[name=level]");
    const subject = form.querySelector("[name=subject]");
    form.addEventListener("change", event => {
        if (event.target.name === "stage") { level.value = ""; subject.value = ""; }
        if (event.target.name === "level") subject.value = "";
    });
});

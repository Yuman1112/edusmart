document.addEventListener("DOMContentLoaded", function () {
    const publishInput = document.getElementById("AnnouncementPublishAt");
    const unitInput = document.getElementById("AnnouncementExpiryUnit");
    const durationInput = document.getElementById("AnnouncementExpiryDuration");
    const preview = document.getElementById("announcementExpiryPreview");

    if (!publishInput || !unitInput || !durationInput || !preview) return;

    function parseLocalDateTime(value) {
        if (!value) return null;
        const parts = value.split("T");
        if (parts.length !== 2) return null;
        const date = parts[0].split("-").map(Number);
        const time = parts[1].split(":").map(Number);
        if (date.length !== 3 || time.length < 2 ||
            [...date, ...time].some(Number.isNaN)) return null;
        return new Date(date[0], date[1] - 1, date[2], time[0], time[1], 0, 0);
    }

    function maximumFor(unit) {
        if (unit === "Days") return 365;
        if (unit === "Weeks") return 52;
        if (unit === "Months") return 12;
        return 0;
    }

    function calculateExpiry(publishAt, unit, duration) {
        const result = new Date(
            publishAt.getFullYear(), publishAt.getMonth(), publishAt.getDate(),
            23, 59, 59, 0);
        if (unit === "Days") result.setDate(result.getDate() + duration);
        else if (unit === "Weeks") result.setDate(result.getDate() + duration * 7);
        else if (unit === "Months") result.setMonth(result.getMonth() + duration);
        return result;
    }

    function formatExpiry(value) {
        return new Intl.DateTimeFormat("en-GB", {
            day: "2-digit", month: "short", year: "numeric",
            hour: "2-digit", minute: "2-digit", hour12: true
        }).format(value).replace(",", "");
    }

    function refreshExpiry() {
        const unit = unitInput.value;
        const noExpiry = unit === "None";
        const maximum = maximumFor(unit);

        durationInput.disabled = noExpiry;
        durationInput.required = !noExpiry;
        durationInput.max = maximum || 365;
        durationInput.placeholder = noExpiry ? "Not required" : `1 - ${maximum}`;
        durationInput.setCustomValidity("");

        if (noExpiry) {
            durationInput.value = "";
            preview.innerHTML =
                '<i class="bi bi-infinity"></i>This announcement will not expire.';
            return true;
        }

        const publishAt = parseLocalDateTime(publishInput.value);
        const duration = Number.parseInt(durationInput.value, 10);

        if (!publishAt) {
            preview.textContent = "Select the publish date and time first.";
            return false;
        }

        if (!Number.isInteger(duration) || duration < 1 || duration > maximum) {
            const message = `Enter a duration between 1 and ${maximum}.`;
            durationInput.setCustomValidity(message);
            preview.textContent = message;
            return false;
        }

        const expiry = calculateExpiry(publishAt, unit, duration);
        preview.innerHTML =
            '<i class="bi bi-clock-history"></i>This announcement will expire on ' +
            formatExpiry(expiry) + ".";
        return true;
    }

    publishInput.addEventListener("input", refreshExpiry);
    publishInput.addEventListener("change", refreshExpiry);
    unitInput.addEventListener("change", refreshExpiry);
    durationInput.addEventListener("input", refreshExpiry);

    durationInput.form?.addEventListener("submit", function (event) {
        if (!refreshExpiry()) {
            event.preventDefault();
            durationInput.reportValidity();
            durationInput.focus();
        }
    });

    refreshExpiry();
});

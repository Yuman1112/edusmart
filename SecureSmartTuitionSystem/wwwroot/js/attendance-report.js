(() => {
    const message = document.getElementById("attendance-refresh-status");
    if (!message) return;
    let busy = false;
    const timer = setInterval(async () => {
        if (document.hidden || busy) return;
        busy = true;
        try {
            const url = new URL(message.dataset.refreshUrl, location.origin);
            const response = await fetch(url, { credentials: "same-origin", cache: "no-store", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok || response.redirected) throw new Error("Session unavailable");
            const html = new DOMParser().parseFromString(await response.text(), "text/html");
            const next = html.querySelector("[data-report-results]");
            const current = document.querySelector("[data-report-results]");
            if (!next || !current) throw new Error("Report unavailable");
            current.replaceWith(next);
            window.EduSmartTables?.enhance(next);
            const count = Number(next.dataset.reportTotal);
            const unavailable = count === 0 || count > 5000;
            const exportButton = document.getElementById("attendance-export");
            const printLink = document.getElementById("attendance-print-link");
            if (exportButton) exportButton.disabled = unavailable;
            if (printLink) printLink.hidden = unavailable;
            message.textContent = "Records updated " + new Date().toLocaleTimeString() + ".";
        } catch { message.textContent = "Automatic refresh is unavailable. Reload the page to check the latest records."; }
        finally { busy = false; }
    }, 20000);
    window.addEventListener("pagehide", () => clearInterval(timer), { once: true });
})();
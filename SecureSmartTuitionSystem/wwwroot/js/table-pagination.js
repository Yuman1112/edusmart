(() => {
    const size = 25;
    function enhance(root = document) {
        root.querySelectorAll("table").forEach(table => {
            if (table.dataset.serverPaged === "true" || table.dataset.tablePaged === "true") return;
            table.dataset.tablePaged = "true";
            let page = 1;
            const nav = document.createElement("nav");
            nav.className = "table-pagination"; nav.setAttribute("aria-label", "Table pages");
            const summary = document.createElement("span"); summary.className = "table-pagination-summary";
            const controls = document.createElement("div"); controls.className = "table-pagination-controls";
            nav.append(summary, controls);
            const wrapper = table.closest(".table-responsive"); (wrapper || table).after(nav);
            function draw(returnFocus = false) {
                // Read current order so table sorting and inserted/removed rows remain correct.
                const rows = Array.from(table.tBodies).flatMap(body => Array.from(body.rows))
                    .filter(row => !(row.cells.length === 1 && row.cells[0].colSpan > 1));
                const pages = Math.max(1, Math.ceil(rows.length / size));
                page = Math.max(1, Math.min(page, pages));
                rows.forEach((row, index) => row.classList.toggle("table-page-hidden", index < (page - 1) * size || index >= page * size));
                summary.textContent = rows.length
                    ? `${(page - 1) * size + 1}–${Math.min(page * size, rows.length)} of ${rows.length} rows · 25 per page`
                    : "0 rows · 25 per page";
                controls.replaceChildren();
                function button(label, target, disabled = false, active = false) {
                    const el = document.createElement("button"); el.type = "button"; el.className = "table-page-button";
                    el.textContent = label; el.disabled = disabled;
                    if (active) { el.classList.add("active"); el.setAttribute("aria-current", "page"); }
                    el.addEventListener("click", () => { page = target; draw(true); }); controls.append(el);
                }
                button("First", 1, page === 1); button("Previous", page - 1, page === 1);
                for (let number = Math.max(1, page - 2); number <= Math.min(pages, page + 2); number++)
                    button(String(number), number, false, number === page);
                button("Next", page + 1, page === pages); button("Last", pages, page === pages);
                if (returnFocus) controls.querySelector('[aria-current="page"]')?.focus();
            }
            draw();
            const observer = new MutationObserver(() => { page = 1; draw(); });
            Array.from(table.tBodies).forEach(body => observer.observe(body, { childList: true }));
        });
    }
    window.EduSmartTables = { enhance };
    document.addEventListener("DOMContentLoaded", () => enhance());
})();

(() => {
    const size = 25;
    // Give each table its own card when its page does not already provide one.
    // Keep an existing card when the scroll region is its direct child.
    function tableCard(table) {
        const scroll = table.closest('.table-responsive, .billing-table-wrapper, .payment-table-wrapper, .receipt-table-wrapper, .availability-scroll, .reports-table-wrap, .enrollment-duplicate-table-wrap');
        const region = scroll || table;
        if (region.parentElement?.classList.contains('shared-table-card')) return region.parentElement;
        const parent = region.parentElement;
        const existingCard = parent?.matches('.dashboard-card, .card, .billing-table-card, .billing-detail-card, .payment-card, .receipt-card, .tutor-management-list-card, .class-form-card, .class-details-card, .reports-card');
        if (existingCard && parent.querySelectorAll('table').length === 1) {
            parent.classList.add('shared-table-card');
            return parent;
        }
        const card = document.createElement('div');
        card.className = 'shared-table-card';
        region.before(card);
        card.append(region);
        return card;
    }
    function enhance(root = document) {
        root.querySelectorAll("table").forEach(table => {
            const card = tableCard(table);
            if (table.dataset.pagination === "off" || table.dataset.serverPaged === "true" || table.dataset.tablePaged === "true") return;
            table.dataset.tablePaged = "true";
            let page = 1;
            const nav = document.createElement("nav");
            nav.className = "table-pagination"; nav.setAttribute("aria-label", "Table pages");
            const summary = document.createElement("span"); summary.className = "table-pagination-summary";
            const controls = document.createElement("div"); controls.className = "table-pagination-controls";
            nav.append(summary, controls);
            card.append(nav);
            function draw(returnFocus = false) {
                // Read current order so table sorting and inserted/removed rows remain correct.
                const rows = Array.from(table.tBodies).flatMap(body => Array.from(body.rows))
                    .filter(row => !(row.cells.length === 1 && row.cells[0].colSpan > 1));
                // Document pages keep the controls hidden when all items fit.
                nav.hidden = table.dataset.pagination === "when-needed" && rows.length <= size;
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

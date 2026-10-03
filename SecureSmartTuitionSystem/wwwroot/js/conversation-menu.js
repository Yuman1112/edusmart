document.addEventListener("DOMContentLoaded", () => {
    const menu = document.getElementById("conversationContextMenu");
    const form = document.getElementById("conversationPreferenceForm");
    const dialog = document.getElementById("deleteConversationDialog");
    if (!menu || !form || !dialog) return;
    let selected = null, returnFocus = null;
    const commands = Array.from(menu.querySelectorAll("[data-conversation-command]"));
    function close(restore = false) {
        menu.hidden = true;
        document.querySelectorAll(".conversation-options").forEach(button => button.setAttribute("aria-expanded", "false"));
        if (restore) returnFocus?.focus();
    }
    function open(row, x, y, focusTarget) {
        if (dialog.open) return;
        selected = row; returnFocus = focusTarget;
        menu.querySelector("[data-pin-label]").textContent = row.dataset.pinned === "true" ? "Unpin" : "Pin";
        menu.hidden = false;
        menu.style.left = Math.max(8, Math.min(x, window.innerWidth - menu.offsetWidth - 8)) + "px";
        menu.style.top = Math.max(8, Math.min(y, window.innerHeight - menu.offsetHeight - 8)) + "px";
        document.querySelectorAll(".conversation-options").forEach(button => button.setAttribute("aria-expanded", "false"));
        row.querySelector(".conversation-options")?.setAttribute("aria-expanded", "true");
        commands[0]?.focus();
    }
    function submit(operation) {
        if (!selected) return;
        form.elements.namedItem("conversationId").value = selected.dataset.id;
        form.elements.namedItem("operation").value = operation;
        form.elements.namedItem("pinned").value = String(selected.dataset.pinned !== "true");
        form.requestSubmit();
    }
    document.querySelectorAll("[data-conversation-row]").forEach(row => {
        row.addEventListener("contextmenu", event => {
            event.preventDefault(); open(row, event.clientX, event.clientY, row.querySelector(".conversation-item"));
        });
        const button = row.querySelector(".conversation-options");
        button?.addEventListener("click", event => {
            event.preventDefault(); event.stopPropagation();
            if (!menu.hidden && selected === row) { close(true); return; }
            const rect = button.getBoundingClientRect(); open(row, rect.left, rect.bottom, button);
        });
        row.addEventListener("keydown", event => {
            if (event.key === "ContextMenu" || (event.shiftKey && event.key === "F10")) {
                event.preventDefault(); const rect = row.getBoundingClientRect(); open(row, rect.left, rect.bottom, event.target);
            }
        });
    });
    menu.addEventListener("click", event => {
        const command = event.target.closest("[data-conversation-command]")?.dataset.conversationCommand;
        if (!selected || !command) return;
        close();
        if (command === "open") window.location.assign(selected.querySelector(".conversation-item").href);
        if (command === "pin") submit("pin");
        if (command === "delete") {
            dialog.querySelector("[data-delete-name]").textContent = selected.dataset.name;
            dialog.showModal(); dialog.querySelector("[data-delete-cancel]").focus();
        }
    });
    menu.addEventListener("keydown", event => {
        const index = commands.indexOf(document.activeElement);
        let next;
        if (event.key === "ArrowDown") next = (index + 1) % commands.length;
        if (event.key === "ArrowUp") next = (index + commands.length - 1) % commands.length;
        if (event.key === "Home") next = 0;
        if (event.key === "End") next = commands.length - 1;
        if (next !== undefined) { event.preventDefault(); commands[next]?.focus(); }
    });
    document.addEventListener("click", event => { if (!menu.contains(event.target)) close(); });
    document.addEventListener("keydown", event => { if (event.key === "Escape" && !menu.hidden) { event.preventDefault(); close(true); } });
    window.addEventListener("resize", () => close());
    document.addEventListener("scroll", () => close(), true);
    dialog.querySelector("[data-delete-cancel]").addEventListener("click", () => dialog.close());
    dialog.querySelector("[data-delete-confirm]").addEventListener("click", () => { dialog.close(); submit("delete"); });
    dialog.addEventListener("close", () => returnFocus?.focus());
});

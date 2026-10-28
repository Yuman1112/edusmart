document.addEventListener("DOMContentLoaded", function () {
    document.body.classList.add("messages-page-active");

    const messages = document.getElementById("chatMessages");
    const input = document.getElementById("messageContent");
    const fileInput = document.getElementById("messageAttachment");
    const fileName = document.getElementById("selectedFileName");

    if (messages) {
        messages.scrollTop = messages.scrollHeight;
    }

    if (input) {
        const resize = () => {
            input.style.height = "auto";
            input.style.height = Math.min(input.scrollHeight, 110) + "px";
        };
        input.addEventListener("input", resize);
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                document.getElementById("messageComposer")?.requestSubmit();
            }
        });
    }

    fileInput?.addEventListener("change", function () {
        if (!fileName) return;
        const file = fileInput.files?.[0];
        fileName.hidden = !file;
        fileName.textContent = file ? "Attached: " + file.name : "";
    });

    const editModalElement = document.getElementById("editMessageModal");
    const editMessageId = document.getElementById("editMessageId");
    const editMessageContent = document.getElementById("editMessageContent");

    document.querySelectorAll(".edit-message-button").forEach(function (button) {
        button.addEventListener("click", function () {
            if (!editModalElement || !editMessageId || !editMessageContent) return;
            editMessageId.value = button.dataset.messageId || "";
            editMessageContent.value = button.dataset.messageContent || "";
            bootstrap.Modal.getOrCreateInstance(editModalElement).show();
            window.setTimeout(() => editMessageContent.focus(), 200);
        });
    });

    document.querySelectorAll(".confirm-delete-for-me").forEach(function (button) {
        button.closest("form")?.addEventListener("submit", function (event) {
            if (!window.confirm("Delete this message from your view?")) {
                event.preventDefault();
            }
        });
    });

    document.querySelectorAll(".confirm-delete-for-everyone").forEach(function (button) {
        button.closest("form")?.addEventListener("submit", function (event) {
            if (!window.confirm("Delete this message for everyone? This cannot be undone.")) {
                event.preventDefault();
            }
        });
    });

    const chatPanel = document.querySelector(".chat-panel[data-conversation-id]");
    const conversationId = chatPanel?.dataset.conversationId;
    const readStatusUrl = chatPanel?.dataset.readStatusUrl;

    let markingRead = false;
    async function markVisibleMessagesRead() {
        if (markingRead || !conversationId || document.hidden) return;
        const endpoint = chatPanel?.dataset.markReadUrl;
        const token = document.querySelector('#messageComposer input[name="__RequestVerificationToken"]')?.value;
        if (!endpoint || !token) return;
        markingRead = true;
        try {
            const response = await fetch(endpoint, {
                method: "POST", credentials: "same-origin", cache: "no-store",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                body: new URLSearchParams({
                    __RequestVerificationToken: token,
                    conversationId,
                    lastVisibleMessageId: chatPanel.dataset.lastVisibleMessageId || "0"
                })
            });
            if (response.ok) document.dispatchEvent(new Event("edusmart:unread-changed"));
        } catch { /* A failed acknowledgement leaves unread messages unchanged. */ }
        finally { markingRead = false; }
    }
    markVisibleMessagesRead();
    document.addEventListener("visibilitychange", markVisibleMessagesRead);

    async function refreshReadStatuses() {
        if (!conversationId || !readStatusUrl || document.hidden) return;

        try {
            const url = new URL(readStatusUrl, window.location.origin);
            url.searchParams.set("conversationId", conversationId);

            const response = await fetch(url, {
                method: "GET",
                cache: "no-store",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!response.ok) return;

            const result = await response.json();
            (result.readMessageIds || []).forEach(function (messageId) {
                const status = document.querySelector(
                    `.message-delivery-status[data-message-id="${messageId}"]`);

                if (!status) return;
                status.classList.remove("bi-check2");
                status.classList.add("bi-check2-all", "read");
                status.title = "Read";

                const bubble = status.closest(".message-bubble");
                bubble?.querySelector(".edit-message-button")?.closest("li")?.remove();
            });
        } catch {
            // Keep the conversation usable if a background status check fails.
        }
    }

    if (conversationId && readStatusUrl) {
        window.setInterval(refreshReadStatuses, 4000);
        document.addEventListener("visibilitychange", refreshReadStatuses);
    }
});

(() => {
    const input = document.getElementById("conversationSearch");
    if (!input) return;
    let tab = "messages";
    const media = document.getElementById("conversationMedia");
    const files = document.getElementById("conversationFiles");
    function applyFilter() {
        const value = input.value.trim().toLocaleLowerCase();
        document.querySelectorAll("#chatMessages .message-bubble-row").forEach(row => {
            row.hidden = tab !== "messages" || (Boolean(value) && !row.textContent.toLocaleLowerCase().includes(value));
        });
        [media, files].forEach((panel, index) => {
            if (!panel) return;
            panel.hidden = tab !== (index === 0 ? "media" : "files");
            let visible = 0;
            panel.querySelectorAll("[data-media-search]").forEach(item => {
                item.hidden = Boolean(value) && !item.dataset.mediaSearch.toLocaleLowerCase().includes(value);
                if (!item.hidden) visible++;
            });
            const empty = panel.querySelector(".chat-media-empty");
            if (empty) empty.hidden = visible !== 0;
        });
    }
    input.addEventListener("input", applyFilter);
    document.querySelectorAll("[data-chat-tab]").forEach(button => button.addEventListener("click", () => {
        tab = button.dataset.chatTab;
        document.querySelectorAll("[data-chat-tab]").forEach(other => {
            const selected = other === button;
            other.setAttribute("aria-pressed", String(selected));
            other.classList.toggle("btn-primary", selected);
            other.classList.toggle("btn-outline-primary", !selected);
        });
        applyFilter();
    }));
})();

(() => {
    const search = document.getElementById("contactSearch"), select = document.getElementById("parentUserId"), list = document.getElementById("contactSearchResults"), status = document.getElementById("contactSearchStatus"), role = document.getElementById("contactRole");
    if (!search || !select || !list || !status) return;
    const form = select.form;
    const contacts = Array.from(select.options).filter(x => x.value).map(x => ({ id: x.value, label: x.textContent.trim(), role: x.parentElement.label || "" }));
    let found = [], active = -1, selected = null;
    // Retain the original select for its server-side field name and no-JS fallback.
    select.hidden = true; select.required = false; search.hidden = false; search.required = true;
    function close() { list.hidden = true; search.setAttribute("aria-expanded", "false"); search.removeAttribute("aria-activedescendant"); active = -1; }
    function choose(contact) {
        selected = contact; select.value = contact.id; search.value = contact.label;
        search.setCustomValidity(""); close(); status.textContent = `Selected ${contact.label}. Press + to open.`;
    }
    function refresh() {
        const term = selected && search.value === selected.label ? "" : search.value.trim().toLocaleLowerCase();
        found = contacts.filter(x => (!role?.value || x.role === role.value) && (!term || x.label.toLocaleLowerCase().includes(term)));
        list.replaceChildren(); active = -1; search.removeAttribute("aria-activedescendant");
        found.forEach((contact, index) => {
            const item = document.createElement("li"); item.id = `contact-option-${index}`;
            item.setAttribute("role", "option"); item.setAttribute("aria-selected", String(selected?.id === contact.id));
            item.textContent = `${contact.label} · ${contact.role}`;
            item.addEventListener("mousedown", event => event.preventDefault());
            item.addEventListener("click", () => choose(contact)); list.append(item);
        });
        list.hidden = false; list.scrollTop = 0; search.setAttribute("aria-expanded", "true");
        status.textContent = found.length ? `${found.length} ${term ? "matching" : "available"} contact(s). Scroll for more.` : "No matching contacts.";
        if (!found.length) { const item = document.createElement("li"); item.textContent = "No matching contacts"; item.className = "contact-no-results"; list.append(item); }
    }
    search.addEventListener("focus", () => { search.placeholder = "Type a name or ID…"; refresh(); });
    search.addEventListener("click", () => { if (list.hidden) refresh(); });
    search.addEventListener("blur", () => { search.placeholder = "Select contact"; close(); });
    search.addEventListener("input", () => { selected = null; select.value = ""; search.setCustomValidity(""); refresh(); });
    role?.addEventListener("change", () => { selected = null; select.value = ""; search.value = ""; search.setCustomValidity(""); refresh(); });
    search.addEventListener("keydown", event => {
        if (event.key === "Escape") { event.preventDefault(); close(); return; }
        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault(); if (list.hidden) refresh(); if (!found.length) return;
            active = event.key === "ArrowDown" ? Math.min(active + 1, found.length - 1) : (active < 0 ? found.length - 1 : Math.max(active - 1, 0));
            Array.from(list.children).forEach((item, index) => item.classList.toggle("contact-active", index === active));
            const item = list.children[active]; search.setAttribute("aria-activedescendant", item.id); item.scrollIntoView({ block: "nearest" });
        }
        if (event.key === "Enter" && !list.hidden) { event.preventDefault(); if (active >= 0) choose(found[active]); }
    });
    document.addEventListener("click", event => { if (!form.contains(event.target)) close(); });
    form.addEventListener("submit", event => {
        if (!selected || select.value !== selected.id || search.value !== selected.label) {
            event.preventDefault(); search.setCustomValidity("Choose a contact from the results first."); search.reportValidity();
        }
    });
    status.textContent = "Choose a contact to start a conversation.";
})();

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
    const contacts = Array.from(select.options).filter(x => x.value).map(x => ({ id: x.value, label: x.textContent, role: x.parentElement.label || "" }));
    function matches(contact) {
        const term = search.value.trim().toLocaleLowerCase();
        return (!role?.value || contact.role === role.value) && (!term || `${contact.label} ${contact.role}`.toLocaleLowerCase().includes(term));
    }
    function refresh() {
        const previous = select.value; const found = contacts.filter(matches);
        select.replaceChildren();
        const placeholder = document.createElement("option"); placeholder.value = ""; placeholder.textContent = "Select contact"; select.append(placeholder);
        for (const roleName of [...new Set(found.map(x => x.role))]) {
            const group = document.createElement("optgroup"); group.label = roleName;
            found.filter(x => x.role === roleName).forEach(contact => { const option = document.createElement("option"); option.value = contact.id; option.textContent = contact.label; group.append(option); }); select.append(group);
        }
        select.value = found.some(x => x.id === previous) ? previous : "";
        list.replaceChildren(); list.hidden = !search.value.trim();
        status.textContent = found.length ? `${found.length} matching contact(s). Choose a result or use the dropdown.` : "No matching contacts.";
        found.slice(0, 50).forEach(contact => {
            const item = document.createElement("li"), button = document.createElement("button"); button.type = "button"; button.textContent = `${contact.label} · ${contact.role}`;
            button.addEventListener("click", () => { select.value = contact.id; list.hidden = true; status.textContent = `Selected ${contact.label}. Press + to open.`; }); item.append(button); list.append(item);
        });
        if (found.length > 50) status.textContent += " Showing the first 50; type more to narrow the results.";
    }
    search.addEventListener("input", () => { select.value = ""; refresh(); });
    role?.addEventListener("change", () => { select.value = ""; refresh(); });
    search.addEventListener("keydown", event => { if (event.key === "Enter") { event.preventDefault(); list.querySelector("button")?.focus(); } if (event.key === "Escape") list.hidden = true; });
    select.addEventListener("change", () => { list.hidden = true; status.textContent = select.value ? `Selected ${select.selectedOptions[0].textContent}. Press + to open.` : "Choose a contact."; });
    refresh();
})();

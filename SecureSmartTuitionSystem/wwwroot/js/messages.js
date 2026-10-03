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

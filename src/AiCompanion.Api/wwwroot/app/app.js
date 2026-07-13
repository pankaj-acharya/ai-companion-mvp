const elements = {
    apiBaseLabel: document.getElementById("apiBaseLabel"),
    chatForm: document.getElementById("chatForm"),
    clearMessagesButton: document.getElementById("clearMessagesButton"),
    loadHistoryButton: document.getElementById("loadHistoryButton"),
    messageInput: document.getElementById("messageInput"),
    messages: document.getElementById("messages"),
    metaHistoryCount: document.getElementById("metaHistoryCount"),
    metaSession: document.getElementById("metaSession"),
    metaTokens: document.getElementById("metaTokens"),
    metaTransport: document.getElementById("metaTransport"),
    modeBadge: document.getElementById("modeBadge"),
    personaId: document.getElementById("personaId"),
    sendButton: document.getElementById("sendButton"),
    sessionId: document.getElementById("sessionId"),
    statusBanner: document.getElementById("statusBanner"),
    streamMode: document.getElementById("streamMode"),
    template: document.getElementById("messageTemplate"),
    userId: document.getElementById("userId")
};

elements.apiBaseLabel.textContent = window.location.origin;

initialize();

function initialize() {
    setStatus("Ready.", "secondary");
    setEmptyState();
    syncSessionMeta();
    updateModeLabel();

    elements.streamMode.addEventListener("change", updateModeLabel);
    elements.sessionId.addEventListener("input", syncSessionMeta);
    elements.clearMessagesButton.addEventListener("click", () => {
        elements.messages.innerHTML = "";
        setEmptyState();
        setStatus("Conversation view cleared.", "secondary");
        elements.metaHistoryCount.textContent = "0";
        elements.metaTokens.textContent = "-";
    });
    elements.loadHistoryButton.addEventListener("click", loadHistory);
    elements.chatForm.addEventListener("submit", submitMessage);
}

function updateModeLabel() {
    const mode = elements.streamMode.checked ? "WebSocket" : "REST";
    elements.modeBadge.textContent = `${mode} mode`;
    elements.metaTransport.textContent = mode;
}

function syncSessionMeta() {
    elements.metaSession.textContent = readConfig().sessionId;
}

function setEmptyState() {
    if (elements.messages.children.length > 0) {
        return;
    }

    appendMessage({
        role: "system",
        content: "No conversation loaded yet. Send a message or load history for the active session.",
        createdAt: new Date().toISOString()
    });
}

async function submitMessage(event) {
    event.preventDefault();

    const config = readConfig();
    const message = elements.messageInput.value.trim();
    if (!message) {
        setStatus("Message is required.", "warning");
        return;
    }

    appendUserMessage(message);
    elements.messageInput.value = "";
    setBusy(true);

    try {
        if (config.streaming) {
            await sendStreamingMessage(config, message);
        } else {
            await sendRestMessage(config, message);
        }
    } catch (error) {
        appendMessage({ role: "error", content: formatError(error), createdAt: new Date().toISOString() });
        setStatus(formatError(error), "danger");
    } finally {
        setBusy(false);
    }
}

async function sendRestMessage(config, message) {
    setStatus("Sending message over REST...", "info");

    const response = await fetch("/api/v1/chat", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${config.userId}`
        },
        body: JSON.stringify({
            session_id: config.sessionId,
            message,
            persona_id: config.personaId || undefined
        })
    });

    const body = await readJson(response);
    if (!response.ok) {
        throw new Error(extractApiError(body, "Chat request failed."));
    }

    appendMessage({ role: "assistant", content: body.response, createdAt: body.created_at });
    elements.metaTokens.textContent = String(body.tokens_used ?? "-");
    setStatus("REST response received.", "success");
}

async function sendStreamingMessage(config, message) {
    setStatus("Opening WebSocket stream...", "info");

    const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
    const socketUrl = `${protocol}//${window.location.host}/ws/chat/${encodeURIComponent(config.sessionId)}?token=${encodeURIComponent(config.userId)}`;
    const socket = new WebSocket(socketUrl);

    const assistantMessage = appendMessage({
        role: "assistant",
        content: "",
        createdAt: new Date().toISOString()
    });

    return await new Promise((resolve, reject) => {
        let finalized = false;

        socket.addEventListener("open", () => {
            socket.send(JSON.stringify({
                message,
                persona_id: config.personaId || undefined
            }));
        });

        socket.addEventListener("message", event => {
            const payload = JSON.parse(event.data);
            if (payload.type === "token") {
                assistantMessage.querySelector(".message-content").textContent += payload.content;
                scrollMessagesToBottom();
                return;
            }

            if (payload.type === "error") {
                finalized = true;
                socket.close();
                reject(new Error(formatWsError(payload.detail)));
                return;
            }

            if (payload.type === "done") {
                finalized = true;
                elements.metaTokens.textContent = String(payload.tokens_used ?? "-");
                setStatus("Streaming response completed.", "success");
                socket.close();
                resolve();
            }
        });

        socket.addEventListener("error", () => {
            if (!finalized) {
                reject(new Error("WebSocket connection failed."));
            }
        });

        socket.addEventListener("close", event => {
            if (!finalized && event.code !== 1000) {
                reject(new Error(`WebSocket closed unexpectedly (${event.code}).`));
            }
        });
    });
}

async function loadHistory() {
    const config = readConfig();
    setBusy(true);
    setStatus("Loading session history...", "info");

    try {
        const url = `/api/v1/chat/history/${encodeURIComponent(config.sessionId)}?page=1&page_size=50`;
        const response = await fetch(url, {
            headers: {
                "Authorization": `Bearer ${config.userId}`
            }
        });

        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "History request failed."));
        }

        elements.messages.innerHTML = "";
        for (const item of body.messages) {
            appendMessage({
                role: item.role,
                content: item.content,
                createdAt: item.created_at
            });
        }

        if (body.messages.length === 0) {
            setEmptyState();
        }

        elements.metaHistoryCount.textContent = String(body.messages.length);
        setStatus(`Loaded ${body.messages.length} message(s) from history.`, "success");
    } catch (error) {
        appendMessage({ role: "error", content: formatError(error), createdAt: new Date().toISOString() });
        setStatus(formatError(error), "danger");
    } finally {
        setBusy(false);
    }
}

function appendUserMessage(content) {
    const hasOnlyPlaceholder = elements.messages.children.length === 1
        && elements.messages.firstElementChild?.classList.contains("system");
    if (hasOnlyPlaceholder) {
        elements.messages.innerHTML = "";
    }

    appendMessage({ role: "user", content, createdAt: new Date().toISOString() });
}

function appendMessage({ role, content, createdAt }) {
    const fragment = elements.template.content.cloneNode(true);
    const card = fragment.querySelector(".message-card");
    const roleElement = fragment.querySelector(".message-role");
    const timeElement = fragment.querySelector(".message-time");
    const contentElement = fragment.querySelector(".message-content");

    card.classList.add(role);
    roleElement.textContent = toTitleCase(role);
    timeElement.textContent = new Date(createdAt).toLocaleString();
    contentElement.textContent = content;

    elements.messages.appendChild(fragment);
    scrollMessagesToBottom();
    return elements.messages.lastElementChild;
}

function scrollMessagesToBottom() {
    elements.messages.scrollTop = elements.messages.scrollHeight;
}

function setBusy(isBusy) {
    elements.sendButton.disabled = isBusy;
    elements.loadHistoryButton.disabled = isBusy;
    elements.streamMode.disabled = isBusy;
}

function setStatus(message, tone) {
    elements.statusBanner.className = `alert alert-${tone} mb-0`;
    elements.statusBanner.textContent = message;
}

function readConfig() {
    return {
        userId: elements.userId.value.trim(),
        sessionId: elements.sessionId.value.trim(),
        personaId: elements.personaId.value.trim(),
        streaming: elements.streamMode.checked
    };
}

async function readJson(response) {
    const contentType = response.headers.get("content-type") || "";
    if (!contentType.includes("application/json")) {
        return null;
    }

    return await response.json();
}

function extractApiError(body, fallback) {
    if (!body) {
        return fallback;
    }

    if (body.detail) {
        return typeof body.detail === "string" ? body.detail : JSON.stringify(body.detail);
    }

    if (body.errors) {
        return Object.entries(body.errors)
            .map(([field, messages]) => `${field}: ${messages.join(", ")}`)
            .join(" | ");
    }

    return fallback;
}

function formatWsError(detail) {
    if (Array.isArray(detail)) {
        return detail.map(item => typeof item === "string" ? item : `${item.field}: ${item.message}`).join(" | ");
    }

    return "WebSocket request failed.";
}

function formatError(error) {
    return error instanceof Error ? error.message : String(error);
}

function toTitleCase(value) {
    return value.charAt(0).toUpperCase() + value.slice(1);
}
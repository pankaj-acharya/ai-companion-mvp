const elements = {
    chatForm: document.getElementById("chatForm"),
    clearMessagesButton: document.getElementById("clearMessagesButton"),
    debugMode: document.getElementById("debugMode"),
    addMemoryButton: document.getElementById("addMemoryButton"),
    loadHistoryButton: document.getElementById("loadHistoryButton"),
    loadMemoryButton: document.getElementById("loadMemoryButton"),
    loadAuditButton: document.getElementById("loadAuditButton"),
    messageInput: document.getElementById("messageInput"),
    memoryAuditList: document.getElementById("memoryAuditList"),
    memoryConsent: document.getElementById("memoryConsent"),
    memoryContent: document.getElementById("memoryContent"),
    memoryList: document.getElementById("memoryList"),
    memoryScope: document.getElementById("memoryScope"),
    messages: document.getElementById("messages"),
    metaHistoryCount: document.getElementById("metaHistoryCount"),
    metaSession: document.getElementById("metaSession"),
    metaTokens: document.getElementById("metaTokens"),
    metaTransport: document.getElementById("metaTransport"),
    modeBadge: document.getElementById("modeBadge"),
    modelId: document.getElementById("modelId"),
    personaId: document.getElementById("personaId"),
    personaTip: document.getElementById("personaTip"),
    sendButton: document.getElementById("sendButton"),
    sessionId: document.getElementById("sessionId"),
    statusBanner: document.getElementById("statusBanner"),
    streamMode: document.getElementById("streamMode"),
    template: document.getElementById("messageTemplate"),
    userId: document.getElementById("userId")
};

const DEBUG_MODE_STORAGE_KEY = "ai-companion-debug-mode";

const PERSONA_TIPS = {
    "": "Tip: Use server default persona for consistent baseline behavior across model comparisons.",
    "Supportive Friend": "Supportive Friend: best for empathetic daily check-ins and encouragement.",
    "Productivity Coach": "Productivity Coach: best for action plans, prioritization, and accountability.",
    "Career Mentor": "Career Mentor: best for growth plans, role prep, and interview-style guidance.",
    "Creative Brainstormer": "Creative Brainstormer: best for idea generation, variations, and naming explorations.",
    "Calm Wellness Buddy": "Calm Wellness Buddy: best for low-stress reflective conversations and grounding prompts."
};

const TOOLING_TIP = "Tooling tip: turn on \"Enable debug console logs\" to inspect raw request and response payloads while tuning personas.";

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initialize);
} else {
    initialize();
}

function initialize() {
    if (!hasRequiredElements()) {
        return;
    }

    loadDebugMode();
    setStatus("Ready.", "secondary");
    setEmptyState();
    syncSessionMeta();
    updateModeLabel();
    updatePersonaTip();
    loadMemoryConsent();

    elements.streamMode.addEventListener("change", updateModeLabel);
    elements.personaId.addEventListener("change", updatePersonaTip);
    elements.debugMode.addEventListener("change", handleDebugModeChange);
    elements.memoryConsent.addEventListener("change", handleMemoryConsentChange);
    elements.addMemoryButton.addEventListener("click", addMemory);
    elements.loadMemoryButton.addEventListener("click", loadMemory);
    elements.loadAuditButton.addEventListener("click", loadMemoryAudit);
    elements.sessionId.addEventListener("input", syncSessionMeta);
    elements.messageInput.addEventListener("keydown", handleComposerKeydown);
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

function loadDebugMode() {
    const storedValue = window.localStorage.getItem(DEBUG_MODE_STORAGE_KEY);
    elements.debugMode.checked = storedValue === "true";
}

function handleDebugModeChange() {
    window.localStorage.setItem(DEBUG_MODE_STORAGE_KEY, String(elements.debugMode.checked));

    if (elements.debugMode.checked) {
        console.debug("[AI Companion] Debug console logging enabled.");
    }
}

function isDebugModeEnabled() {
    return elements.debugMode.checked;
}

async function loadMemoryConsent() {
    const config = readConfig();

    try {
        const response = await fetch("/api/v1/memory/consent", {
            headers: authHeaders(config.userId)
        });
        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "Failed to load memory consent."));
        }

        elements.memoryConsent.checked = body?.enabled === true;
    } catch (error) {
        setStatus(formatError(error), "danger");
    }
}

async function handleMemoryConsentChange() {
    const config = readConfig();

    try {
        const response = await fetch("/api/v1/memory/consent", {
            method: "PUT",
            headers: {
                ...authHeaders(config.userId),
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ enabled: elements.memoryConsent.checked })
        });
        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "Failed to update memory consent."));
        }

        setStatus(`Memory consent ${body.enabled ? "enabled" : "disabled"}.`, "success");
    } catch (error) {
        elements.memoryConsent.checked = !elements.memoryConsent.checked;
        setStatus(formatError(error), "danger");
    }
}

function handleComposerKeydown(event) {
    if (event.key !== "Enter" || event.shiftKey) {
        return;
    }

    event.preventDefault();
    if (typeof elements.chatForm.requestSubmit === "function") {
        elements.chatForm.requestSubmit();
        return;
    }

    elements.chatForm.dispatchEvent(new Event("submit", { cancelable: true, bubbles: true }));
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
            ...authHeaders(config.userId)
        },
        body: JSON.stringify({
            session_id: config.sessionId,
            message,
            persona_id: config.personaId || undefined,
            model_id: config.modelId || undefined
        })
    });

    const body = await readJson(response);
    logJsonDebug("REST /api/v1/chat raw response", body);
    if (!response.ok) {
        throw new Error(extractApiError(body, "Chat request failed."));
    }

    const assistantText = typeof body?.response === "string" ? body.response : "";
    if (!assistantText.trim()) {
        const warningMessage = "REST call succeeded (200) but assistant text was empty. Check console debug logs for raw JSON.";
        appendMessage({ role: "system", content: warningMessage, createdAt: new Date().toISOString() });
        elements.metaTokens.textContent = String(body?.tokens_used ?? "-");
        setStatus(warningMessage, "warning");
        return;
    }

    appendMessage({ role: "assistant", content: assistantText, createdAt: body.created_at });
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
                persona_id: config.personaId || undefined,
                model_id: config.modelId || undefined
            }));
        });

        socket.addEventListener("message", event => {
            const payload = JSON.parse(event.data);
            logJsonDebug("WebSocket /ws/chat raw message", payload);
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
            headers: authHeaders(config.userId)
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

async function addMemory() {
    const config = readConfig();
    const content = elements.memoryContent.value.trim();
    if (!content) {
        setStatus("Memory content is required.", "warning");
        return;
    }

    setBusy(true);
    try {
        const response = await fetch("/api/v1/memory", {
            method: "POST",
            headers: {
                ...authHeaders(config.userId),
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                scope: elements.memoryScope.value,
                content,
                is_approved: true
            })
        });
        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "Failed to save memory."));
        }

        elements.memoryContent.value = "";
        setStatus("Memory saved.", "success");
        await loadMemory();
    } catch (error) {
        setStatus(formatError(error), "danger");
    } finally {
        setBusy(false);
    }
}

async function loadMemory() {
    const config = readConfig();
    try {
        const response = await fetch("/api/v1/memory?approved_only=true", {
            headers: authHeaders(config.userId)
        });
        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "Failed to load memory."));
        }

        renderMemoryList(Array.isArray(body?.items) ? body.items : []);
        setStatus("Memory loaded.", "success");
    } catch (error) {
        setStatus(formatError(error), "danger");
    }
}

async function deleteMemory(id) {
    const config = readConfig();

    try {
        const response = await fetch(`/api/v1/memory/${encodeURIComponent(id)}`, {
            method: "DELETE",
            headers: authHeaders(config.userId)
        });
        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "Failed to delete memory."));
        }

        await loadMemory();
        setStatus("Memory deleted.", "success");
    } catch (error) {
        setStatus(formatError(error), "danger");
    }
}

async function loadMemoryAudit() {
    const config = readConfig();
    try {
        const response = await fetch("/api/v1/memory/audit?page=1&page_size=20", {
            headers: authHeaders(config.userId)
        });
        const body = await readJson(response);
        if (!response.ok) {
            throw new Error(extractApiError(body, "Failed to load memory audit."));
        }

        renderAuditList(Array.isArray(body?.events) ? body.events : []);
        setStatus("Memory audit loaded.", "success");
    } catch (error) {
        setStatus(formatError(error), "danger");
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
    elements.addMemoryButton.disabled = isBusy;
    elements.loadMemoryButton.disabled = isBusy;
    elements.loadAuditButton.disabled = isBusy;
    elements.streamMode.disabled = isBusy;
}

function setStatus(message, tone) {
    if (!elements.statusBanner) {
        return;
    }

    elements.statusBanner.className = `alert alert-${tone} mb-0`;
    elements.statusBanner.textContent = message;
}

function readConfig() {
    return {
        userId: elements.userId.value.trim(),
        sessionId: elements.sessionId.value.trim(),
        personaId: elements.personaId.value.trim(),
        modelId: elements.modelId.value.trim(),
        streaming: elements.streamMode.checked
    };
}

function authHeaders(userId) {
    return {
        "Authorization": `Bearer ${userId}`
    };
}

async function readJson(response) {
    const contentType = response.headers.get("content-type") || "";
    if (!contentType.includes("application/json")) {
        debugLog("[AI Companion] Non-JSON response body skipped.");
        return null;
    }

    return await response.json();
}

function logJsonDebug(label, payload) {
    if (!isDebugModeEnabled()) {
        return;
    }

    if (payload == null) {
        console.debug(`[AI Companion] ${label}: null`);
        return;
    }

    try {
        console.debug(`[AI Companion] ${label}:\n${JSON.stringify(payload, null, 2)}`);
    } catch {
        console.debug(`[AI Companion] ${label}:`, payload);
    }
}

function debugLog(message) {
    if (!isDebugModeEnabled()) {
        return;
    }

    console.debug(message);
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

function renderMemoryList(items) {
    elements.memoryList.innerHTML = "";
    if (items.length === 0) {
        const empty = document.createElement("li");
        empty.className = "list-group-item small text-secondary";
        empty.textContent = "No approved memory saved yet.";
        elements.memoryList.appendChild(empty);
        return;
    }

    for (const item of items) {
        const listItem = document.createElement("li");
        listItem.className = "list-group-item d-flex justify-content-between align-items-start gap-2";
        listItem.innerHTML = `<div><div class="fw-semibold">${item.scope}</div><div class="small">${item.content}</div></div>`;

        const deleteButton = document.createElement("button");
        deleteButton.type = "button";
        deleteButton.className = "btn btn-sm btn-outline-danger";
        deleteButton.textContent = "Delete";
        deleteButton.addEventListener("click", () => deleteMemory(item.id));
        listItem.appendChild(deleteButton);

        elements.memoryList.appendChild(listItem);
    }
}

function renderAuditList(events) {
    elements.memoryAuditList.innerHTML = "";
    if (events.length === 0) {
        const empty = document.createElement("li");
        empty.className = "list-group-item small text-secondary";
        empty.textContent = "No audit events yet.";
        elements.memoryAuditList.appendChild(empty);
        return;
    }

    for (const event of events) {
        const listItem = document.createElement("li");
        listItem.className = "list-group-item";
        const createdAt = new Date(event.created_at).toLocaleString();
        const details = event.details ? ` (${event.details})` : "";
        listItem.textContent = `${createdAt} - ${event.action}${details}`;
        elements.memoryAuditList.appendChild(listItem);
    }
}

function toTitleCase(value) {
    return value.charAt(0).toUpperCase() + value.slice(1);
}

function updatePersonaTip() {
    if (!elements.personaTip || !elements.personaId) {
        return;
    }

    const selectedPersona = elements.personaId.value;
    const personaTip = PERSONA_TIPS[selectedPersona] ?? PERSONA_TIPS[""];
    elements.personaTip.textContent = `${personaTip} ${TOOLING_TIP}`;
}

function hasRequiredElements() {
    const requiredKeys = [
        "addMemoryButton",
        "chatForm",
        "clearMessagesButton",
        "debugMode",
        "loadHistoryButton",
        "loadMemoryButton",
        "loadAuditButton",
        "messageInput",
        "memoryAuditList",
        "memoryConsent",
        "memoryContent",
        "memoryList",
        "memoryScope",
        "messages",
        "metaHistoryCount",
        "metaSession",
        "metaTokens",
        "metaTransport",
        "modeBadge",
        "modelId",
        "personaId",
        "sendButton",
        "sessionId",
        "streamMode",
        "template",
        "userId"
    ];

    const missingKeys = requiredKeys.filter(key => !elements[key]);
    if (missingKeys.length === 0) {
        return true;
    }

    console.error("[AI Companion] Missing required UI elements:", missingKeys.join(", "));
    return false;
}
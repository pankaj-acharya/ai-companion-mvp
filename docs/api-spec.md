# API Specification (Epic #1)

## Authentication
All REST endpoints require:
- `Authorization: Bearer <user_id>`

WebSocket endpoint requires query parameter:
- `token=<user_id>`

## POST `/api/v1/chat`
Request:
```json
{
  "session_id": "session-123",
  "message": "Hello there",
  "persona_id": "Supportive Friend"
}
```

`persona_id` is optional; when omitted, default persona `Supportive Friend` is used.

Response:
```json
{
  "response": "AI response text",
  "session_id": "session-123",
  "tokens_used": 42,
  "created_at": "2026-07-02T10:00:00Z"
}
```

Validation failures return `422`.

## GET `/api/v1/chat/history/{session_id}?page=1&page_size=20`
Response:
```json
{
  "messages": [
    {
      "role": "user",
      "content": "Hello",
      "created_at": "2026-07-02T10:00:00Z"
    },
    {
      "role": "assistant",
      "content": "Hi there!",
      "created_at": "2026-07-02T10:00:01Z"
    }
  ],
  "total": 2,
  "page": 1,
  "page_size": 20
}
```

- Returns `404` if session is not found for the authenticated user.

## WS `/ws/chat/{session_id}?token=<user_id>`
Client send payload:
```json
{
  "message": "How are you?",
  "persona_id": "Supportive Friend"
}
```

Server stream events:
1. Token chunks:
```json
{ "type": "token", "content": "Hello " }
```
2. Completion event:
```json
{ "type": "done", "tokens_used": 7 }
```
3. Validation errors:
```json
{ "type": "error", "detail": [...] }
```


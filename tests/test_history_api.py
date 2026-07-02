from fastapi.testclient import TestClient


def _send_chat(client: TestClient, headers: dict[str, str], session_id: str, message: str) -> None:
    response = client.post(
        "/api/v1/chat",
        headers=headers,
        json={"session_id": session_id, "message": message},
    )
    assert response.status_code == 200


def test_history_returns_paginated_messages(client: TestClient, auth_header: dict[str, str]) -> None:
    _send_chat(client, auth_header, "history-s", "first")
    _send_chat(client, auth_header, "history-s", "second")

    response = client.get("/api/v1/chat/history/history-s?page=1&page_size=2", headers=auth_header)
    assert response.status_code == 200
    body = response.json()
    assert body["total"] == 4
    assert body["page"] == 1
    assert body["page_size"] == 2
    assert len(body["messages"]) == 2


def test_history_404_when_missing_session(client: TestClient, auth_header: dict[str, str]) -> None:
    response = client.get("/api/v1/chat/history/does-not-exist", headers=auth_header)
    assert response.status_code == 404


def test_history_is_user_scoped(client: TestClient, auth_header: dict[str, str]) -> None:
    _send_chat(client, auth_header, "private-s", "secret")
    response = client.get(
        "/api/v1/chat/history/private-s",
        headers={"Authorization": "Bearer another-user"},
    )
    assert response.status_code == 404


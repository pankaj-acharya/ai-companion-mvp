from fastapi.testclient import TestClient


def test_chat_endpoint_happy_path(client: TestClient, auth_header: dict[str, str], default_persona: str) -> None:
    response = client.post(
        "/api/v1/chat",
        headers=auth_header,
        json={"session_id": "s1", "message": "hello"},
    )
    assert response.status_code == 200
    body = response.json()
    assert body["session_id"] == "s1"
    assert body["tokens_used"] == 11
    assert body["response"] == f"reply:{default_persona}:hello"


def test_chat_endpoint_422_on_invalid_payload(client: TestClient, auth_header: dict[str, str]) -> None:
    response = client.post(
        "/api/v1/chat",
        headers=auth_header,
        json={"session_id": "", "message": ""},
    )
    assert response.status_code == 422


def test_chat_endpoint_requires_auth(client: TestClient) -> None:
    response = client.post(
        "/api/v1/chat",
        json={"session_id": "s1", "message": "hello"},
    )
    assert response.status_code == 401


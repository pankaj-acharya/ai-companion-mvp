import pytest
from fastapi.testclient import TestClient


def test_websocket_streaming_happy_path(client: TestClient) -> None:
    with client.websocket_connect("/ws/chat/ws-session?token=user-123") as ws:
        ws.send_json({"message": "hello"})
        messages = [ws.receive_json(), ws.receive_json(), ws.receive_json(), ws.receive_json()]
        assert messages[-1]["type"] == "done"
        assert messages[-1]["tokens_used"] > 0
        assert all(m["type"] in {"token", "done"} for m in messages)

def test_websocket_rejects_missing_token(client: TestClient) -> None:
    with pytest.raises(Exception):
        with client.websocket_connect("/ws/chat/ws-session"):
            pass

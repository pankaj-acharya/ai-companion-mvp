import pytest
from pydantic import ValidationError

from app.config import Settings


def test_settings_reads_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("OPENAI_API_KEY", "k")
    monkeypatch.setenv("OPENAI_MODEL", "gpt-4o-mini")
    monkeypatch.setenv("OPENAI_TEMPERATURE", "0.5")
    monkeypatch.setenv("OPENAI_MAX_TOKENS", "700")
    settings = Settings()
    assert settings.openai_model == "gpt-4o-mini"
    assert settings.openai_temperature == 0.5
    assert settings.openai_max_tokens == 700


def test_settings_fails_when_api_key_missing(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)
    with pytest.raises(ValidationError):
        Settings()

from collections.abc import Generator

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import Session, sessionmaker
from sqlalchemy.pool import StaticPool

from app.database import Base, get_db
from app.llm import DEFAULT_PERSONA, LLMClient, LLMResult
from app.main import app, get_llm_client


class TestLLMClient(LLMClient):
    async def generate(self, *, message: str, persona: str) -> LLMResult:
        return LLMResult(text=f"reply:{persona}:{message}", tokens_used=11)

    async def stream_generate(self, *, message: str, persona: str):
        payload = f"reply:{persona}:{message}"
        for item in payload.split(":"):
            yield item


@pytest.fixture(autouse=True)
def env_setup(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("OPENAI_API_KEY", "test-key")
    monkeypatch.setenv("DATABASE_URL", "sqlite:///./test_chat.db")
    monkeypatch.setenv("OPENAI_MODEL", "gpt-4o")
    monkeypatch.setenv("OPENAI_TEMPERATURE", "0.2")
    monkeypatch.setenv("OPENAI_MAX_TOKENS", "300")


@pytest.fixture()
def client() -> Generator[TestClient, None, None]:
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        future=True,
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    TestingSessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, future=True)
    Base.metadata.create_all(bind=engine)

    def override_get_db() -> Generator[Session, None, None]:
        db = TestingSessionLocal()
        try:
            yield db
        finally:
            db.close()

    app.dependency_overrides[get_db] = override_get_db
    app.dependency_overrides[get_llm_client] = lambda: TestLLMClient()

    with TestClient(app) as test_client:
        yield test_client

    app.dependency_overrides.clear()
    engine.dispose()


@pytest.fixture()
def auth_header() -> dict[str, str]:
    return {"Authorization": "Bearer user-123"}


@pytest.fixture()
def default_persona() -> str:
    return DEFAULT_PERSONA

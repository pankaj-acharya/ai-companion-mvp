from abc import ABC, abstractmethod
from collections.abc import AsyncIterator
from dataclasses import dataclass

from openai import AsyncOpenAI

from app.config import get_settings


DEFAULT_PERSONA = "Supportive Friend"


@dataclass
class LLMResult:
    text: str
    tokens_used: int


class LLMClient(ABC):
    @abstractmethod
    async def generate(self, *, message: str, persona: str) -> LLMResult:
        raise NotImplementedError

    @abstractmethod
    async def stream_generate(self, *, message: str, persona: str) -> AsyncIterator[str]:
        raise NotImplementedError


class OpenAILLMClient(LLMClient):
    def __init__(self) -> None:
        settings = get_settings()
        self._model = settings.openai_model
        self._temperature = settings.openai_temperature
        self._max_tokens = settings.openai_max_tokens
        self._client = AsyncOpenAI(api_key=settings.openai_api_key.get_secret_value())

    async def generate(self, *, message: str, persona: str) -> LLMResult:
        completion = await self._client.chat.completions.create(
            model=self._model,
            temperature=self._temperature,
            max_tokens=self._max_tokens,
            messages=[
                {"role": "system", "content": f"You are the '{persona}' persona for an AI companion."},
                {"role": "user", "content": message},
            ],
        )
        text = completion.choices[0].message.content or ""
        tokens = completion.usage.total_tokens if completion.usage else 0
        return LLMResult(text=text, tokens_used=tokens)

    async def stream_generate(self, *, message: str, persona: str) -> AsyncIterator[str]:
        stream = await self._client.chat.completions.create(
            model=self._model,
            temperature=self._temperature,
            max_tokens=self._max_tokens,
            messages=[
                {"role": "system", "content": f"You are the '{persona}' persona for an AI companion."},
                {"role": "user", "content": message},
            ],
            stream=True,
        )
        async for event in stream:
            delta = event.choices[0].delta.content if event.choices else None
            if delta:
                yield delta


class EchoLLMClient(LLMClient):
    async def generate(self, *, message: str, persona: str) -> LLMResult:
        text = f"[{persona}] {message}"
        return LLMResult(text=text, tokens_used=len(message.split()))

    async def stream_generate(self, *, message: str, persona: str) -> AsyncIterator[str]:
        payload = f"[{persona}] {message}"
        for token in payload.split():
            yield token + " "


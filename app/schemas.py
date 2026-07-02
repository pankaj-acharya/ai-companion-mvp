from datetime import datetime

from pydantic import BaseModel, Field


class ChatRequest(BaseModel):
    session_id: str = Field(min_length=1, max_length=128)
    message: str = Field(min_length=1, max_length=8000)
    persona_id: str | None = Field(default=None, min_length=1, max_length=128)


class ChatResponse(BaseModel):
    response: str
    session_id: str
    tokens_used: int
    created_at: datetime


class HistoryMessage(BaseModel):
    role: str
    content: str
    created_at: datetime


class HistoryResponse(BaseModel):
    messages: list[HistoryMessage]
    total: int
    page: int
    page_size: int


class WsChatRequest(BaseModel):
    message: str = Field(min_length=1, max_length=8000)
    persona_id: str | None = Field(default=None, min_length=1, max_length=128)


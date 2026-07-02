from datetime import UTC, datetime

from fastapi import Depends, FastAPI, HTTPException, Query, WebSocket, WebSocketDisconnect, status
from pydantic import ValidationError
from sqlalchemy.orm import Session

from app.auth import get_user_id_from_auth_header, get_user_id_from_ws_token
from app.config import get_settings
from app.database import Base, SessionLocal, engine, get_db
from app.llm import DEFAULT_PERSONA, LLMClient, OpenAILLMClient
from app.repository import add_message, get_conversation_for_user, get_or_create_conversation, list_messages
from app.schemas import ChatRequest, ChatResponse, HistoryMessage, HistoryResponse, WsChatRequest

app = FastAPI(title="AI Companion MVP API", version="0.1.0")


def get_llm_client() -> LLMClient:
    return OpenAILLMClient()


@app.on_event("startup")
def startup() -> None:
    get_settings()
    Base.metadata.create_all(bind=engine)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/v1/chat", response_model=ChatResponse, status_code=status.HTTP_200_OK)
async def create_chat_response(
    payload: ChatRequest,
    db: Session = Depends(get_db),
    user_id: str = Depends(get_user_id_from_auth_header),
    llm: LLMClient = Depends(get_llm_client),
) -> ChatResponse:
    persona = payload.persona_id or DEFAULT_PERSONA
    conversation = get_or_create_conversation(db=db, session_id=payload.session_id, user_id=user_id)
    if conversation.user_id != user_id:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Session belongs to a different user.")

    add_message(db=db, session_id=payload.session_id, role="user", content=payload.message)
    result = await llm.generate(message=payload.message, persona=persona)
    add_message(db=db, session_id=payload.session_id, role="assistant", content=result.text)
    db.commit()

    return ChatResponse(
        response=result.text,
        session_id=payload.session_id,
        tokens_used=result.tokens_used,
        created_at=datetime.now(UTC),
    )


@app.get("/api/v1/chat/history/{session_id}", response_model=HistoryResponse)
def get_chat_history(
    session_id: str,
    page: int = Query(default=1, ge=1),
    page_size: int = Query(default=20, ge=1, le=200),
    db: Session = Depends(get_db),
    user_id: str = Depends(get_user_id_from_auth_header),
) -> HistoryResponse:
    conversation = get_conversation_for_user(db=db, session_id=session_id, user_id=user_id)
    if conversation is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Session not found.")

    messages, total = list_messages(db=db, session_id=session_id, page=page, page_size=page_size)
    return HistoryResponse(
        messages=[HistoryMessage(role=msg.role, content=msg.content, created_at=msg.created_at) for msg in messages],
        total=total,
        page=page,
        page_size=page_size,
    )


@app.websocket("/ws/chat/{session_id}")
async def ws_chat(session_id: str, websocket: WebSocket) -> None:
    token = websocket.query_params.get("token")
    try:
        user_id = get_user_id_from_ws_token(token)
    except HTTPException:
        await websocket.close(code=1008, reason="Unauthorized.")
        return

    await websocket.accept()
    db = SessionLocal()
    llm_factory = app.dependency_overrides.get(get_llm_client, get_llm_client)
    llm = llm_factory()
    try:
        conversation = get_or_create_conversation(db=db, session_id=session_id, user_id=user_id)
        if conversation.user_id != user_id:
            await websocket.close(code=1008, reason="Forbidden.")
            return

        while True:
            raw_payload = await websocket.receive_json()
            try:
                payload = WsChatRequest.model_validate(raw_payload)
            except ValidationError as exc:
                await websocket.send_json({"type": "error", "detail": exc.errors()})
                continue

            persona = payload.persona_id or DEFAULT_PERSONA
            add_message(db=db, session_id=session_id, role="user", content=payload.message)
            chunks: list[str] = []
            token_count = 0
            async for chunk in llm.stream_generate(message=payload.message, persona=persona):
                chunks.append(chunk)
                token_count += 1
                await websocket.send_json({"type": "token", "content": chunk})

            add_message(db=db, session_id=session_id, role="assistant", content="".join(chunks).strip())
            db.commit()
            await websocket.send_json({"type": "done", "tokens_used": token_count})
    except WebSocketDisconnect:
        return
    finally:
        db.close()

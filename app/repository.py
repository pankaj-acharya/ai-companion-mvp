from sqlalchemy import Select, func, select
from sqlalchemy.orm import Session

from app.models import Conversation, Message


def get_or_create_conversation(*, db: Session, session_id: str, user_id: str) -> Conversation:
    conversation = db.get(Conversation, session_id)
    if conversation is None:
        conversation = Conversation(id=session_id, user_id=user_id)
        db.add(conversation)
        db.flush()
    return conversation


def get_conversation_for_user(*, db: Session, session_id: str, user_id: str) -> Conversation | None:
    statement: Select[tuple[Conversation]] = select(Conversation).where(
        Conversation.id == session_id, Conversation.user_id == user_id
    )
    return db.execute(statement).scalar_one_or_none()


def add_message(*, db: Session, session_id: str, role: str, content: str) -> Message:
    message = Message(conversation_id=session_id, role=role, content=content)
    db.add(message)
    db.flush()
    return message


def list_messages(*, db: Session, session_id: str, page: int, page_size: int) -> tuple[list[Message], int]:
    total_stmt = select(func.count(Message.id)).where(Message.conversation_id == session_id)
    total = db.execute(total_stmt).scalar_one()

    statement: Select[tuple[Message]] = (
        select(Message)
        .where(Message.conversation_id == session_id)
        .order_by(Message.created_at.asc(), Message.id.asc())
        .offset((page - 1) * page_size)
        .limit(page_size)
    )
    messages = list(db.execute(statement).scalars())
    return messages, int(total)


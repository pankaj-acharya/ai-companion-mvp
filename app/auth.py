from fastapi import Header, HTTPException, Query, status


def get_user_id_from_auth_header(authorization: str | None = Header(default=None)) -> str:
    if authorization is None:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Missing Authorization header.")
    prefix = "Bearer "
    if not authorization.startswith(prefix):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Authorization must use Bearer token.")
    user_id = authorization[len(prefix) :].strip()
    if not user_id:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Bearer token must not be empty.")
    return user_id


def get_user_id_from_ws_token(token: str | None = Query(default=None)) -> str:
    if token is None or not token.strip():
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Missing websocket token.")
    return token.strip()


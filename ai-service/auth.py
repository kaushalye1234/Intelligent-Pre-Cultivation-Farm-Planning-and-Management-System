import secrets
from typing import Annotated

from fastapi import Depends, Header, HTTPException, status

from config import Settings, get_settings


def require_service_token(
    authorization: Annotated[str | None, Header()] = None,
    x_agriassist_ai_token: Annotated[str | None, Header()] = None,
    settings: Settings = Depends(get_settings),
) -> None:
    expected = settings.ai_service_token
    if not expected:
        raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail="AI service token is not configured.")

    provided = x_agriassist_ai_token
    if authorization and authorization.lower().startswith("bearer "):
        provided = authorization[7:].strip()

    if not provided or not secrets.compare_digest(provided, expected):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="AI service token is invalid.")

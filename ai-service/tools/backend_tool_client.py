from typing import Any
from uuid import UUID

import httpx

from config import Settings
from schemas.crop_planning import ToolEnvelope


class ToolClientError(RuntimeError):
    pass


class BackendToolClient:
    def __init__(self, settings: Settings) -> None:
        self._base_url = settings.backend_tool_base_url.rstrip("/")
        self._token = settings.backend_tool_token
        self._timeout_seconds = settings.tool_timeout_seconds

    async def get(self, path: str, *, workflow_id: UUID, params: dict[str, Any] | None = None) -> ToolEnvelope:
        if not self._token:
            raise ToolClientError("BACKEND_TOOL_TOKEN is not configured.")

        query = dict(params or {})
        query["workflowId"] = str(workflow_id)
        headers = {"Authorization": f"Bearer {self._token}"}
        async with httpx.AsyncClient(timeout=self._timeout_seconds) as client:
            response = await client.get(f"{self._base_url}{path}", headers=headers, params=query)

        try:
            payload = response.json()
        except ValueError as exc:
            raise ToolClientError("Backend tool returned non-JSON data.") from exc

        envelope = ToolEnvelope.model_validate(payload)
        if response.status_code >= 400 or envelope.status.lower() == "failed":
            raise ToolClientError(envelope.safe_error or "Backend tool call failed safely.")
        return envelope

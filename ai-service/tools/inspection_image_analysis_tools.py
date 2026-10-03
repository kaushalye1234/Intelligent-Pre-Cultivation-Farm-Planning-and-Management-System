import base64
import binascii
import hashlib
from dataclasses import dataclass
from uuid import UUID

from tools.backend_tool_client import BackendToolClient, ToolClientError


@dataclass(frozen=True)
class VerifiedInspectionImage:
    analysis_id: UUID
    inspection_image_id: UUID
    content_type: str
    content_sha256: str
    image_bytes: bytes


class InspectionImageAnalysisTools:
    MAX_ORIGINAL_BYTES = 5 * 1024 * 1024

    def __init__(self, client: BackendToolClient) -> None:
        self._client = client

    async def get_verified_image(self, analysis_id: UUID) -> VerifiedInspectionImage:
        payload = await self._client.get_json(
            f"/api/internal/agent-tools/inspection-image-analysis/{analysis_id}/image-bytes"
        )
        try:
            returned_analysis_id = UUID(str(payload["analysisId"]))
            image_id = UUID(str(payload["inspectionImageId"]))
            content_type = str(payload["contentType"])
            expected_hash = str(payload["contentSha256"]).lower()
            encoded = str(payload["imageBase64"])
            image_bytes = base64.b64decode(encoded, validate=True)
        except (KeyError, ValueError, TypeError, binascii.Error) as exc:
            raise ToolClientError("Backend image retrieval returned invalid data.") from exc
        if returned_analysis_id != analysis_id:
            raise ToolClientError("Backend image retrieval did not match the requested analysis.")
        if content_type not in {"image/jpeg", "image/png", "image/webp"}:
            raise ToolClientError("Backend image retrieval returned an unsupported content type.")
        if not image_bytes or len(image_bytes) > self.MAX_ORIGINAL_BYTES:
            raise ToolClientError("Backend image retrieval returned an invalid image size.")
        actual_hash = hashlib.sha256(image_bytes).hexdigest()
        if len(expected_hash) != 64 or actual_hash != expected_hash:
            raise ToolClientError("Backend image integrity verification failed.")
        return VerifiedInspectionImage(
            returned_analysis_id,
            image_id,
            content_type,
            actual_hash,
            image_bytes,
        )

import asyncio
import base64
from typing import Any

from openai import AsyncOpenAI

from providers.base_llm_provider import (
    BaseLLMProvider,
    LLMProviderError,
    LLMResponse,
    ProviderConfigurationError,
    StructuredGenerationRequest,
    WebSearchResponse,
    WebSearchSource,
    classify_provider_exception,
)


class OpenAIProvider(BaseLLMProvider):
    provider_name = "openai"

    def __init__(
        self,
        api_key: str,
        model: str,
        timeout_seconds: float,
        web_search_timeout_seconds: float | None = None,
        web_search_max_retries: int | None = None,
    ) -> None:
        if not api_key or not model:
            raise ProviderConfigurationError("OpenAI API key and AI_MODEL are required for OpenAI.")
        self._client = AsyncOpenAI(api_key=api_key, timeout=timeout_seconds)
        self._model = model
        self._timeout_seconds = timeout_seconds
        self._web_search_timeout_seconds = web_search_timeout_seconds or timeout_seconds
        if web_search_timeout_seconds is None and web_search_max_retries is None:
            self._web_search_client = self._client
        else:
            options: dict[str, Any] = {"timeout": self._web_search_timeout_seconds}
            if web_search_max_retries is not None:
                options["max_retries"] = web_search_max_retries
            self._web_search_client = self._client.with_options(**options)

    async def generate_json(
        self,
        prompt: str,
        response_schema: dict[str, Any] | None = None,
    ) -> LLMResponse:
        async def _generate() -> str:
            response_format: dict[str, Any] = {"type": "json_object"}
            if response_schema is not None:
                response_format = {
                    "type": "json_schema",
                    "json_schema": {
                        "name": "crop_field_analysis_output",
                        "strict": False,
                        "schema": response_schema,
                    },
                }
            response = await self._client.chat.completions.create(
                model=self._model,
                messages=[{"role": "user", "content": prompt}],
                response_format=response_format,
            )
            return response.choices[0].message.content or ""

        text = await asyncio.wait_for(_generate(), timeout=self._timeout_seconds)
        return LLMResponse(text=text)

    async def generate_crop_finding_json(
        self,
        prompt: str,
        response_schema: dict[str, Any] | None,
        timeout_seconds: float,
    ) -> LLMResponse:
        response_format: dict[str, Any] = {"type": "json_object"}
        if response_schema is not None:
            response_format = {
                "type": "json_schema",
                "json_schema": {
                    "name": "crop_finding_output",
                    "strict": False,
                    "schema": response_schema,
                },
            }

        async def _generate() -> str:
            client = self._client.with_options(timeout=timeout_seconds, max_retries=0)
            response = await client.chat.completions.create(
                model=self._model,
                messages=[{"role": "user", "content": prompt}],
                response_format=response_format,
            )
            return response.choices[0].message.content or ""

        text = await asyncio.wait_for(_generate(), timeout=timeout_seconds)
        return LLMResponse(text=text)

    async def generate_structured_json(self, request: StructuredGenerationRequest) -> LLMResponse:
        if bool(request.image_bytes) != bool(request.image_mime_type):
            raise LLMProviderError(
                "Controlled image bytes and MIME type must be supplied together.",
                category="invalid_structured_request",
                operation="structured_generation",
            )
        if request.contract_version < 1 or not request.schema_name:
            raise LLMProviderError(
                "Structured generation contract metadata is invalid.",
                category="invalid_structured_request",
                operation="structured_generation",
            )
        if not hasattr(self._client, "responses"):
            raise ProviderConfigurationError("The installed OpenAI SDK does not support the Responses API.")

        content: list[dict[str, Any]] = [{"type": "input_text", "text": request.user_input}]
        if request.image_bytes is not None and request.image_mime_type is not None:
            encoded = base64.b64encode(request.image_bytes).decode("ascii")
            content.append({
                "type": "input_image",
                "image_url": f"data:{request.image_mime_type};base64,{encoded}",
            })

        async def _generate() -> Any:
            client = self._client.with_options(timeout=request.timeout_seconds, max_retries=0)
            return await client.responses.create(
                model=self._model,
                instructions=request.system_input,
                input=[{"role": "user", "content": content}],
                text={
                    "format": {
                        "type": "json_schema",
                        "name": request.schema_name,
                        "strict": True,
                        "schema": request.response_schema,
                    }
                },
                metadata={"contract_version": str(request.contract_version)},
                store=False,
            )

        try:
            response = await asyncio.wait_for(_generate(), timeout=request.timeout_seconds)
        except Exception as exc:
            raise classify_provider_exception(
                exc,
                operation="structured_generation",
                timeout_message="OpenAI structured generation timed out.",
            ) from exc

        status = getattr(response, "status", "completed")
        payload = response.model_dump(mode="json") if hasattr(response, "model_dump") else response
        if self._contains_refusal(payload):
            raise LLMProviderError(
                "OpenAI refused the structured generation request.",
                category="refusal",
                operation="structured_generation",
            )
        if status != "completed":
            raise LLMProviderError(
                "OpenAI returned an incomplete structured response.",
                category="incomplete",
                operation="structured_generation",
            )

        text = getattr(response, "output_text", None) or self._find_output_text(payload)
        if not isinstance(text, str) or not text.strip():
            raise LLMProviderError(
                "OpenAI returned malformed structured output.",
                category="malformed_structured_output",
                operation="structured_generation",
            )
        return LLMResponse(text=text)

    @staticmethod
    def _contains_refusal(value: Any) -> bool:
        if isinstance(value, dict):
            if value.get("type") == "refusal" or value.get("refusal"):
                return True
            return any(OpenAIProvider._contains_refusal(child) for child in value.values())
        if isinstance(value, (list, tuple)):
            return any(OpenAIProvider._contains_refusal(child) for child in value)
        return False

    @staticmethod
    def _find_output_text(value: Any) -> str | None:
        if isinstance(value, dict):
            if value.get("type") == "output_text" and isinstance(value.get("text"), str):
                return value["text"]
            for child in value.values():
                found = OpenAIProvider._find_output_text(child)
                if found:
                    return found
        elif isinstance(value, (list, tuple)):
            for child in value:
                found = OpenAIProvider._find_output_text(child)
                if found:
                    return found
        return None

    async def search_web(
        self,
        prompt: str,
        allowed_domains: list[str],
        max_results: int,
    ) -> WebSearchResponse:
        if not allowed_domains:
            raise ProviderConfigurationError("Controlled web search requires at least one approved domain.")
        if not hasattr(self._web_search_client, "responses"):
            raise ProviderConfigurationError(
                "The installed OpenAI SDK does not support the Responses API required for web search."
            )

        async def _search() -> Any:
            return await self._web_search_client.responses.create(
                model=self._model,
                input=prompt,
                tools=[{
                    "type": "web_search",
                    "filters": {"allowed_domains": allowed_domains},
                }],
                tool_choice="required",
                include=["web_search_call.action.sources"],
                store=False,
            )

        try:
            response = await asyncio.wait_for(_search(), timeout=self._web_search_timeout_seconds)
        except Exception as exc:
            raise classify_provider_exception(
                exc,
                operation="web_search",
                timeout_message="OpenAI web search exceeded the configured CropFinding timeout.",
            ) from exc

        payload = response.model_dump(mode="json") if hasattr(response, "model_dump") else response
        sources: list[WebSearchSource] = []
        seen: set[str] = set()

        def visit(value: Any) -> None:
            if len(sources) >= max_results:
                return
            if isinstance(value, dict):
                url = value.get("url")
                if isinstance(url, str) and url.startswith(("http://", "https://")) and url not in seen:
                    seen.add(url)
                    sources.append(WebSearchSource(url=url, title=str(value.get("title") or "")))
                for child in value.values():
                    visit(child)
            elif isinstance(value, (list, tuple)):
                for child in value:
                    visit(child)

        visit(payload)
        return WebSearchResponse(sources=sources[:max_results])

import asyncio
import base64
import json
from typing import Any

from openai import AsyncOpenAI

from providers.base_llm_provider import (
    BaseLLMProvider,
    LLMProviderError,
    LLMFunctionCall,
    LLMResponse,
    LLMToolCallResult,
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

    async def generate_tool_call(self, prompt: str, tool_schema: dict[str, Any]) -> LLMToolCallResult:
        name = tool_schema.get("name")
        if not isinstance(name, str) or not name or not isinstance(tool_schema.get("parameters"), dict):
            raise LLMProviderError(
                "The controlled tool schema is invalid.",
                category="invalid_tool_schema",
                operation="tool_call",
            )

        async def _generate() -> Any:
            client = self._client.with_options(timeout=self._timeout_seconds, max_retries=0)
            return await client.chat.completions.create(
                model=self._model,
                messages=[{"role": "user", "content": prompt}],
                tools=[{"type": "function", "function": tool_schema}],
                tool_choice="auto",
                parallel_tool_calls=False,
            )

        try:
            response = await asyncio.wait_for(_generate(), timeout=self._timeout_seconds)
        except Exception as exc:
            raise classify_provider_exception(
                exc,
                operation="tool_call",
                timeout_message="OpenAI controlled tool call timed out.",
            ) from exc

        choices = getattr(response, "choices", None)
        if not isinstance(choices, (list, tuple)) or len(choices) != 1:
            raise LLMProviderError(
                "OpenAI returned an invalid controlled tool response.",
                category="malformed_tool_response",
                operation="tool_call",
            )
        message = getattr(choices[0], "message", None)
        if message is None or getattr(message, "refusal", None):
            raise LLMProviderError("OpenAI refused the controlled tool request.", category="refusal", operation="tool_call")
        calls = getattr(message, "tool_calls", None) or []
        if not calls:
            return LLMToolCallResult(function_call=None, terminal_text=getattr(message, "content", None))
        if len(calls) != 1:
            raise LLMProviderError(
                "OpenAI returned more than one tool call.",
                category="multiple_tool_calls",
                operation="tool_call",
            )
        call = calls[0]
        function = getattr(call, "function", None)
        call_id = getattr(call, "id", None)
        function_name = getattr(function, "name", None)
        raw_arguments = getattr(function, "arguments", None)
        if not isinstance(call_id, str) or not call_id or function_name != name or not isinstance(raw_arguments, str):
            raise LLMProviderError(
                "OpenAI returned an unexpected controlled tool call.",
                category="invalid_tool_call",
                operation="tool_call",
            )
        try:
            arguments = json.loads(raw_arguments)
        except (TypeError, json.JSONDecodeError) as exc:
            raise LLMProviderError(
                "OpenAI returned malformed controlled tool arguments.",
                category="malformed_tool_arguments",
                operation="tool_call",
            ) from exc
        if not isinstance(arguments, dict):
            raise LLMProviderError(
                "OpenAI returned malformed controlled tool arguments.",
                category="malformed_tool_arguments",
                operation="tool_call",
            )
        return LLMToolCallResult(function_call=LLMFunctionCall(call_id, function_name, arguments))

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

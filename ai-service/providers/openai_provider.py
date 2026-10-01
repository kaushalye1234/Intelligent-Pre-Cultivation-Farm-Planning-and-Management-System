import asyncio
from typing import Any

from openai import AsyncOpenAI

from providers.base_llm_provider import (
    BaseLLMProvider,
    LLMProviderError,
    LLMResponse,
    ProviderConfigurationError,
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

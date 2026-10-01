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
)


class OpenAIProvider(BaseLLMProvider):
    provider_name = "openai"

    def __init__(self, api_key: str, model: str, timeout_seconds: float) -> None:
        if not api_key or not model:
            raise ProviderConfigurationError("OpenAI API key and AI_MODEL are required for OpenAI.")
        self._client = AsyncOpenAI(api_key=api_key, timeout=timeout_seconds)
        self._model = model
        self._timeout_seconds = timeout_seconds

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
        if not hasattr(self._client, "responses"):
            raise ProviderConfigurationError(
                "The installed OpenAI SDK does not support the Responses API required for web search."
            )

        async def _search() -> Any:
            return await self._client.responses.create(
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
            response = await asyncio.wait_for(_search(), timeout=self._timeout_seconds)
        except asyncio.TimeoutError:
            raise
        except Exception as exc:
            raise LLMProviderError(f"OpenAI web search failed: {exc}") from exc

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

import asyncio

from openai import AsyncOpenAI

from providers.base_llm_provider import BaseLLMProvider, LLMResponse, ProviderConfigurationError


class OpenAIProvider(BaseLLMProvider):
    provider_name = "openai"

    def __init__(self, api_key: str, model: str, timeout_seconds: float) -> None:
        if not api_key or not model:
            raise ProviderConfigurationError("OpenAI API key and AI_MODEL are required for OpenAI.")
        self._client = AsyncOpenAI(api_key=api_key, timeout=timeout_seconds)
        self._model = model
        self._timeout_seconds = timeout_seconds

    async def generate_json(self, prompt: str) -> LLMResponse:
        async def _generate() -> str:
            response = await self._client.chat.completions.create(
                model=self._model,
                messages=[{"role": "user", "content": prompt}],
                response_format={"type": "json_object"},
            )
            return response.choices[0].message.content or ""

        text = await asyncio.wait_for(_generate(), timeout=self._timeout_seconds)
        return LLMResponse(text=text)

import asyncio

from providers.base_llm_provider import BaseLLMProvider, LLMResponse, ProviderConfigurationError


class GeminiProvider(BaseLLMProvider):
    provider_name = "gemini"

    def __init__(self, api_key: str, model: str, timeout_seconds: float) -> None:
        if not api_key or not model:
            raise ProviderConfigurationError("Gemini API key and AI_MODEL are required for Gemini.")
        self._api_key = api_key
        self._model = model
        self._timeout_seconds = timeout_seconds

    async def generate_json(self, prompt: str) -> LLMResponse:
        import google.generativeai as genai

        def _generate() -> str:
            genai.configure(api_key=self._api_key)
            model = genai.GenerativeModel(self._model)
            response = model.generate_content(
                prompt,
                generation_config={"response_mime_type": "application/json"},
            )
            return response.text or ""

        text = await asyncio.wait_for(asyncio.to_thread(_generate), timeout=self._timeout_seconds)
        return LLMResponse(text=text)

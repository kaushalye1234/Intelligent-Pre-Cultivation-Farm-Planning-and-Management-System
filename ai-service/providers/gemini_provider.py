import asyncio
from typing import Any

from providers.base_llm_provider import BaseLLMProvider, LLMResponse, ProviderConfigurationError


def _to_gemini_response_schema(schema: dict[str, Any]) -> dict[str, Any]:
    definitions = schema.get("$defs", {})
    supported_keys = {
        "description",
        "enum",
        "format",
        "items",
        "maxItems",
        "minItems",
        "nullable",
        "properties",
        "required",
        "type",
    }
    field_names = {"maxItems": "max_items", "minItems": "min_items"}

    def convert(node: Any) -> Any:
        if not isinstance(node, dict):
            return node

        reference = node.get("$ref")
        if reference is not None:
            prefix = "#/$defs/"
            if not reference.startswith(prefix) or reference[len(prefix):] not in definitions:
                raise ProviderConfigurationError("Gemini response schema contains an unsupported reference.")
            resolved = dict(definitions[reference[len(prefix):]])
            resolved.update({key: value for key, value in node.items() if key != "$ref"})
            return convert(resolved)

        alternatives = node.get("anyOf")
        if alternatives is not None:
            concrete = [item for item in alternatives if item.get("type") != "null"]
            nullable = len(concrete) != len(alternatives)
            if len(concrete) != 1:
                raise ProviderConfigurationError("Gemini response schema contains an unsupported union.")
            converted = convert(concrete[0])
            if nullable:
                converted["nullable"] = True
            return converted

        converted: dict[str, Any] = {}
        for key, value in node.items():
            if key not in supported_keys:
                continue
            output_key = field_names.get(key, key)
            if key == "properties":
                converted[output_key] = {name: convert(child) for name, child in value.items()}
            elif key == "items":
                converted[output_key] = convert(value)
            else:
                converted[output_key] = value
        return converted

    return convert(schema)


class GeminiProvider(BaseLLMProvider):
    provider_name = "gemini"

    def __init__(self, api_key: str, model: str, timeout_seconds: float) -> None:
        if not api_key or not model:
            raise ProviderConfigurationError("Gemini API key and AI_MODEL are required for Gemini.")
        self._api_key = api_key
        self._model = model
        self._timeout_seconds = timeout_seconds

    async def generate_json(
        self,
        prompt: str,
        response_schema: dict[str, Any] | None = None,
    ) -> LLMResponse:
        import google.generativeai as genai

        def _generate() -> str:
            genai.configure(api_key=self._api_key)
            model = genai.GenerativeModel(self._model)
            generation_config: dict[str, Any] = {"response_mime_type": "application/json"}
            if response_schema is not None:
                generation_config["response_schema"] = _to_gemini_response_schema(response_schema)
            response = model.generate_content(
                prompt,
                generation_config=generation_config,
            )
            return response.text or ""

        text = await asyncio.wait_for(asyncio.to_thread(_generate), timeout=self._timeout_seconds)
        return LLMResponse(text=text)

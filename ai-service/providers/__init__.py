from config import Settings
from providers.base_llm_provider import BaseLLMProvider
from providers.openai_provider import OpenAIProvider


def create_provider(settings: Settings) -> BaseLLMProvider | None:
    if not settings.ai_model:
        return None

    if settings.ai_provider == "openai" and settings.openai_api_key:
        return OpenAIProvider(api_key=settings.openai_api_key, model=settings.ai_model, timeout_seconds=settings.provider_timeout_seconds)

    return None


def create_crop_finding_provider(settings: Settings) -> OpenAIProvider | None:
    if settings.ai_provider != "openai" or not settings.openai_api_key or not settings.ai_model:
        return None
    return OpenAIProvider(
        api_key=settings.openai_api_key,
        model=settings.ai_model,
        timeout_seconds=settings.provider_timeout_seconds,
        web_search_timeout_seconds=settings.crop_finding_web_search_timeout_seconds,
        web_search_max_retries=0,
    )

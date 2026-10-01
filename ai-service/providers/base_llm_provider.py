from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any


class LLMProviderError(RuntimeError):
    pass


class ProviderConfigurationError(LLMProviderError):
    pass


@dataclass(frozen=True)
class LLMResponse:
    text: str


@dataclass(frozen=True)
class WebSearchSource:
    url: str
    title: str = ""


@dataclass(frozen=True)
class WebSearchResponse:
    sources: list[WebSearchSource]


class BaseLLMProvider(ABC):
    provider_name: str

    @abstractmethod
    async def generate_json(
        self,
        prompt: str,
        response_schema: dict[str, Any] | None = None,
    ) -> LLMResponse:
        raise NotImplementedError

    async def search_web(
        self,
        prompt: str,
        allowed_domains: list[str],
        max_results: int,
    ) -> WebSearchResponse:
        raise ProviderConfigurationError(f"{self.provider_name} does not support controlled web search.")

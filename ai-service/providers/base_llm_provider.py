from abc import ABC, abstractmethod
from dataclasses import dataclass


class LLMProviderError(RuntimeError):
    pass


class ProviderConfigurationError(LLMProviderError):
    pass


@dataclass(frozen=True)
class LLMResponse:
    text: str


class BaseLLMProvider(ABC):
    provider_name: str

    @abstractmethod
    async def generate_json(self, prompt: str) -> LLMResponse:
        raise NotImplementedError

import asyncio
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any


class LLMProviderError(RuntimeError):
    def __init__(
        self,
        message: str,
        *,
        category: str = "unknown_provider_error",
        operation: str | None = None,
        status_code: int | None = None,
        error_code: str | None = None,
        provider_request_id: str | None = None,
        root_exception_class: str | None = None,
        retryable: bool = False,
    ) -> None:
        super().__init__(message)
        self.category = category
        self.operation = operation
        self.status_code = status_code
        self.error_code = error_code
        self.provider_request_id = provider_request_id
        self.root_exception_class = root_exception_class
        self.retryable = retryable
        self.request_id: str | None = None
        self.action: str | None = None
        self.stage: int | None = None
        self.attempt: int | None = None
        self.configured_timeout_seconds: float | None = None
        self.effective_timeout_seconds: float | None = None
        self.elapsed_operation_ms: int | None = None
        self.remaining_budget_seconds: float | None = None
        self.source_count: int | None = None
        self.chunk_count: int | None = None
        self.extracted_character_count: int | None = None

    def add_context(
        self,
        *,
        request_id: str,
        action: str,
        stage: int,
        attempt: int,
    ) -> "LLMProviderError":
        self.request_id = request_id
        self.action = action
        self.stage = stage
        self.attempt = attempt
        return self

    def add_diagnostics(
        self,
        *,
        configured_timeout_seconds: float,
        effective_timeout_seconds: float,
        elapsed_operation_ms: int,
        remaining_budget_seconds: float,
        source_count: int,
        chunk_count: int,
        extracted_character_count: int,
    ) -> "LLMProviderError":
        self.configured_timeout_seconds = configured_timeout_seconds
        self.effective_timeout_seconds = effective_timeout_seconds
        self.elapsed_operation_ms = elapsed_operation_ms
        self.remaining_budget_seconds = remaining_budget_seconds
        self.source_count = source_count
        self.chunk_count = chunk_count
        self.extracted_character_count = extracted_character_count
        return self


class ProviderConfigurationError(LLMProviderError):
    pass


def classify_provider_exception(
    exc: Exception,
    *,
    operation: str,
    timeout_message: str,
) -> LLMProviderError:
    if isinstance(exc, LLMProviderError):
        if exc.operation is None:
            exc.operation = operation
        return exc

    root = exc
    while isinstance(root.__cause__, Exception):
        root = root.__cause__

    exception_name = type(root).__name__
    normalized_name = exception_name.casefold()
    raw_status = getattr(root, "status_code", None)
    status_code = raw_status if isinstance(raw_status, int) else None
    raw_code = getattr(root, "code", None)
    error_code = str(raw_code)[:120] if raw_code is not None else None
    raw_request_id = getattr(root, "request_id", None)
    provider_request_id = str(raw_request_id)[:200] if raw_request_id is not None else None

    if isinstance(exc, TimeoutError) or isinstance(root, TimeoutError) or "timeout" in normalized_name:
        category = "timeout"
        message = timeout_message
        retryable = False
    elif status_code == 429 or "ratelimit" in normalized_name or "rate_limit" in normalized_name:
        category = "rate_limit"
        message = "OpenAI rate limit was reached."
        retryable = True
    elif status_code is not None and 500 <= status_code <= 599:
        category = "server_error"
        message = "OpenAI returned a server error."
        retryable = True
    elif status_code is not None and 400 <= status_code <= 499:
        category = "invalid_request"
        message = "OpenAI rejected the CropFinding request."
        retryable = False
    elif isinstance(root, ConnectionError) or "connection" in normalized_name or "connect" in normalized_name:
        category = "connection"
        message = "OpenAI connection failed."
        retryable = True
    else:
        category = "unknown_provider_error"
        message = "OpenAI provider request failed."
        retryable = False

    return LLMProviderError(
        message,
        category=category,
        operation=operation,
        status_code=status_code,
        error_code=error_code,
        provider_request_id=provider_request_id,
        root_exception_class=exception_name,
        retryable=retryable,
    )


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

    async def generate_crop_finding_json(
        self,
        prompt: str,
        response_schema: dict[str, Any] | None,
        timeout_seconds: float,
    ) -> LLMResponse:
        """Run CropFinding analysis within its request-specific remaining budget.

        Providers can override this to isolate SDK timeout/retry configuration.
        The default keeps test and alternate providers compatible without
        changing their existing ``generate_json`` behavior.
        """
        return await asyncio.wait_for(
            self.generate_json(prompt, response_schema=response_schema),
            timeout=timeout_seconds,
        )

    async def search_web(
        self,
        prompt: str,
        allowed_domains: list[str],
        max_results: int,
    ) -> WebSearchResponse:
        raise ProviderConfigurationError(f"{self.provider_name} does not support controlled web search.")

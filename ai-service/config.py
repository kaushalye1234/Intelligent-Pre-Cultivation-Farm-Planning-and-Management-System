from functools import lru_cache
from typing import Literal

from pydantic import Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    ai_service_token: str = Field(default="", alias="AI_SERVICE_TOKEN")
    ai_provider: Literal["openai"] = Field(default="openai", alias="AI_PROVIDER")
    ai_model: str = Field(default="", alias="AI_MODEL")
    openai_api_key: str = Field(default="", alias="OPENAI_API_KEY")
    backend_tool_base_url: str = Field(default="http://localhost:5000", alias="BACKEND_TOOL_BASE_URL")
    backend_tool_token: str = Field(default="", alias="BACKEND_TOOL_TOKEN")
    tool_timeout_seconds: float = 8
    provider_timeout_seconds: float = 30
    crop_finding_candidate_limit: int = Field(default=8, alias="CROP_FINDING_CANDIDATE_LIMIT", ge=1, le=20)
    crop_finding_stage1_retrieval_limit: int = Field(default=5, alias="CROP_FINDING_STAGE1_RETRIEVAL_LIMIT", ge=1, le=10)
    crop_finding_stage2_retrieval_limit: int = Field(default=3, alias="CROP_FINDING_STAGE2_RETRIEVAL_LIMIT", ge=1, le=6)
    crop_finding_connect_timeout_seconds: float = Field(default=4, alias="CROP_FINDING_CONNECT_TIMEOUT_SECONDS", gt=0, le=30)
    crop_finding_read_timeout_seconds: float = Field(default=12, alias="CROP_FINDING_READ_TIMEOUT_SECONDS", gt=0, le=60)
    crop_finding_document_timeout_seconds: float = Field(default=18, alias="CROP_FINDING_DOCUMENT_TIMEOUT_SECONDS", gt=0, le=90)
    crop_finding_web_search_timeout_seconds: float = Field(default=50, alias="CROP_FINDING_WEB_SEARCH_TIMEOUT_SECONDS", gt=0, le=90)
    crop_finding_overall_timeout_seconds: float = Field(default=105, alias="CROP_FINDING_OVERALL_TIMEOUT_SECONDS", gt=10, le=180)
    crop_finding_max_redirects: int = Field(default=3, alias="CROP_FINDING_MAX_REDIRECTS", ge=0, le=5)
    crop_finding_max_html_bytes: int = Field(default=2_000_000, alias="CROP_FINDING_MAX_HTML_BYTES", ge=100_000, le=5_000_000)
    crop_finding_max_pdf_bytes: int = Field(default=10_000_000, alias="CROP_FINDING_MAX_PDF_BYTES", ge=500_000, le=25_000_000)
    crop_finding_max_pdf_pages: int = Field(default=60, alias="CROP_FINDING_MAX_PDF_PAGES", ge=1, le=150)
    crop_finding_max_extracted_chars_per_source: int = Field(default=45_000, alias="CROP_FINDING_MAX_EXTRACTED_CHARS_PER_SOURCE", ge=5_000, le=100_000)
    crop_finding_max_total_extracted_chars: int = Field(default=100_000, alias="CROP_FINDING_MAX_TOTAL_EXTRACTED_CHARS", ge=10_000, le=250_000)
    crop_finding_retry_count: int = Field(default=1, alias="CROP_FINDING_RETRY_COUNT", ge=0, le=2)

    @model_validator(mode="after")
    def validate_crop_finding_timeouts(self) -> "Settings":
        if self.crop_finding_web_search_timeout_seconds >= self.crop_finding_overall_timeout_seconds:
            raise ValueError("CROP_FINDING_WEB_SEARCH_TIMEOUT_SECONDS must be shorter than CROP_FINDING_OVERALL_TIMEOUT_SECONDS.")
        return self

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")


@lru_cache
def get_settings() -> Settings:
    return Settings()

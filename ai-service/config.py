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
    inspection_image_overall_timeout_seconds: float = Field(default=120, alias="INSPECTION_IMAGE_OVERALL_TIMEOUT_SECONDS", ge=60, le=150)
    inspection_image_pass1_timeout_seconds: float = Field(default=30, alias="INSPECTION_IMAGE_PASS1_TIMEOUT_SECONDS", ge=10, le=45)
    inspection_image_pass2_timeout_seconds: float = Field(default=45, alias="INSPECTION_IMAGE_PASS2_TIMEOUT_SECONDS", ge=15, le=60)
    inspection_image_retrieval_timeout_seconds: float = Field(default=35, alias="INSPECTION_IMAGE_RETRIEVAL_TIMEOUT_SECONDS", ge=10, le=45)
    inspection_image_stage1_entry_limit: int = Field(default=4, alias="INSPECTION_IMAGE_STAGE1_ENTRY_LIMIT", ge=1, le=4)
    inspection_image_stage1_document_limit: int = Field(default=6, alias="INSPECTION_IMAGE_STAGE1_DOCUMENT_LIMIT", ge=1, le=6)
    inspection_image_stage2_entry_limit: int = Field(default=3, alias="INSPECTION_IMAGE_STAGE2_ENTRY_LIMIT", ge=1, le=3)
    inspection_image_stage2_document_limit: int = Field(default=4, alias="INSPECTION_IMAGE_STAGE2_DOCUMENT_LIMIT", ge=1, le=4)
    inspection_image_candidate_links_per_page: int = Field(default=20, alias="INSPECTION_IMAGE_CANDIDATE_LINKS_PER_PAGE", ge=1, le=20)
    inspection_image_evidence_document_limit: int = Field(default=3, alias="INSPECTION_IMAGE_EVIDENCE_DOCUMENT_LIMIT", ge=1, le=3)
    inspection_image_evidence_chars_per_source: int = Field(default=8000, alias="INSPECTION_IMAGE_EVIDENCE_CHARS_PER_SOURCE", ge=500, le=8000)
    inspection_image_evidence_total_chars: int = Field(default=20000, alias="INSPECTION_IMAGE_EVIDENCE_TOTAL_CHARS", ge=1000, le=20000)
    inspection_image_min_readable_chars: int = Field(default=200, alias="INSPECTION_IMAGE_MIN_READABLE_CHARS", ge=100, le=1000)
    inspection_image_relevance_threshold: int = Field(default=16, alias="INSPECTION_IMAGE_RELEVANCE_THRESHOLD", ge=5, le=100)
    inspection_image_strong_evidence_threshold: int = Field(default=30, alias="INSPECTION_IMAGE_STRONG_EVIDENCE_THRESHOLD", ge=10, le=150)
    inspection_image_duplicate_similarity: float = Field(default=0.85, alias="INSPECTION_IMAGE_DUPLICATE_SIMILARITY", ge=0.5, le=1)
    crop_finding_candidate_limit: int = Field(default=8, alias="CROP_FINDING_CANDIDATE_LIMIT", ge=1, le=20)
    crop_finding_stage1_retrieval_limit: int = Field(default=5, alias="CROP_FINDING_STAGE1_RETRIEVAL_LIMIT", ge=1, le=10)
    crop_finding_stage2_retrieval_limit: int = Field(default=3, alias="CROP_FINDING_STAGE2_RETRIEVAL_LIMIT", ge=1, le=6)
    crop_finding_connect_timeout_seconds: float = Field(default=4, alias="CROP_FINDING_CONNECT_TIMEOUT_SECONDS", gt=0, le=30)
    crop_finding_read_timeout_seconds: float = Field(default=12, alias="CROP_FINDING_READ_TIMEOUT_SECONDS", gt=0, le=60)
    crop_finding_document_timeout_seconds: float = Field(default=18, alias="CROP_FINDING_DOCUMENT_TIMEOUT_SECONDS", gt=0, le=90)
    crop_finding_web_search_timeout_seconds: float = Field(default=50, alias="CROP_FINDING_WEB_SEARCH_TIMEOUT_SECONDS", gt=0, le=90)
    crop_finding_structured_analysis_timeout_seconds: float = Field(
        default=45,
        alias="CROP_FINDING_STRUCTURED_ANALYSIS_TIMEOUT_SECONDS",
        gt=0,
        le=90,
    )
    crop_finding_completion_safety_margin_seconds: float = Field(
        default=5,
        alias="CROP_FINDING_COMPLETION_SAFETY_MARGIN_SECONDS",
        gt=0,
        le=30,
    )
    crop_finding_overall_timeout_seconds: float = Field(default=105, alias="CROP_FINDING_OVERALL_TIMEOUT_SECONDS", gt=10, le=180)
    crop_finding_max_redirects: int = Field(default=3, alias="CROP_FINDING_MAX_REDIRECTS", ge=0, le=5)
    crop_finding_max_html_bytes: int = Field(default=2_000_000, alias="CROP_FINDING_MAX_HTML_BYTES", ge=100_000, le=5_000_000)
    crop_finding_max_pdf_bytes: int = Field(default=10_000_000, alias="CROP_FINDING_MAX_PDF_BYTES", ge=500_000, le=25_000_000)
    crop_finding_max_pdf_pages: int = Field(default=60, alias="CROP_FINDING_MAX_PDF_PAGES", ge=1, le=150)
    crop_finding_max_extracted_chars_per_source: int = Field(default=45_000, alias="CROP_FINDING_MAX_EXTRACTED_CHARS_PER_SOURCE", ge=5_000, le=100_000)
    crop_finding_max_total_extracted_chars: int = Field(default=100_000, alias="CROP_FINDING_MAX_TOTAL_EXTRACTED_CHARS", ge=10_000, le=250_000)
    crop_finding_analysis_max_chars_per_source: int = Field(
        default=12_000,
        alias="CROP_FINDING_ANALYSIS_MAX_CHARS_PER_SOURCE",
        ge=2_000,
        le=50_000,
    )
    crop_finding_analysis_max_total_chars: int = Field(
        default=45_000,
        alias="CROP_FINDING_ANALYSIS_MAX_TOTAL_CHARS",
        ge=5_000,
        le=100_000,
    )
    crop_finding_retry_count: int = Field(default=1, alias="CROP_FINDING_RETRY_COUNT", ge=0, le=2)

    @model_validator(mode="after")
    def validate_crop_finding_timeouts(self) -> "Settings":
        if self.crop_finding_web_search_timeout_seconds >= self.crop_finding_overall_timeout_seconds:
            raise ValueError("CROP_FINDING_WEB_SEARCH_TIMEOUT_SECONDS must be shorter than CROP_FINDING_OVERALL_TIMEOUT_SECONDS.")
        if (
            self.crop_finding_structured_analysis_timeout_seconds
            + self.crop_finding_completion_safety_margin_seconds
            >= self.crop_finding_overall_timeout_seconds
        ):
            raise ValueError(
                "CROP_FINDING_STRUCTURED_ANALYSIS_TIMEOUT_SECONDS plus the completion safety margin "
                "must be shorter than CROP_FINDING_OVERALL_TIMEOUT_SECONDS."
            )
        if self.crop_finding_analysis_max_chars_per_source > self.crop_finding_max_extracted_chars_per_source:
            raise ValueError(
                "CROP_FINDING_ANALYSIS_MAX_CHARS_PER_SOURCE cannot exceed "
                "CROP_FINDING_MAX_EXTRACTED_CHARS_PER_SOURCE."
            )
        if self.crop_finding_analysis_max_total_chars > self.crop_finding_max_total_extracted_chars:
            raise ValueError(
                "CROP_FINDING_ANALYSIS_MAX_TOTAL_CHARS cannot exceed CROP_FINDING_MAX_TOTAL_EXTRACTED_CHARS."
            )
        return self

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")


@lru_cache
def get_settings() -> Settings:
    return Settings()

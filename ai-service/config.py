from functools import lru_cache
from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    ai_service_token: str = Field(default="", alias="AI_SERVICE_TOKEN")
    ai_provider: Literal["gemini", "openai"] = Field(default="gemini", alias="AI_PROVIDER")
    ai_model: str = Field(default="", alias="AI_MODEL")
    gemini_api_key: str = Field(default="", alias="GEMINI_API_KEY")
    openai_api_key: str = Field(default="", alias="OPENAI_API_KEY")
    backend_tool_base_url: str = Field(default="http://localhost:5000", alias="BACKEND_TOOL_BASE_URL")
    backend_tool_token: str = Field(default="", alias="BACKEND_TOOL_TOKEN")
    tool_timeout_seconds: float = 8
    provider_timeout_seconds: float = 30

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")


@lru_cache
def get_settings() -> Settings:
    return Settings()

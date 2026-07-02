from functools import lru_cache

from pydantic import Field, SecretStr, ValidationError, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    openai_api_key: SecretStr = Field(alias="OPENAI_API_KEY")
    openai_model: str = Field(default="gpt-4o", alias="OPENAI_MODEL")
    openai_temperature: float = Field(default=0.3, alias="OPENAI_TEMPERATURE", ge=0.0, le=2.0)
    openai_max_tokens: int = Field(default=512, alias="OPENAI_MAX_TOKENS", gt=0, le=8192)
    database_url: str = Field(default="sqlite:///./chat.db", alias="DATABASE_URL")

    @model_validator(mode="after")
    def validate_api_key_present(self) -> "Settings":
        if not self.openai_api_key.get_secret_value().strip():
            raise ValueError("OPENAI_API_KEY must not be empty.")
        return self


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    try:
        return Settings()
    except ValidationError as exc:
        raise RuntimeError(f"Invalid application settings: {exc}") from exc


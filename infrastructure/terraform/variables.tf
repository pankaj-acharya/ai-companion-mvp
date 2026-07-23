variable "project_name" {
  type        = string
  description = "Short project identifier used in resource names."
  default     = "aicomp"
}

variable "environment" {
  type        = string
  description = "Deployment environment name."
  default     = "dev"

  validation {
    condition     = contains(["dev", "test", "prod"], var.environment)
    error_message = "environment must be one of: dev, test, prod."
  }
}

variable "location" {
  type        = string
  description = "Azure region for resources."
  default     = "eastus"
}

variable "tags" {
  type        = map(string)
  description = "Additional tags to apply to all resources."
  default     = {}
}

variable "app_service_sku_name" {
  type        = string
  description = "Linux App Service Plan SKU name."
  default     = "B1"
}

variable "use_mock_llm" {
  type        = bool
  description = "When true, app starts in mock mode and does not require OPENAI_API_KEY."
  default     = true
}

variable "llm_provider" {
  type        = string
  description = "LLM provider expected by the app (openai or ollama)."
  default     = "openai"

  validation {
    condition     = contains(["openai", "ollama"], lower(var.llm_provider))
    error_message = "llm_provider must be either openai or ollama."
  }
}

variable "openai_api_key" {
  type        = string
  description = "OpenAI API key, required only when use_mock_llm is false and provider is openai."
  default     = ""
  sensitive   = true

  validation {
    condition     = var.use_mock_llm || lower(var.llm_provider) == "ollama" || length(trim(var.openai_api_key)) > 0
    error_message = "openai_api_key is required when use_mock_llm is false and llm_provider is openai."
  }
}

variable "openai_model" {
  type        = string
  description = "Model name for OPENAI_MODEL setting."
  default     = "gpt-4o"
}

variable "llm_base_url" {
  type        = string
  description = "Optional explicit LLM base URL. Empty means app defaults will apply."
  default     = ""
}

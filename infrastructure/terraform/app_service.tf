resource "azurerm_service_plan" "main" {
  name                = "asp-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku_name
  tags                = local.common_tags
}

resource "azurerm_linux_web_app" "api" {
  name                = "app-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true
  tags                = local.common_tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                               = var.environment == "prod"
    application_stack {
      dotnet_version = "8.0"
    }
    ftps_state                              = "FtpsOnly"
    minimum_tls_version                     = "1.2"
    http2_enabled                           = true
    use_32_bit_worker                       = false
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"                      = var.environment == "prod" ? "Production" : "Development"
    "APPLICATIONINSIGHTS_CONNECTION_STRING"       = azurerm_application_insights.main.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION"  = "~3"
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE"         = "true"
    "WEBSITE_HEALTHCHECK_MAXPINGFAILURES"         = "3"
    "ConnectionStrings__DefaultConnection"        = "Data Source=/home/data/chat.db"
    "LLM_PROVIDER"                                = lower(var.llm_provider)
    "OPENAI_USE_MOCK"                             = tostring(var.use_mock_llm)
    "OPENAI_MODEL"                                = var.openai_model
    "OPENAI_API_KEY"                              = var.openai_api_key
    "LLM_BASE_URL"                                = var.llm_base_url
  }

  logs {
    application_logs {
      file_system_level = "Information"
    }

    http_logs {
      file_system {
        retention_in_days = 3
        retention_in_mb   = 35
      }
    }
  }
}

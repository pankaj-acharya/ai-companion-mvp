output "resource_group_name" {
  description = "Resource group where the app is deployed."
  value       = azurerm_resource_group.main.name
}

output "app_service_name" {
  description = "App Service name used by deployment job."
  value       = azurerm_linux_web_app.api.name
}

output "app_service_default_hostname" {
  description = "Default app hostname."
  value       = azurerm_linux_web_app.api.default_hostname
}

output "app_service_url" {
  description = "Public HTTPS URL for the API."
  value       = "https://${azurerm_linux_web_app.api.default_hostname}"
}

locals {
  suffix = lower(random_string.name_suffix.result)

  base_name = lower(join("-", [
    var.project_name,
    var.environment,
    local.suffix
  ]))

  common_tags = merge({
    project     = "ai-companion-mvp"
    environment = var.environment
    managed_by  = "terraform"
  }, var.tags)
}

# Security Notes

## Authentication model

The deployment pipeline uses OpenID Connect (OIDC) via `azure/login`. This avoids storing Azure client secrets in GitHub.

## Least privilege recommendation

Current script assigns `Contributor` at subscription scope for simplicity. For tighter security:

1. Create a dedicated resource group for this workload.
2. Scope the role assignment to that resource group only.
3. Consider splitting roles: one for Terraform apply, one for app deploy.

## Secret handling

- Keep `OPENAI_API_KEY` in GitHub secrets.
- Terraform variable `openai_api_key` is marked sensitive.
- Use `use_mock_llm=true` while testing without live OpenAI usage.

## State protection

- Terraform state is stored in Azure Storage.
- Restrict state account access via RBAC.
- Enable soft delete and versioning on the storage account.

# Pipelines

This folder stores pipeline design assets and operational notes.

Executable GitHub Actions workflow files must be kept in `.github/workflows` by GitHub platform rules.

## Workflows in this repository

- `build-app.yml`: Runs CI build, tests, publish, and uploads app package artifact.
- `terraform-validate-plan.yml`: Runs on pull requests to validate Terraform and generate a dev plan artifact.
- `deploy-infra.yml`: Applies Terraform with approval gate for the selected environment.
- `deploy-app.yml`: Deploys the app package to App Service and runs health verification.

## Required GitHub repository secrets

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `OPENAI_API_KEY` (optional when mock mode is enabled)

## Required GitHub repository variables

- `TF_BACKEND_RESOURCE_GROUP`
- `TF_BACKEND_STORAGE_ACCOUNT`
- `TF_BACKEND_CONTAINER`
- `TF_BACKEND_STATE_KEY_DEV`
- `TF_BACKEND_STATE_KEY_PROD`

## Optional GitHub repository variables for targeting

- `AZURE_GITHUB_ENVIRONMENT`: name of your GitHub Environment used for approvals (for example, `personal-subscription`).
- `AZURE_CLIENT_ID`: optional override for service principal/app registration client id.
- `AZURE_TENANT_ID`: optional override for tenant id.
- `AZURE_SUBSCRIPTION_ID_DEV`: subscription id for dev deployments.
- `AZURE_SUBSCRIPTION_ID_PROD`: subscription id for prod deployments.

## Workflow runtime override

- `deploy-infra.yml` and `deploy-app.yml` accept `azure_subscription_id` in manual runs (`workflow_dispatch`) to override subscription targeting per run.

## Approval gate

Create a GitHub Environment and set its name in `AZURE_GITHUB_ENVIRONMENT`. The deploy workflows use that environment before Terraform apply and app deployment.

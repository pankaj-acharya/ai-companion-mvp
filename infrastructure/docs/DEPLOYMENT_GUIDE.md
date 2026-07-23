# Azure Deployment Guide

## 1. One-time prerequisites

- Azure subscription (personal)
- GitHub repository access
- Azure CLI and Terraform locally for manual runs

## 2. Bootstrap Terraform state backend

Run:

```powershell
./infrastructure/scripts/bootstrap-tf-backend.ps1 -Location eastus -ResourceGroupName rg-aicomp-tfstate -StorageAccountName <globally-unique-name> -ContainerName tfstate
```

Add GitHub repository variables:

- `TF_BACKEND_RESOURCE_GROUP`
- `TF_BACKEND_STORAGE_ACCOUNT`
- `TF_BACKEND_CONTAINER`
- `TF_BACKEND_STATE_KEY_DEV`
- `TF_BACKEND_STATE_KEY_PROD`

## 3. Configure OIDC between GitHub and Azure

Run:

```powershell
./infrastructure/scripts/setup-oidc.ps1 -GithubOwner <owner> -GithubRepo ai-companion-mvp -Branch main
```

Add GitHub secrets from script output:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Optional secret:

- `OPENAI_API_KEY`

Optional repository variables for targeting:

- `AZURE_GITHUB_ENVIRONMENT` (example: `personal-subscription`)
- `AZURE_SUBSCRIPTION_ID_DEV`
- `AZURE_SUBSCRIPTION_ID_PROD`
- `AZURE_CLIENT_ID` and `AZURE_TENANT_ID` (if you prefer variables over secrets)

## 4. Configure manual approval gate

- Create GitHub Environment (for example, `personal-subscription`)
- Add required reviewers
- Optionally restrict deployment branches to `main`
- Set repository variable `AZURE_GITHUB_ENVIRONMENT` to this environment name

## 5. Deploy flows

### Pull request flow

1. Open PR to `main`
2. Workflow `Terraform Validate and Plan` runs
3. Download `terraform-plan-dev` artifact and review changes

### Deployment flow

1. Merge to `main` or run workflows manually
2. CI workflow `Build App` runs for build, tests, and package generation
3. Workflow `Deploy Infrastructure` applies Terraform after your configured GitHub Environment approval
4. Workflow `Deploy App` deploys the API package and verifies `GET /health`

Note: Manual runs of Deploy Infrastructure and Deploy App can pass `azure_subscription_id` to target a specific subscription for that run.

## 6. Local Terraform commands (optional)

```powershell
cd infrastructure/terraform
terraform init -backend-config=backend.hcl.example
terraform fmt -recursive
terraform validate
terraform plan -var-file=terraform.tfvars.dev
```

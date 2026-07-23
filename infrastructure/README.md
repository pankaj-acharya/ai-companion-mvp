# Infrastructure

This folder contains Infrastructure as Code and deployment support scripts.

## Contents

- `terraform/`: Azure resources for this MVP API.
- `scripts/`: one-time setup and helper scripts for backend/OIDC/deploy.
- `docs/`: deployment and security operating guides.

## Quick start

1. Bootstrap Terraform backend storage.
2. Configure GitHub OIDC for your personal subscription.
3. Push a PR and review Terraform plan.
4. Merge to `main` and approve deployment environment gate.

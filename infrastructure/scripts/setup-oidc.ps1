param(
    [Parameter(Mandatory = $true)]
    [string]$GithubOwner,

    [Parameter(Mandatory = $true)]
    [string]$GithubRepo,

    [Parameter(Mandatory = $false)]
    [string]$Branch = "main",

    [Parameter(Mandatory = $false)]
    [string]$AppDisplayName = "ai-companion-mvp-gha-oidc"
)

$ErrorActionPreference = "Stop"

$subscriptionId = az account show --query id -o tsv
$tenantId = az account show --query tenantId -o tsv

Write-Host "Creating or finding Entra app registration..."
$appId = az ad app list --display-name $AppDisplayName --query "[0].appId" -o tsv
if ([string]::IsNullOrWhiteSpace($appId)) {
    $appId = az ad app create --display-name $AppDisplayName --query appId -o tsv
}

Write-Host "Creating service principal for the app..."
az ad sp create --id $appId | Out-Null

$subject = "repo:$GithubOwner/$GithubRepo:ref:refs/heads/$Branch"
$credentialJson = @"
{
  \"name\": \"github-main-$Branch\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"$subject\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}
"@

$tempFile = Join-Path $env:TEMP "oidc-credential.json"
Set-Content -Path $tempFile -Value $credentialJson -Encoding ascii

Write-Host "Adding federated credential..."
az ad app federated-credential create --id $appId --parameters @$tempFile | Out-Null

Write-Host "Assigning Contributor role on current subscription..."
az role assignment create \
    --assignee $appId \
    --role Contributor \
    --scope "/subscriptions/$subscriptionId" | Out-Null

Write-Host "OIDC setup complete. Add these GitHub secrets:"
Write-Host "AZURE_CLIENT_ID=$appId"
Write-Host "AZURE_TENANT_ID=$tenantId"
Write-Host "AZURE_SUBSCRIPTION_ID=$subscriptionId"

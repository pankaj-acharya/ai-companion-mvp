param(
    [Parameter(Mandatory = $false)]
    [string]$Location = "eastus",

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-aicomp-tfstate",

    [Parameter(Mandatory = $false)]
    [string]$StorageAccountName = "aicomptfstate12345",

    [Parameter(Mandatory = $false)]
    [string]$ContainerName = "tfstate"
)

$ErrorActionPreference = "Stop"

Write-Host "Creating resource group if missing..."
az group create --name $ResourceGroupName --location $Location | Out-Null

Write-Host "Creating storage account if missing..."
az storage account create \
    --resource-group $ResourceGroupName \
    --name $StorageAccountName \
    --location $Location \
    --sku Standard_LRS \
    --kind StorageV2 \
    --allow-blob-public-access false | Out-Null

Write-Host "Creating blob container if missing..."
$accountKey = az storage account keys list \
    --resource-group $ResourceGroupName \
    --account-name $StorageAccountName \
    --query "[0].value" -o tsv

az storage container create \
    --name $ContainerName \
    --account-name $StorageAccountName \
    --account-key $accountKey | Out-Null

Write-Host "Backend bootstrap complete."
Write-Host "Use these values in backend.hcl.example and GitHub repository variables."

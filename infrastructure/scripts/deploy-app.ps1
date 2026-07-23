param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $false)]
    [string]$OutputZip = "app.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing API..."
dotnet publish .\src\AiCompanion.Api\AiCompanion.Api.csproj -c Release -o .\publish

if (Test-Path $OutputZip) {
    Remove-Item $OutputZip -Force
}

Write-Host "Creating zip package..."
Compress-Archive -Path .\publish\* -DestinationPath $OutputZip -Force

Write-Host "Deploying package to Azure Web App..."
az webapp deploy \
    --resource-group $ResourceGroupName \
    --name $WebAppName \
    --src-path $OutputZip \
    --type zip | Out-Null

Write-Host "Running health check..."
$hostname = az webapp show --resource-group $ResourceGroupName --name $WebAppName --query defaultHostName -o tsv
Invoke-RestMethod "https://$hostname/health" | Out-Null

Write-Host "Deployment complete: https://$hostname"

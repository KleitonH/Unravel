# Baixa o modelo MiniLM multilíngue (~120MB ONNX + ~17MB tokenizer)
# para backend/models/minilm/.
#
# Modelo: sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2
# Suporta PT-BR + 100 outras línguas; output 384-dim L2-normalizado.
#
# Uso (no PowerShell, da pasta backend/):
#   .\scripts\download-minilm.ps1
#
# Depois, em appsettings.Development.json, mude:
#   "Embedding": { "Enabled": true, ... }

$ErrorActionPreference = 'Stop'

$repo = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
$base = "https://huggingface.co/$repo/resolve/main"
$dest = Join-Path $PSScriptRoot ".." "models" "minilm"

if (-not (Test-Path $dest)) {
    New-Item -ItemType Directory -Path $dest | Out-Null
}

$files = @(
    @{ Url = "$base/onnx/model.onnx";   Path = Join-Path $dest "model.onnx";     Mb = 120 },
    @{ Url = "$base/tokenizer.json";    Path = Join-Path $dest "tokenizer.json"; Mb = 17 }
)

foreach ($f in $files) {
    if (Test-Path $f.Path) {
        $sizeMb = [math]::Round((Get-Item $f.Path).Length / 1MB, 1)
        Write-Host "✓ $($f.Path) já existe ($sizeMb MB), pulando." -ForegroundColor Green
        continue
    }
    Write-Host "↓ Baixando $($f.Url) (~$($f.Mb) MB)…" -ForegroundColor Cyan
    Invoke-WebRequest -Uri $f.Url -OutFile $f.Path -UseBasicParsing
    $sizeMb = [math]::Round((Get-Item $f.Path).Length / 1MB, 1)
    Write-Host "✓ Salvo em $($f.Path) ($sizeMb MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Pronto. Próximos passos:" -ForegroundColor Yellow
Write-Host "  1. Edite backend/src/Unravel.API/appsettings.Development.json:"
Write-Host '       "Embedding": { "Enabled": true, ... }'
Write-Host "  2. Reinicie a API ou rode: dotnet run --project src/Unravel.API -- forge:eval"

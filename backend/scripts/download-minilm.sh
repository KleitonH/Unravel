#!/usr/bin/env bash
# Baixa MiniLM multilíngue (~120MB ONNX + ~17MB tokenizer) pra
# backend/models/minilm/. Equivalente bash do download-minilm.ps1.
#
# Uso (da pasta backend/):
#   bash scripts/download-minilm.sh
set -euo pipefail

REPO="sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
BASE="https://huggingface.co/${REPO}/resolve/main"
DEST="$(dirname "$0")/../models/minilm"

mkdir -p "$DEST"

download() {
    local url="$1"
    local out="$2"
    local size_mb="$3"
    if [[ -f "$out" ]]; then
        local actual_mb
        actual_mb=$(du -m "$out" | cut -f1)
        echo "✓ $out já existe (${actual_mb} MB), pulando."
        return
    fi
    echo "↓ Baixando $url (~${size_mb} MB)…"
    # -L segue redirect (HF usa CDN); --fail aborta em HTTP error
    curl -fL --progress-bar -o "$out" "$url"
    local actual_mb
    actual_mb=$(du -m "$out" | cut -f1)
    echo "✓ Salvo em $out (${actual_mb} MB)"
}

download "${BASE}/onnx/model.onnx"    "${DEST}/model.onnx"     120
download "${BASE}/tokenizer.json"     "${DEST}/tokenizer.json"  17

echo
echo "Pronto. Próximos passos:"
echo "  1. Edite backend/src/Unravel.API/appsettings.Development.json:"
echo '       "Embedding": { "Enabled": true, ... }'
echo "  2. Reinicie a API ou rode: dotnet run --project src/Unravel.API -- forge:eval"

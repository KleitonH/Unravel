#!/usr/bin/env bash
# PR 18 — Baixa o modelo MiniLM multilíngue (ONNX + tokenizer) para uso
# no SemanticDistractorPicker. Idempotente: pula o download se já existe.
#
# Uso:
#   ./scripts/download-minilm.sh [destino]
#
# Default: /var/lib/unravel/models/minilm/  (Linux)  ou  ./models/minilm/  (dev local)
#
# Variáveis de ambiente:
#   MODEL_DIR — sobrescreve o destino
#
# Tamanho final: ~120 MB
#
# Após o download, configurar em appsettings.json:
#   "Embedding": {
#     "Enabled": true,
#     "ModelPath": ".../minilm/model.onnx",
#     "TokenizerPath": ".../minilm/tokenizer.json"
#   }

set -euo pipefail

DEFAULT_DIR="/var/lib/unravel/models/minilm"
[ ! -w "$(dirname "$DEFAULT_DIR")" 2>/dev/null ] && DEFAULT_DIR="./models/minilm"

DEST="${MODEL_DIR:-${1:-$DEFAULT_DIR}}"
mkdir -p "$DEST"

REPO="xenova/paraphrase-multilingual-MiniLM-L12-v2"
BASE_URL="https://huggingface.co/${REPO}/resolve/main"

# Pares "origem→destino" — o Xenova publica o ONNX dentro de onnx/.
FILES=(
    "onnx/model.onnx:model.onnx"
    "tokenizer.json:tokenizer.json"
)

echo "→ Baixando modelo MiniLM multilíngue para: ${DEST}"
for pair in "${FILES[@]}"; do
    src="${pair%%:*}"
    name="${pair##*:}"
    dst="${DEST}/${name}"
    if [ -f "$dst" ]; then
        size=$(stat -c%s "$dst" 2>/dev/null || stat -f%z "$dst")
        echo "  ✓ ${name} já existe ($size bytes); pulando."
        continue
    fi
    echo "  ↓ ${src}"
    curl -fsSL "${BASE_URL}/${src}" -o "$dst" || {
        echo "ERRO: falha ao baixar ${BASE_URL}/${src}" >&2
        exit 1
    }
done

echo "✓ Pronto. Configure em appsettings:"
echo '  "Embedding": {'
echo '    "Enabled": true,'
echo "    \"ModelPath\": \"${DEST}/model.onnx\","
echo "    \"TokenizerPath\": \"${DEST}/tokenizer.json\""
echo '  }'

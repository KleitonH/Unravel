#!/usr/bin/env bash
# PR 20 — Baixa um LLM pequeno em formato .gguf pra LlmChallengeStrategy.
# Default: Phi-3.5 Mini Instruct Q4_K_M (~2.3 GB, melhor qualidade pt-br
# para o tamanho). Alternativa: Qwen 2.5 3B Instruct Q4_K_M (~1.9 GB).
#
# Uso:
#   ./scripts/download-llm.sh [modelo]
#
# Modelos suportados:
#   phi    → Phi-3.5 Mini Q4_K_M (default)
#   qwen   → Qwen 2.5 3B Q4_K_M
#
# Destino: /var/lib/unravel/models/llm/{nome}.gguf (Linux)
#       ou ./models/llm/{nome}.gguf (dev local)
#
# Pós-download, configurar em appsettings.json:
#   "Llm": {
#     "Enabled": true,
#     "ModelPath": ".../phi-3.5-mini.gguf"
#   }

set -euo pipefail

MODEL="${1:-phi}"
case "$MODEL" in
    phi)
        URL="https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/Phi-3.5-mini-instruct-Q4_K_M.gguf"
        NAME="phi-3.5-mini.gguf"
        ;;
    qwen)
        URL="https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF/resolve/main/qwen2.5-3b-instruct-q4_k_m.gguf"
        NAME="qwen-2.5-3b.gguf"
        ;;
    *)
        echo "Modelo desconhecido: $MODEL. Use 'phi' ou 'qwen'." >&2
        exit 1
        ;;
esac

DEFAULT_DIR="/var/lib/unravel/models/llm"
[ ! -w "$(dirname "$DEFAULT_DIR")" 2>/dev/null ] && DEFAULT_DIR="./models/llm"

DEST="${MODEL_DIR:-$DEFAULT_DIR}"
mkdir -p "$DEST"
TARGET="${DEST}/${NAME}"

if [ -f "$TARGET" ]; then
    size=$(stat -c%s "$TARGET" 2>/dev/null || stat -f%z "$TARGET")
    echo "✓ ${NAME} já existe (${size} bytes); pulando download."
else
    echo "↓ Baixando ${NAME} (~2 GB; vai demorar):"
    echo "  $URL"
    curl -fL --progress-bar "$URL" -o "$TARGET" || {
        echo "ERRO: falha ao baixar $URL" >&2
        rm -f "$TARGET"
        exit 1
    }
fi

echo ""
echo "✓ Pronto. Configure em appsettings:"
echo '  "Llm": {'
echo '    "Enabled": true,'
echo "    \"ModelPath\": \"${TARGET}\","
echo '    "GpuLayerCount": 0,'
echo '    "ContextSize": 2048,'
echo '    "MaxTokens": 400,'
echo '    "Temperature": 0.7'
echo '  }'

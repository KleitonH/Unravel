#!/bin/bash
# ----------------------------------------------------------------
# Script de inicialização do servidor — Unravel
# Ubuntu 24.04 LTS
# Execute UMA VEZ no servidor antes do primeiro deploy:
#   chmod +x setup.sh && sudo bash setup.sh
# ----------------------------------------------------------------
set -e

# ── Configuração ────────────────────────────────────────────────
PROJECT_PATH="/opt/unravel"   # mesmo valor do secret PROJECT_PATH
DEPLOY_USER="root"            # mesmo valor do secret SSH_USERNAME
API_PORT="5000"
DB_NAME="unravel_db"
DB_USER="unravel_user"
# ────────────────────────────────────────────────────────────────

echo ""
read -rsp "Senha do banco de dados PostgreSQL ($DB_USER): " DB_PASSWORD
echo ""
read -rsp "Valor de Jwt__Key (mín. 32 chars): " JWT_KEY
echo ""

# ── Dependências ────────────────────────────────────────────────
echo "[1/6] Instalando dependências..."
apt-get update -q
apt-get install -y -q apache2 postgresql

# .NET 8 ASP.NET Core Runtime
if ! command -v dotnet &>/dev/null; then
    apt-get install -y -q aspnetcore-runtime-8.0 || {
        # fallback: repositório oficial Microsoft
        wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb
        dpkg -i packages-microsoft-prod.deb
        rm packages-microsoft-prod.deb
        apt-get update -q
        apt-get install -y -q aspnetcore-runtime-8.0
    }
fi

# ── Diretórios do projeto ───────────────────────────────────────
echo "[2/6] Criando diretórios em $PROJECT_PATH..."
mkdir -p "$PROJECT_PATH/api"
mkdir -p "$PROJECT_PATH/frontend"
chown -R "$DEPLOY_USER:$DEPLOY_USER" "$PROJECT_PATH"

# ── Banco de dados ──────────────────────────────────────────────
echo "[3/6] Configurando PostgreSQL..."
systemctl enable postgresql
systemctl start postgresql

sudo -u postgres psql -tc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" \
    | grep -q 1 || sudo -u postgres psql -c \
    "CREATE USER $DB_USER WITH PASSWORD '$DB_PASSWORD';"

sudo -u postgres psql -tc "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" \
    | grep -q 1 || sudo -u postgres psql -c \
    "CREATE DATABASE $DB_NAME OWNER $DB_USER;"

# ── Arquivo .env ────────────────────────────────────────────────
echo "[4/6] Criando $PROJECT_PATH/.env..."
cat > "$PROJECT_PATH/.env" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:$API_PORT

ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD

Jwt__Key=$JWT_KEY
Jwt__Issuer=unravel-api
Jwt__Audience=unravel-client
Jwt__ExpiresInMinutes=60
EOF
chmod 600 "$PROJECT_PATH/.env"
chown "$DEPLOY_USER:$DEPLOY_USER" "$PROJECT_PATH/.env"

# ── Apache2 ─────────────────────────────────────────────────────
echo "[5/6] Configurando Apache2..."
a2enmod proxy proxy_http rewrite

cat > /etc/apache2/sites-available/unravel.conf <<EOF
<VirtualHost *:80>
    DocumentRoot $PROJECT_PATH/frontend

    <Directory $PROJECT_PATH/frontend>
        Options -Indexes +FollowSymLinks
        AllowOverride None
        Require all granted

        # Angular — roteamento client-side
        RewriteEngine On
        RewriteBase /
        RewriteRule ^index\.html$ - [L]
        RewriteCond %{REQUEST_FILENAME} !-f
        RewriteCond %{REQUEST_FILENAME} !-d
        RewriteRule . /index.html [L]
    </Directory>

    # Proxy para a API .NET
    ProxyPreserveHost On
    ProxyPass /api/ http://localhost:$API_PORT/api/
    ProxyPassReverse /api/ http://localhost:$API_PORT/api/
</VirtualHost>
EOF

a2ensite unravel
a2dissite 000-default || true
apache2ctl configtest
systemctl enable apache2
systemctl restart apache2

# ── Sudoers ─────────────────────────────────────────────────────
echo "[6/6] Configurando permissões sudo..."
cat > /etc/sudoers.d/unravel-deploy <<EOF
$DEPLOY_USER ALL=(ALL) NOPASSWD: /bin/systemctl daemon-reload
$DEPLOY_USER ALL=(ALL) NOPASSWD: /bin/systemctl enable unravel-api
$DEPLOY_USER ALL=(ALL) NOPASSWD: /bin/systemctl restart unravel-api
$DEPLOY_USER ALL=(ALL) NOPASSWD: /bin/systemctl reload apache2
$DEPLOY_USER ALL=(ALL) NOPASSWD: /bin/mv /tmp/unravel-api.service /etc/systemd/system/unravel-api.service
EOF
chmod 440 /etc/sudoers.d/unravel-deploy

# ── Conclusão ───────────────────────────────────────────────────
echo ""
echo "✓ Servidor pronto. Próximos passos:"
echo "  1. Faça o push na branch main para disparar o primeiro deploy"
echo "  2. Acompanhe em: https://github.com/<seu-repo>/actions"
echo ""
echo "  ATENÇÃO: o frontend está com apiUrl = 'http://localhost:5000'"
echo "  Isso só funciona se o acesso vier do próprio servidor."
echo "  Para uso remoto, atualize frontend/src/environments/environment.production.ts"
echo "  com o IP ou domínio público do servidor."
echo ""

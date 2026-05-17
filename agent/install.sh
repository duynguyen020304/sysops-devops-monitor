#!/usr/bin/env bash
# ============================================================
# Monitoring Agent Installer
# ============================================================
# Self-configuring script. Backend injects BACKEND_URL and
# INSTALL_TOKEN at download time.
#
# Usage:
#   curl -fsSL "https://api/agent-install/download?t=X&pw=Y" | sudo bash
#   sudo bash install-agent.sh --uninstall
#   sudo bash install-agent.sh --dry-run
# ============================================================

set -euo pipefail

# --- Injected by backend at download time ---
BACKEND_URL=""
INSTALL_TOKEN=""

# --- Defaults ---
INSTALL_DIR="/opt/monitoring-agent"
PM2_NAME="monitoring-agent"
NODE_MAJOR=20

# --- Colors ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# --- Flags ---
DRY_RUN=false
UNINSTALL=false

# --- Parse args ---
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run) DRY_RUN=true; shift ;;
        --uninstall) UNINSTALL=true; shift ;;
        --api-url) BACKEND_URL="$2"; shift 2 ;;
        --token) INSTALL_TOKEN="$2"; shift 2 ;;
        --dir) INSTALL_DIR="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --api-url URL     Backend API URL"
            echo "  --token TOKEN     Install token"
            echo "  --dir DIR         Install directory (default: /opt/monitoring-agent)"
            echo "  --dry-run         Show commands without executing"
            echo "  --uninstall       Remove agent"
            echo "  -h, --help        Show this help"
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# --- Helpers ---
info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
ok()    { echo -e "${GREEN}[✓]${NC}    $*"; }
warn()  { echo -e "${YELLOW}[⚠]${NC}   $*"; }
err()   { echo -e "${RED}[✗]${NC}    $*" >&2; }
die()   { err "$@"; exit 1; }

run_cmd() {
    if $DRY_RUN; then
        echo -e "${YELLOW}[DRY]${NC}  $*"
    else
        "$@"
    fi
}

# --- Temp dir for cleanup ---
TMPDIR=""
cleanup() {
    [[ -n "$TMPDIR" ]] && [[ -d "$TMPDIR" ]] && rm -rf "$TMPDIR"
}
trap cleanup EXIT
# ==========================================
# Uninstall
# ==========================================
if $UNINSTALL; then
    info "Uninstalling monitoring agent..."
    if command -v pm2 &>/dev/null; then
        run_cmd pm2 delete "$PM2_NAME" 2>/dev/null || true
        run_cmd pm2 save
    fi
    run_cmd rm -rf "$INSTALL_DIR"
    ok "Agent uninstalled from $INSTALL_DIR"
    exit 0
fi

# ==========================================
# Preflight
# ==========================================
[[ "$(id -u)" -ne 0 ]] && die "This script must be run as root (use sudo)."
[[ -z "$BACKEND_URL" ]] && die "BACKEND_URL not set. Use --api-url or download from dashboard."
[[ -z "$INSTALL_TOKEN" ]] && die "INSTALL_TOKEN not set. Use --token or download from dashboard."

info "========================================="
info " Monitoring Agent Installer"
info "========================================="
info "Backend: $BACKEND_URL"
info "Token:   ${INSTALL_TOKEN:0:8}..."
info "Dir:     $INSTALL_DIR"
echo ""

# ==========================================
# Runtime PATH bootstrap
# ==========================================
export PNPM_HOME="${PNPM_HOME:-/root/.local/share/pnpm}"
export PATH="$PNPM_HOME:$PNPM_HOME/bin:/root/.bun/bin:/root/.bun/install/global/node_modules/.bin:/root/.bun/install/global/node_modules/pm2/bin:$PATH"
if [[ -s "${NVM_DIR:-/root/.nvm}/nvm.sh" ]]; then
    # shellcheck disable=SC1091
    . "${NVM_DIR:-/root/.nvm}/nvm.sh"
fi
if ! command -v node &>/dev/null; then
    NODE_BIN=$(find /root/.nvm/versions/node -maxdepth 3 -type f -name node 2>/dev/null | sort -V | tail -1 || true)
    if [[ -n "$NODE_BIN" ]]; then
        export PATH="$(dirname "$NODE_BIN"):$PATH"
    fi
fi

# ==========================================
# Step 1: Register with backend
# ==========================================
info "Step 1/7: Registering server with backend..."

HOSTNAME_VAL=$(hostname 2>/dev/null || echo "unknown")
OS_VAL=$(uname -s 2>/dev/null || echo "unknown")
ARCH_VAL=$(uname -m 2>/dev/null || echo "unknown")

REGISTER_BODY=$(cat <<EOF
{"hostname":"$HOSTNAME_VAL","os":"$OS_VAL","arch":"$ARCH_VAL"}
EOF
)

REGISTER_RESPONSE=""
if ! $DRY_RUN; then
    REGISTER_RESPONSE=$(curl -sf -X POST "$BACKEND_URL/api/agent-install/register" \
        -H "Content-Type: application/json" \
        -H "X-Install-Token: $INSTALL_TOKEN" \
        -d "$REGISTER_BODY" 2>&1) || {
        err "Registration failed."
        err "Response: $REGISTER_RESPONSE"
        die "Check that your install token is valid and not expired."
    }

    # Parse response
    SERVER_ID=$(echo "$REGISTER_RESPONSE" | grep -o '"serverId":"[^"]*"' | head -1 | cut -d'"' -f4)
    SERVER_TOKEN=$(echo "$REGISTER_RESPONSE" | grep -o '"serverToken":"[^"]*"' | head -1 | cut -d'"' -f4)
    API_URL=$(echo "$REGISTER_RESPONSE" | grep -o '"apiUrl":"[^"]*"' | head -1 | cut -d'"' -f4)

    [[ -z "$SERVER_ID" ]] && die "Failed to parse serverId from registration response."
    [[ -z "$SERVER_TOKEN" ]] && die "Failed to parse serverToken from registration response."

    # Use returned apiUrl if available, otherwise use BACKEND_URL
    [[ -n "$API_URL" ]] && AGENT_API_URL="$API_URL" || AGENT_API_URL="$BACKEND_URL"

    ok "Server registered. ID: ${SERVER_ID:0:8}..."
else
    echo -e "${YELLOW}[DRY]${NC}  curl -sf -X POST $BACKEND_URL/api/agent-install/register -H 'X-Install-Token: ...' -d '$REGISTER_BODY'"
    SERVER_ID="dry-run-server-id"
    SERVER_TOKEN="dry-run-server-token"
    AGENT_API_URL="$BACKEND_URL"
fi
# ==========================================
# Step 2: Check/Install Node.js
# ==========================================
info "Step 2/7: Checking Node.js..."

if command -v node &>/dev/null; then
    NODE_VERSION=$(node -v 2>/dev/null | sed 's/v//' | cut -d. -f1)
    if [[ "$NODE_VERSION" -ge "$NODE_MAJOR" ]]; then
        ok "Node.js $(node -v) found"
    else
        warn "Node.js $(node -v) found, but $NODE_MAJOR+ required"
        info "Installing Node.js $NODE_MAJOR..."
        if command -v apt-get &>/dev/null; then
            run_cmd curl -fsSL https://deb.nodesource.com/setup_$NODE_MAJOR.x | bash -
            run_cmd apt-get install -y nodejs
        elif command -v dnf &>/dev/null; then
            run_cmd dnf install -y nodejs npm
        elif command -v yum &>/dev/null; then
            run_cmd yum install -y nodejs npm
        else
            die "Node.js $NODE_MAJOR+ required, but no supported package manager found. Install Node.js, then rerun."
        fi
    fi
else
    info "Node.js not found. Installing Node.js $NODE_MAJOR..."
    if ! $DRY_RUN; then
        if command -v apt-get &>/dev/null; then
            curl -fsSL https://deb.nodesource.com/setup_$NODE_MAJOR.x | bash -
            apt-get install -y nodejs
        elif command -v dnf &>/dev/null; then
            dnf install -y nodejs npm
        elif command -v yum &>/dev/null; then
            yum install -y nodejs npm
        else
            die "Node.js $NODE_MAJOR+ required, but no supported package manager found. Install Node.js, then rerun."
        fi
    fi
fi

# ==========================================
# Step 3: Check/Install pnpm + PM2
# ==========================================
info "Step 3/7: Checking pnpm and PM2..."

if ! command -v pnpm &>/dev/null; then
    info "Installing pnpm..."
    run_cmd npm install -g pnpm
fi
ok "pnpm $(pnpm -v 2>/dev/null || echo 'installed')"

if ! command -v pm2 &>/dev/null; then
    info "Installing PM2..."
    run_cmd npm install -g pm2
fi
ok "PM2 $(pm2 -v 2>/dev/null || echo 'installed')"

# ==========================================
# Step 4: Download agent files
# ==========================================
info "Step 4/7: Downloading agent files..."

if ! $DRY_RUN; then
    TMPDIR=$(mktemp -d)
    TARBALL="$TMPDIR/agent.tar.gz"

    curl -sf -o "$TARBALL" "$BACKEND_URL/api/agent-install/agent-files?t=$INSTALL_TOKEN" || {
        # Fallback: if agent-files endpoint fails, try to find dist locally
        warn "Could not download agent files from backend."
        die "Ensure agent is built (pnpm build) and deployed with the backend."
    }

    ok "Downloaded agent files"

    # Extract
    mkdir -p "$INSTALL_DIR"
    tar -xzf "$TARBALL" -C "$INSTALL_DIR"
    ok "Extracted to $INSTALL_DIR"
else
    echo -e "${YELLOW}[DRY]${NC}  curl -sf -o /tmp/agent.tar.gz $BACKEND_URL/api/agent-install/agent-files?t=..."
    echo -e "${YELLOW}[DRY]${NC}  mkdir -p $INSTALL_DIR && tar -xzf /tmp/agent.tar.gz -C $INSTALL_DIR"
fi
# ==========================================
# Step 5: Install dependencies
# ==========================================
info "Step 5/7: Installing dependencies..."

if ! $DRY_RUN; then
    cd "$INSTALL_DIR"
    pnpm install --prod 2>&1 || die "Failed to install agent dependencies."
    ok "Dependencies installed"
fi

# ==========================================
# Step 6: Configure agent
# ==========================================
info "Step 6/7: Writing configuration..."

if ! $DRY_RUN; then
    PM2_BIN=$(command -v pm2 || true)
    cat > "$INSTALL_DIR/.env" <<ENVEOF
AGENT_API_URL=$AGENT_API_URL
AGENT_SERVER_TOKEN=$SERVER_TOKEN
AGENT_SERVER_ID=$SERVER_ID
COLLECT_INTERVAL_MS=30000
HEARTBEAT_INTERVAL_MS=60000
LOG_BATCH_SIZE=100
MAX_BUFFER_SIZE=1000
PATH=$PATH
PM2_BIN=$PM2_BIN
ENVEOF
    chmod 600 "$INSTALL_DIR/.env"
    ok "Configuration written to $INSTALL_DIR/.env"
else
    echo -e "${YELLOW}[DRY]${NC}  Write .env with AGENT_API_URL=$AGENT_API_URL"
    echo -e "${YELLOW}[DRY]${NC}  AGENT_SERVER_ID=$SERVER_ID"
fi

# ==========================================
# Step 7: Start agent with PM2
# ==========================================
info "Step 7/7: Starting agent..."

if ! $DRY_RUN; then
    pm2 delete "$PM2_NAME" 2>/dev/null || true
    pm2 start "$INSTALL_DIR/dist/index.js" --name "$PM2_NAME" --cwd "$INSTALL_DIR"
    pm2 save

    ok "Agent started as PM2 process '$PM2_NAME'"

    # Verify
    sleep 3
    STATUS=$(pm2 jlist 2>/dev/null | grep -o '"pm2_env":{[^}]*"status":"[^"]*"' | head -1 | grep -o '"status":"[^"]*"' | cut -d'"' -f4 || echo "unknown")

    if [[ "$STATUS" == "online" ]]; then
        ok "Agent is ONLINE"
    else
        warn "Agent status: $STATUS (may need a moment to stabilize)"
    fi

    # Test backend connectivity
    if curl -sf --max-time 5 "$AGENT_API_URL/api/agent/heartbeat" -o /dev/null 2>/dev/null; then
        ok "Backend API reachable at $AGENT_API_URL"
    else
        warn "Backend API at $AGENT_API_URL not reachable (agent may still work if network resolves later)"
    fi

    # Check if PM2 startup is configured
    if ! systemctl list-units --type=service 2>/dev/null | grep -q pm2; then
        echo ""
        warn "PM2 startup is not configured. Run this to enable auto-start on boot:"
        echo -e "  ${BLUE}pm2 startup${NC}"
        echo -e "  ${BLUE}pm2 save${NC}"
    fi
else
    echo -e "${YELLOW}[DRY]${NC}  pm2 delete $PM2_NAME 2>/dev/null || true"
    echo -e "${YELLOW}[DRY]${NC}  pm2 start $INSTALL_DIR/dist/index.js --name $PM2_NAME --cwd $INSTALL_DIR"
    echo -e "${YELLOW}[DRY]${NC}  pm2 save"
fi

echo ""
ok "========================================="
ok " Installation Complete!"
ok "========================================="
echo ""
info "Server ID:   $SERVER_ID"
info "Install Dir: $INSTALL_DIR"
info "PM2 Process: $PM2_NAME"
echo ""
info "Useful commands:"
echo "  pm2 status              # Check agent status"
echo "  pm2 logs $PM2_NAME      # View agent logs"
echo "  pm2 restart $PM2_NAME   # Restart agent"
echo "  $0 --uninstall          # Remove agent"
echo ""

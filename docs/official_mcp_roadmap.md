# GeneXus MCP Nirvana: The Master Plan (v2.0)

> "The bridge between modern AI and legacy power."

## 1. Executive Summary

This document defines the final architectural vision for the **GeneXus Model Context Protocol (MCP) Server**. It represents a shift from experimental Node.js wrappers to a **robust, dual-process .NET architecture** that ensures:

1.  **100% Protocol Compliance:** Using the official Microsoft/Anthropic `.NET 8` SDK.
2.  **Native Execution:** Running directly inside the GeneXus 18 environment (`.NET Framework 4.8`).
3.  **Enterprise Stability:** Isolating the volatile GeneXus runtime from the persistent client connection.

---

## 2. The Architectural Thesis

### The Problem: "Assembly Hell"

GeneXus 18 is a sophisticated but legacy ecosystem built on **.NET Framework 4.8**. It relies on global assembly caches (GAC), 32-bit dependencies, and specific binding redirects.
Modern AI tools (Claude Desktop, Cursor) and the official MCP SDK require **.NET 8.0+**.
Attempting to load GeneXus DLLs directly into a .NET 8 application is technically impossible due to runtime incompatibilities.

### The Solution: The "Gateway-Worker" Pattern

We decouple the _Interface_ from the _Implementation_ using two distinct processes communicating via high-performance Standard I/O (Stdio).

```mermaid
graph TD
    subgraph "External World"
        Client[Claude / Cursor / IDE]
    end

    subgraph "Nirvana Control Plane (.NET 8)"
        Gateway[MCP Gateway]
        Router[Tool Router]
        Security[Security Layer]
    end

    subgraph "GeneXus Runtime Host (.NET 4.8)"
        Worker[GX Worker Process]
        Loader[Assembly Loader]
        BL[Business Logic]
        KB[Knowledge Base]
    end

    Client <-->|"JSON-RPC (Stdio)"| Gateway
    Gateway <==>"JSON-RPC (Stdio/Pipes)"| Worker
    Worker ---|"In-Process Call"| BL
    BL ---|"Read/Write"| KB
```

---

## 3. Component Deep Dive

### 3.1. The Gateway (`GxMcp.Gateway.exe`)

- **Technology:** .NET 8.0 Console Application.
- **Role:** The "Face" of the system.
- **Responsibilities:**
  - **Protocol Handling:** Uses `ModelContextProtocol` NuGet to handle JSON-RPC handshakes, ping, and capabilities negotiation.
  - **Lifecycle Management:** Spawns and monitors the Worker process. Restarts it if GeneXus crashes (a common occurrence).
  - **Startup Validator:**
    - Reads `config.json` for `GeneXusInstallPath`.
    - Checks if `Artech.Genexus.Common.dll` exists at that path.
    - **Dry-Run:** Spawns Worker with `--test-load` flag to verify DLL binding before accepting client connections.
  - **Security:** Validates requests before they reach the core.
  - **Telemetry:** Logs traffic to the Nirvana Dashboard.

### 3.2. The Worker (`GxMcp.Worker.exe`)

- **Technology:** .NET Framework 4.8 Console Application.
- **Location:** **MUST** reside in `C:\Program Files (x86)\GeneXus\GeneXus18\` to resolve dependencies automatically.
- **Role:** The "Muscle".
- **Responsibilities:**
  - **Headless Execution:** Initializes `ServiceFactory` and `GeneXus.Common` without UI.
  - **Diagnostics Mode:** If started with `--test-load`, attempts to load `GeneXus.Common` and report `Success` or `BindingError` (with detailed fusion log) to stdout, then exits.
  - **Context Management:** Opens the `KnowledgeBase` and holds the `Model` in memory.
  * **Execution:** Runs specifically allowed capabilities (Object Listing, Source Retrieval, Build execution).

### 3.3. The Bridge (IPC)

- **Protocol:** Simplified JSON-RPC 2.0.
- **Transport:** Standard Input/Output (stdin/stdout).
  - _Gateway_ writes JSON to _Worker_ stdin.
  - _Worker_ writes JSON response to stdout.
  - _Worker_ writes logs/debug info to stderr (captured by Gateway).

### 3.4. The Dashboard Integration (The "Control Plane")

To ensure the **Dashboard** remains the command center, the **Gateway** will not just be a passive MCP server. It will also host a **Lightweight HTTP API** (ASP.NET Core).

- **Command Flow (Web -> Native):**
  - Dashboard (Next.js) sends `POST http://localhost:5000/api/command` -> Gateway (.NET 8).
  - Gateway forwards command to Worker (via Stdio).
  - Worker executes GeneXus logic.
- **Telemetry Flow (Native -> Web):**
  - Gateway captures Worker output.
  - Gateway writes structured logs directly to **PostgreSQL** (using `Npgsql`) or broadcasts via **Server-Sent Events (SSE)**.
  - Dashboard subscribes to these events for the "Live Terminal".

---

## 4. The Developer Workflow

How we build and debug this system:

### 4.1. Building ("The Dual Build")

We will have a single `.sln` with two projects.

1.  **Build Solution:** Triggers build for Gateway (to `./bin`) and Worker (to `./bin/Worker`).
2.  **Deploy Step (Post-Build):** A script checks if `GxMcp.Worker.exe` has changed. If so, it copies it to the GeneXus installation directory.

### 4.2. Debugging

- **Debug Gateway:** F5 in Visual Studio / VS Code on the .NET 8 project.
- **Debug Worker:**
  1.  Start Gateway.
  2.  Gateway spawns Worker.
  3.  In VS, "Attach to Process" -> Select `GxMcp.Worker.exe`.
  - _Pro Tip:_ We will add a `--debug-wait` flag to the Worker, causing it to pause on startup until a debugger is attached.

---

## 5. Migration Strategy from "Nirvana v1"

We are currently using `nirvana-bridge.js` (Node.js) and `gx_api.ps1` (PowerShell).

### Phase 1: The Gateway Scaffolding

- **Action:** Create the .NET 8 Console App + ASP.NET Core Minimal API.
- **Result:** A running process that can accept HTTP requests and MCP StdIO.

### Phase 2: The Native Worker

- **Action:** Create the .NET 4.8 App in `C:\Program Files (x86)\GeneXus\GeneXus18`.
- **Result:** A headless agent capable of loading `Artech.Genexus.Common.dll`.

### Phase 3: The Switchover

- **Action:** Point the Dashboard to the new .NET 8 API (Port 5000) instead of Node.js (Port 3457).
- **Result:** Full retirement of the legacy Node.js bridge.

---

## 6. Final Infrastructure Specification

### 6.1. Repository Hygiene (Git Strategy)

- **NO DLLs in Git:** We will strictly `.gitignore` all `*.dll`, `*.pdb`, `bin/`, and `obj/`.
- **Reference Policy:** Projects will reference DLLs from `$(GX_PROGRAM_DIR)` (Environment Variable) or a local `local.settings.props`, ensuring the repo remains lightweight (< 10MB).
- **Cleanup Action:** We will purge existing `lib/` folders containing binaries once the .NET solution is active.

### Directory Structure

```text
C:\Projetos\GenexusMCP\
├── .gitignore          # Strict rules
├── GenexusMCP.sln      # Single Solution
├── src\
│   ├── GxMcp.Gateway\  # .NET 8 Source
│   └── GxMcp.Worker\   # .NET 4.8 Source
├── docs\               # Documentation
└── nirvana-bridge.js   # (Legacy - to be removed)
```

### Configuration (`config.json`)

```json
{
  "GeneXus": {
    "InstallationPath": "C:\\Program Files (x86)\\GeneXus\\GeneXus18",
    "WorkerExecutable": "GxMcp.Worker.exe"
  },
  "Server": {
    "HttpPort": 5000,
    "McpStdio": true
  }
}
```

---

_Approved by The Architect, 2024._

# GeneXus MCP Integration: Development Log

This document chronicles the journey of integrating a native GeneXus Model Context Protocol (MCP) server, detailing architectural decisions, successes, and the specific technical challenges encountered with the GeneXus SDK.

## Project Structure Note

_Updated 2026-02-17:_ The project artifacts have been consolidated into `C:\Projetos\GenexusMCP`.

- **Docs:** `C:\Projetos\GenexusMCP\docs`
- **Tools:** `C:\Projetos\GenexusMCP\tools` (Debug scripts like `debug_gx.ps1`, `inspect_gx.ps1`)
- **Source:** `C:\Projetos\GenexusMCP\GeneXusMCPServer`

## Key Architectures Attempted

### 1. The Native C# Executor (Initial Vision)

**Goal:** Run a standalone `.exe` that loads GeneXus assemblies directly and exposes MCP tools via Stdio.
**Outcome:** Partial Success / Major Runtime Failure.

- **Success:** Created a lightweight JSON-RPC server without external dependencies using `TcpListener` (later Stdio).
- **Failure:** The GeneXus SDK (`Artech.Architecture.Common`, `Artech.MsBuild.Common`, etc.) relies heavily on global state, specific configuration files (`MsBuild.exe.config`), and identifying the execution context. Running it outside of `GeneXus.exe` or `MSBuild.exe` provoked relentless `TypeLoadException` and `FileNotFoundException` due to complex binding redirects not present in a standard .NET app.

### 2. The MSBuild Task Bridge (Current Approach)

**Goal:** Use the native executable only as a proxy to launch `MSBuild.exe`, which is a supported environment for GeneXus operations.
**Outcome:** Functional but complex to debug.

- **Success:** Successfully launched MSBuild processes and captured output.
- **Failure:**
  - **Task Resolution:** Standard `GetKBObjects` tasks were missing or not loadable due to `MSB4062` (Assembly loading context issues).
  - **Inline Tasks:** Writing C# code inside `.targets` files (`CodeTaskFactory`) faced `CS0117` errors because the API surface (Properties vs Fields) of `BLServices` and `KBObject` was not fully known or accessible in that context.

## Successes

1. **Protocol Implementation:**
   - Successfully implemented the MCP JSON-RPC protocol (Client <-> Server) over Standard Input/Output.
   - The server correctly parses messages, handles handshakes, and routes tool calls.

2. **Fusion Strategy (Hybrid Execution):**
   - We successfully identified that the _safest_ way to interact with GeneXus is via its own tooling (MSBuild extensions), rather than trying to reverse-engineer the entire runtime environment in a custom EXE.

3. **Antigravity Integration:**
   - The server is correctly registered in `antigravity_mcp_config.json` and recognized by the IDE.

4. **Semantic Intelligence Engine (v18.7):**
   - **Hybrid Analysis**: Successfully integrated SDK native references with Regex fallbacks in `AnalyzeService`.
   - **Graph Ranking**: Implemented Hub/Authority scores in `SearchService` to surface critical KB objects.
   - **Business Mapping**: Automated domain discovery based on DB relations and naming.
   - **Live Indexing**: Achieved real-time search sync across all writing tools (`Write`, `Forge`, `Batch`).

## Failures & Blockers

1. **"Assembly Hell" (Dependency Resolution):**
   - GeneXus assemblies are not designed to be loaded by arbitrary applications. They require the `AppBase` to be the GeneXus installation directory.
   - Even with `AssemblyResolve` events hooked, internal dependencies (like `Artech.Common.Properties`) often fail to load because the hosting process doesn't have the correct binding redirects configuration.

2. **API Opacity:**
   - The GeneXus SDK (`BLServices`, `UIServices`) is not documented for external use. Trial and error with Reflection (`inspect_gx.ps1`) revealed that many expected properties (like `KB.CurrentModel`) are either internal, protected, or require specific initialization sequences that are hidden inside the main GeneXus executable.

3. **Runtime Environment:**
   - The SDK expects to find configuration files (`client.exe.config`, `MsBuild.exe.config`) effectively "next to" the executing process. Running from `C:\sistemas\GeneXusMCP` while referencing DLLs in `C:\Program Files...` creates a split-brain scenario where config files are not found.

## Conclusion

The "bridgeless" native dream is possible but requires replicating the **exact** `App.config` and runtime environment of `GeneXus.exe`. The current "Bridge" approach (calling MSBuild or PowerShell) is a pragmatic compromise that trades performance (process startup cost) for stability (supported execution environment).

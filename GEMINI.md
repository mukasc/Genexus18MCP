# Protocolo GeneXus MCP Nirvana (Sentient Edition v18.6)

> [!IMPORTANT]
> **Skills Required**: Before performing ANY task in this repo, the agent MUST load and follow:
>
> 1. `[GeneXus MCP Mastery](file:///.gemini/skills/genexus-mastery/SKILL.md)` - For tool performance and cache usage.
> 2. `[GeneXus 18 Guidelines](file:///.gemini/skills/genexus18-guidelines/SKILL.md)` - For official GeneXus development rules.

## 🏗️ Architecture: Native SDK Dual-Process

1.  **Gateway (.NET 8)**: `GxMcp.Gateway.exe`. Handles MCP protocol, Stdio, and process orchestration.
2.  **Worker (.NET 4.8 x86)**: `GxMcp.Worker.exe`. Loads GeneXus SDK DLLs natively for high-performance KB access.

## 🛠️ Tool Usage Guide (SDK Optimized)

### 1. `genexus_list_objects`
**Purpose**: Direct KB discovery via SDK.
- **Params**: `filter` (comma-separated types), `limit` (max 50), `offset`.
- **Note**: Now returns objects from memory, much faster than previous MSBuild versions.

### 2. `genexus_read_object`
**Purpose**: Deep analysis of object structure.
- **Params**: `name` (e.g., `Trn:Customer`).
- **Output**: Full object XML structure including GUIDs for all parts (Source, Rules, etc.).

### 3. `genexus_write_object`
**Purpose**: Instant native writing to KB objects.
- **Params**: `name`, `part` (Source, Rules, Events), `code`.
- **Note**: Uses direct SDK `Save()`, bypassing MSBuild/XPZ overhead.

### 4. `genexus_analyze`
**Purpose**: Static analysis and linting.
- **Output**: JSON with `complexity`, `calls`, `tables`, and semantic `insights`.

### 5. `genexus_doctor`
**Purpose**: Diagnosing build failures.
- **Tip**: Run immediately after a failed `genexus_build`.

### 6. `genexus_refactor`
**Purpose**: Native cleanup.
- **Action**: "CleanVars" (removes unused variables verified by SDK).

### 7. `genexus_batch`
**Purpose**: Atomic multi-object operations.
- **Tip**: Use `Add` to buffer and `Commit` to save all changes in one transaction.

## 🧠 Best Practices (v18.6)

- **Bitness Awareness**: The Worker MUST run as x86 to interact with GeneXus DLLs.
- **Instant Feedback**: Tools like `read` and `write` are now near-instant (<1s) due to the persistent SDK instance in `KbService`.
- **GUID Precision**: Use GUIDs returned by `read_object` when manipulating internal parts via custom logic.
- **Verify**: After `write_object`, use `read_object` to verify the change was persisted correctly in the KB.

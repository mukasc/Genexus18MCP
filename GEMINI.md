# Protocolo GeneXus MCP Nirvana (Sentient Edition v18.7)

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

**Purpose**: Direct KB discovery via SDK. Returns objects from memory (fast).

- **Params**: `filter` (comma-separated types), `limit` (max 50), `offset`.

### 2. `genexus_read_object`

**Purpose**: Deep analysis of object structure and XML retrieval.

- **Params**: `name` (e.g., `Trn:Customer`).

### 3. `genexus_write_object`

**Purpose**: Instant native writing to KB objects. Triggers **Live Indexing**.

- **Params**: `name`, `part` (Source, Rules, Events), `code`.

### 4. `genexus_analyze` (Semantic Intelligence)

**Purpose**: Deep static analysis, BI extraction, and Linter.

- **Output**:
  - `calls` & `tables`: Hybrid dependency graph (SDK + Regex).
  - `rules`: Business rule extraction (Validation, Persistence, UI).
  - `domain`: Automated business domain mapping (Financeiro, Protocolo, etc.).
  - `insights`: Proactive Linter (N+1 queries, unused vars, empty loops).
  - `complexity`: Structural complexity score.

### 5. `genexus_search` (Semantic Engine)

**Purpose**: Search with context awareness and graph-based ranking.

- **Features**:
  - **Synonym Expansion**: Matches "acad" with "student/aluno".
  - **Graph Ranking**: Prioritizes results by "Authority" (CalledBy) and "Hubiness" (Calls).
  - **Snippet Scoring**: Analyzes source code relevancy.

### 6. `genexus_batch`

**Purpose**: Atomic multi-object operations. Triggers **Live Indexing** for all committed objects.

### 7. `genexus_visualize`

**Purpose**: Generates an interactive HTML graph of the Knowledge Base dependencies.

- **Params**: `domain` (Optional: Filter by business domain like 'Financeiro').
- **Output**: Returns the file path to the generated HTML visualizer.

## 🧠 Intelligence & Best Practices (v18.7)

- **Live Indexing**: Any write (`write`, `forge`, `batch`) triggers immediate re-analysis. Search results are always up-to-date.
- **Business Domains**: The system automatically groups objects into domains based on naming conventions and database relations.
- **Bitness Awareness**: The Worker MUST run as x86 to interact with GeneXus DLLs.
- **Transaction-Table Collision**: Always use type prefixes (e.g., `Trn:Customer`) to ensure correct targeting.
- **Cache Invalidation**: After ANY native SDK write, call `_objectService.Invalidate(name)` to ensure subsequent reads see the new structure.
- **Wiki Persistence**: Writing to `Documentation` requires a `WikiPage` with mandatory metadata (`Name` = `Type.Name`, and `Module`).

For deeper technical details, consult `[docs/native_sdk_insights.md](file:///c:/Projetos/GenexusMCP/docs/native_sdk_insights.md)`.

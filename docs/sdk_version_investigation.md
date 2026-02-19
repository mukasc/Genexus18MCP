# Investigation Plan: SDK Version Mismatch (11.0.0.0 vs 18.x)

## Summary

When the MCP Worker opens a GeneXus Knowledge Base, it appears to identify itself as version **11.0.0.0** in the KB's internal metadata. This causes the GeneXus IDE to show a warning about a "different installation" when the user re-opens the KB in the GUI.

## Hypothesis

The version "11.0.0.0" is likely a hardcoded default in the `Artech.Core.Connector` or `Artech.Architecture.Common` assemblies when they are initialized without a specific context or calling application host information.

## Investigation Backlog

### 🛠️ Short-term: Worker Metadata

- **Assembly Information**: Ensure `GxMcp.Worker` has assembly attributes that match a "GeneXus-like" process.
- **Initialization Overloads**: Explore `Connector.Initialize` overloads or related classes (`ArtechServices`, `ContextService`) for version-specifying methods.

### 🔍 Medium-term: SDK Comparison

- **GeneXus.MSBuild.Tasks**: Analyze [GeneXus.MSBuild.Tasks.dll](<file:///C:/Program_Files_(x86)/GeneXus/GeneXus18/GeneXus.MSBuild.Tasks.dll>) to see how it bootstraps without triggering this warning.
- **Log Comparison**: Compare the `KnowledgeBase.Open` logs between the IDE and the Worker to see where divergence occurs.

### 🚀 Long-term: Stability

- **IDE Compatibility**: Ensure the MCP Server is "transparent" to the IDE, allowing seamless coexistence without metadata corruption or annoying warnings.

---

_Created on: 2026-02-19_
_Related to: GeneXus SDK Initialization Phase_

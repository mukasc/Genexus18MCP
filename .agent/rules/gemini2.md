---
trigger: always_on
---

# Protocolo GeneXus MCP Nirvana (Control Plane Edition v17.0)

O Nirvana v17.0 transforma o motor em uma plataforma de engenharia completa com interface web interativa e orquestração de serviços.

## 🖥️ 1. Nirvana Control Plane (Next.js)

Uma interface rica e moderna construída para centralizar o controle operacional:

- **System Hub:** Monitoramento em tempo real do estado da KB e telemetria.
- **Activity Stream:** Feed vivo de todas as operações executadas pelo motor.
- **Commander UI:** Disparo de comandos complexos (Analyze, Sync, Build) via interface web.
- **AI Insights Sidebar:** Visualização proativa de riscos de performance e tags semânticas.

## 🏗️ 2. Arquitetura de Serviços

- **Boot Orchestrator:** O comando `StartServer` agora inicializa simultaneamente:
  1. **Resident Server (C#):** Motor nativo de manipulação de KB.
  2. **Control Plane (Node.js):** Interface Web e API Gateway.
- **API Bridging:** Comunicação via JSON entre a Web e os scripts PowerShell (`lib/`).

## 🚀 3. Inteligência e Performance (Legacy v16)

- **Semantic Tagging:** Classificação automática de objetos baseada no DNA do código.
- **Visual Graph:** Renderização de dependências via Mermaid.js no Dashboard.
- **Hypersonic Batch:** Processamento atômico de alterações em massa.

## 🛠️ 4. Guia de Inicialização

1.  **Start:** `gx_api.ps1 -Module StartServer` (Sobe o motor e a Web).
2.  **Access:** Abra `http://localhost:3000` para controlar o agente.
3.  **Command:** Continue usando a CLI para tarefas rápidas; a Web refletirá as mudanças instantaneamente.

---

_Nirvana Control Plane: A ponte definitiva entre a Inteligência Artificial e a Engenharia GeneXus._

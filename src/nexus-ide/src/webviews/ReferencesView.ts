import * as vscode from "vscode";
import { GxFileSystemProvider } from "../gxFileSystem";
import { GxTreeItem } from "../gxTreeProvider";
import { GX_SCHEME } from "../constants";
import { GxUriParser } from "../utils/GxUriParser";

export class ReferencesView {
  private static panels = new Map<string, vscode.WebviewPanel>();

  static async show(item: GxTreeItem | vscode.Uri | undefined, provider: GxFileSystemProvider) {
    let objName = "";
    let objType = "";
    let targetUri: vscode.Uri | undefined;

    if (item instanceof vscode.Uri) {
      targetUri = item;
    } else if (item && item.resourceUri) {
      targetUri = item.resourceUri;
    } else {
      targetUri = GxUriParser.getActiveGxUri() || undefined;
    }

    if (targetUri && targetUri.scheme === GX_SCHEME) {
      const info = GxUriParser.parse(targetUri);
      if (info) {
        objName = info.name;
        objType = info.type;
      }
    }

    if (!objName) {
      vscode.window.showErrorMessage("Selecione um objeto para buscar referências.");
      return;
    }

    const panelKey = objType ? `${objType}:${objName}` : objName;

    if (this.panels.has(panelKey)) {
      this.panels.get(panelKey)!.reveal(vscode.ViewColumn.Beside);
      return;
    }

    const panel = vscode.window.createWebviewPanel(
      "gxReferences",
      `${objName} References`,
      vscode.ViewColumn.Beside,
      { enableScripts: true }
    );

    this.panels.set(panelKey, panel);
    panel.onDidDispose(() => this.panels.delete(panelKey));

    panel.webview.html = `<h1>Buscando referências para ${objName}...</h1>`;

    try {
      const result = await provider.callGateway({
        method: "execute_command",
        params: {
          module: "Analyze",
          action: "GetHierarchy",
          target: panelKey,
        },
      });

      if (result && (result.calls || result.calledBy)) {
        panel.webview.html = this.getHtmlContent(objName, result);
        
        panel.webview.onDidReceiveMessage(
          async (message: any) => {
            switch (message.command) {
              case 'openObject':
                const uri = vscode.Uri.parse(`${GX_SCHEME}:/${message.type}/${message.name}.gx`);
                await vscode.commands.executeCommand('vscode.open', uri);
                return;
            }
          },
          undefined
        );

      } else {
        const errorMsg = result?.error ? `<p style="color:red">Erro: ${result.error}</p>` : "";
        panel.webview.html = `<h1>Não foram encontradas referências para ${objName}.</h1>${errorMsg}`;
      }
    } catch (e) {
      panel.webview.html = `<h1>Erro: ${e}</h1>`;
    }
  }

  private static getHtmlContent(objName: string, data: any): string {
    const renderList = (items: any[]) => {
      if (!items || items.length === 0) return '<li>Nenhuma referência encontrada.</li>';
      return items.map(item => `
        <li class="ref-item" onclick="openObject('${item.name}', '${item.type}')">
          <span class="type-badge">${item.type}</span>
          <span class="obj-name">${item.name}</span>
          ${item.description ? `<div class="obj-desc">${item.description}</div>` : ''}
        </li>
      `).join('');
    };

    return `
      <!DOCTYPE html>
      <html>
      <head>
          <style>
              body { font-family: sans-serif; padding: 20px; color: var(--vscode-foreground); background-color: var(--vscode-editor-background); }
              h1 { border-bottom: 1px solid var(--vscode-widget-border); padding-bottom: 10px; }
              .container { display: flex; gap: 40px; }
              .column { flex: 1; }
              ul { list-style: none; padding: 0; }
              .ref-item { 
                padding: 10px; 
                margin-bottom: 8px; 
                border: 1px solid var(--vscode-widget-border); 
                border-radius: 4px; 
                cursor: pointer; 
                transition: background-color 0.2s;
              }
              .ref-item:hover { background-color: var(--vscode-list-hoverBackground); }
              .type-badge { 
                font-size: 0.8em; 
                background: var(--vscode-badge-background); 
                color: var(--vscode-badge-foreground); 
                padding: 2px 6px; 
                border-radius: 10px; 
                margin-right: 8px; 
                text-transform: uppercase;
              }
              .obj-name { font-weight: bold; }
              .obj-desc { font-size: 0.9em; opacity: 0.8; margin-top: 4px; }
              h2 { color: var(--vscode-symbolIcon-classForeground); }
          </style>
          <script>
            const vscode = acquireVsCodeApi();
            function openObject(name, type) {
              vscode.postMessage({
                command: 'openObject',
                name: name,
                type: type
              });
            }
          </script>
      </head>
      <body>
          <h1>Referências para: ${objName}</h1>
          <div class="container">
              <div class="column">
                  <h2>Called By (Entrada)</h2>
                  <ul>${renderList(data.calledBy)}</ul>
              </div>
              <div class="column">
                  <h2>Calls (Saída)</h2>
                  <ul>${renderList(data.calls)}</ul>
              </div>
          </div>
      </body>
      </html>
    `;
  }
}

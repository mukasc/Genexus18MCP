import * as vscode from "vscode";
import { GxFileSystemProvider } from "../gxFileSystem";
import { GX_SCHEME } from "../constants";
import { GxUriParser } from "../utils/GxUriParser";

export class SearchView {
  private static panel: vscode.WebviewPanel | undefined;

  static async show(provider: GxFileSystemProvider) {
    if (this.panel) {
      this.panel.reveal(vscode.ViewColumn.Active);
      return;
    }

    this.panel = vscode.window.createWebviewPanel(
      "gxSearch",
      "GeneXus Advanced Search",
      vscode.ViewColumn.Active,
      { enableScripts: true, retainContextWhenHidden: true }
    );

    this.panel.onDidDispose(() => (this.panel = undefined));

    this.panel.webview.html = this.getHtmlContent();

    this.panel.webview.onDidReceiveMessage(
      async (message: any) => {
        switch (message.command) {
          case "search":
            const results = await this.performSearch(provider, message.query, message.filters);
            this.panel?.webview.postMessage({ command: "results", results });
            return;
          case "openObject":
            const uri = vscode.Uri.parse(`${GX_SCHEME}:/${message.type}/${message.name}.gx`);
            await vscode.commands.executeCommand("vscode.open", uri);
            return;
        }
      },
      undefined
    );
  }

  private static async performSearch(provider: GxFileSystemProvider, query: string, filters: any) {
    let finalQuery = query;
    if (filters.type) finalQuery += ` type:${filters.type}`;
    if (filters.after) finalQuery += ` after:${filters.after}`;
    if (filters.before) finalQuery += ` before:${filters.before}`;

    try {
      const result = await provider.callGateway({
        module: "Search",
        action: "Query",
        target: finalQuery,
        limit: 100
      });
      return result.results || result.Results || [];
    } catch (e) {
      console.error("Search failed:", e);
      return { error: String(e) };
    }
  }

  private static getHtmlContent(): string {
    const html = [
      '<!DOCTYPE html>',
      '<html>',
      '<head>',
      '    <style>',
      '        body { font-family: "Segoe UI", sans-serif; padding: 20px; color: var(--vscode-foreground); background-color: var(--vscode-editor-background); }',
      '        .search-container { margin-bottom: 20px; display: flex; flex-direction: column; gap: 10px; border-bottom: 1px solid var(--vscode-widget-border); padding-bottom: 20px; }',
      '        .input-group { display: flex; gap: 10px; align-items: center; }',
      '        input, select { background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border); padding: 5px 10px; border-radius: 2px; }',
      '        input[type="text"] { flex: 1; height: 25px; }',
      '        button { background: var(--vscode-button-background); color: var(--vscode-button-foreground); border: none; padding: 6px 15px; cursor: pointer; border-radius: 2px; }',
      '        button:hover { background: var(--vscode-button-hoverBackground); }',
      '        .filters { display: flex; gap: 20px; font-size: 0.9em; flex-wrap: wrap; }',
      '        .filter-item { display: flex; align-items: center; gap: 8px; }',
      '        #results { margin-top: 20px; }',
      '        .result-item { padding: 10px; border: 1px solid var(--vscode-widget-border); margin-bottom: 10px; border-radius: 4px; cursor: pointer; }',
      '        .result-item:hover { background: var(--vscode-list-hoverBackground); }',
      '        .res-header { display: flex; justify-content: space-between; margin-bottom: 5px; }',
      '        .res-name { font-weight: bold; color: var(--vscode-textLink-foreground); }',
      '        .res-type { font-size: 0.8em; opacity: 0.7; background: var(--vscode-badge-background); color: var(--vscode-badge-foreground); padding: 1px 5px; border-radius: 10px; }',
      '        .res-desc { font-size: 0.9em; opacity: 0.8; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }',
      '        .res-path { font-size: 0.8em; opacity: 0.5; margin-top: 4px; }',
      '    </style>',
      '</head>',
      '<body>',
      '    <h2>GeneXus Advanced Search</h2>',
      '    <div class="search-container">',
      '        <div class="input-group">',
      '            <input type="text" id="searchInput" placeholder="Search name, description, code..." onkeyup="if(event.key===\'Enter\') doSearch()" />',
      '            <button onclick="doSearch()">Search</button>',
      '        </div>',
      '        <div class="filters">',
      '            <div class="filter-item">',
      '                <label>Type:</label>',
      '                <select id="typeFilter">',
      '                    <option value="">All</option>',
      '                    <option value="Procedure">Procedure</option>',
      '                    <option value="Transaction">Transaction</option>',
      '                    <option value="WebPanel">Web Panel</option>',
      '                    <option value="SDT">SDT</option>',
      '                    <option value="DataProvider">Data Provider</option>',
      '                    <option value="Table">Table</option>',
      '                </select>',
      '            </div>',
      '            <div class="filter-item">',
      '                <label>After:</label>',
      '                <input type="date" id="afterDate" />',
      '            </div>',
      '            <div class="filter-item">',
      '                <label>Before:</label>',
      '                <input type="date" id="beforeDate" />',
      '            </div>',
      '        </div>',
      '    </div>',
      '    <div id="results"></div>',
      '',
      '    <script>',
      '      const vscode = acquireVsCodeApi();',
      '      ',
      '      function doSearch() {',
      '        const query = document.getElementById("searchInput").value;',
      '        const filters = {',
      '          type: document.getElementById("typeFilter").value,',
      '          after: document.getElementById("afterDate").value,',
      '          before: document.getElementById("beforeDate").value',
      '        };',
      '        document.getElementById("results").innerHTML = "<p>Searching...</p>";',
      '        vscode.postMessage({ command: "search", query, filters });',
      '      }',
      '',
      '      window.addEventListener("message", event => {',
      '        const message = event.data;',
      '        if (message.command === "results") {',
      '          const results = message.results || message.Results;',
      '          renderResults(results);',
      '        }',
      '      });',
      '',
      '      function renderResults(results) {',
      '        const container = document.getElementById("results");',
      '        if (!results || results.length === 0) {',
      '          container.innerHTML = "<p>No objects found matching the criteria.</p>";',
      '          return;',
      '        }',
      '        if (results.error) {',
      '          container.innerHTML = "<p style=\'color:red\'>Error: " + results.error + "</p>";',
      '          return;',
      '        }',
      '',
      '        container.innerHTML = results.map(r => {',
      '          return "<div class=\'result-item\' onclick=\'openObject(\\"" + r.name + "\\", \\"" + r.type + "\\")\'>" +',
      '            "<div class=\'res-header\'>" +',
      '              "<span class=\'res-name\'>" + r.name + "</span>" +',
      '              "<span class=\'res-type\'>" + r.type + "</span>" +',
      '            "</div>" +',
      '            "<div class=\'res-desc\'>" + (r.description || "") + "</div>" +',
      '            "<div class=\'res-path\'>" + (r.parent || "Root") + "</div>" +',
      '          "</div>";',
      '        }).join("");',
      '      }',
      '',
      '      function openObject(name, type) {',
      '        vscode.postMessage({ command: "openObject", name, type });',
      '      }',
      '    </script>',
      '</body>',
      '</html>'
    ];
    return html.join('\n');
  }
}

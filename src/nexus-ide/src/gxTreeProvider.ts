import * as vscode from "vscode";
import * as fs from "fs";
import * as path from "path";
import { GxUriParser } from "./utils/GxUriParser";

const TYPE_ICON_FILE: Record<string, string> = {
  Module: "module",
  Folder: "folder",
  Procedure: "procedure",
  WebPanel: "webpanel",
  Transaction: "transaction",
  SDT: "sdt",
  StructuredDataType: "sdt",
  DataProvider: "dataprovider",
  DataView: "dataview",
  Attribute: "attribute",
  Table: "table",
  SDPanel: "sdpanel",
};

function getTypeFromResource(uri: vscode.Uri): string {
  if (uri.scheme === "file" && fs.existsSync(uri.fsPath) && fs.statSync(uri.fsPath).isDirectory()) {
    return "Folder";
  }

  const info = GxUriParser.parse(uri);
  return info?.type || "Object";
}

export class GxTreeItem extends vscode.TreeItem {
  constructor(
    public readonly resourceUri: vscode.Uri,
    collapsibleState: vscode.TreeItemCollapsibleState,
    private readonly extensionUri: vscode.Uri,
  ) {
    super(resourceUri, collapsibleState);

    const gxType = getTypeFromResource(resourceUri);
    const isContainer = collapsibleState !== vscode.TreeItemCollapsibleState.None;

    this.tooltip = `[${gxType}] ${path.basename(resourceUri.fsPath || resourceUri.path)}`;
    this.contextValue = `gx_${gxType.toLowerCase()}`;

    const iconFile = TYPE_ICON_FILE[gxType];
    if (iconFile) {
      const iconUri = vscode.Uri.joinPath(
        extensionUri,
        "resources",
        `${iconFile}.svg`,
      );
      this.iconPath = { light: iconUri, dark: iconUri };
    }

    if (!isContainer) {
      this.command = {
        command: "vscode.open",
        title: "Open",
        arguments: [this.resourceUri],
      };
    }
  }

  get gxName(): string {
    const parsed = GxUriParser.parse(this.resourceUri);
    return parsed?.name || path.basename(this.resourceUri.fsPath || this.resourceUri.path);
  }

  get gxType(): string {
    return getTypeFromResource(this.resourceUri);
  }

  get gxParentPath(): string {
    return path.dirname(this.resourceUri.fsPath || this.resourceUri.path);
  }
}

export class GxTreeProvider implements vscode.TreeDataProvider<GxTreeItem> {
  private _onDidChangeTreeData = new vscode.EventEmitter<
    GxTreeItem | undefined | null | void
  >();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
  private _cache = new Map<string, { items: GxTreeItem[]; time: number }>();

  constructor(
    private readonly shadowRoot: string,
    private readonly extensionUri: vscode.Uri,
  ) {}

  refresh(): void {
    this._cache.clear();
    this._onDidChangeTreeData.fire();
  }

  refreshNode(item: GxTreeItem): void {
    this._cache.delete(item.resourceUri.fsPath.toLowerCase());
    this._onDidChangeTreeData.fire(item);
  }

  getTreeItem(element: GxTreeItem): vscode.TreeItem {
    return element;
  }

  async getChildren(element?: GxTreeItem): Promise<GxTreeItem[]> {
    const targetDir = element ? element.resourceUri.fsPath : this.shadowRoot;
    const cacheKey = targetDir.toLowerCase();
    const cached = this._cache.get(cacheKey);
<<<<<<< HEAD
    // PERFORMANCE: Reduced cache time to 15 seconds for better sync
    if (cached && Date.now() - cached.time < 15000) return cached.items;
=======
    if (cached && Date.now() - cached.time < 300000) {
      return cached.items;
    }
>>>>>>> upstream/main

    if (!fs.existsSync(targetDir) || !fs.statSync(targetDir).isDirectory()) {
      return [];
    }

<<<<<<< HEAD
      console.log(
        `[GxTree] Result for "${parentName}": ${(result?.results || result?.Results)?.length || 0} objects`,
      );

      const objects: GxObject[] =
        result.results || result.Results || (Array.isArray(result) ? result : []);

      // Sort: Module (0) → Folder (1) → Files (2), alphabetical within each group
      objects.sort((a, b) => {
        const oa = TYPE_ORDER[a.type] ?? FILE_ORDER;
        const ob = TYPE_ORDER[b.type] ?? FILE_ORDER;
        if (oa !== ob) return oa - ob;
=======
    const items = fs
      .readdirSync(targetDir, { withFileTypes: true })
      .filter((entry) =>
        entry.name !== ".gx_index.json" &&
        entry.name !== ".mcp_config.json",
      )
      .sort((a, b) => {
        if (a.isDirectory() !== b.isDirectory()) {
          return a.isDirectory() ? -1 : 1;
        }
>>>>>>> upstream/main
        return a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
      })
      .map((entry) => {
        const itemUri = vscode.Uri.file(path.join(targetDir, entry.name));
        return new GxTreeItem(
          itemUri,
          entry.isDirectory()
            ? vscode.TreeItemCollapsibleState.Collapsed
            : vscode.TreeItemCollapsibleState.None,
          this.extensionUri,
        );
      });

<<<<<<< HEAD
      this._cache.set(cacheKey, { items, time: Date.now() });

      // --- ELITE BACKGROUND PRE-FETCH ---
      // If we just loaded the root, pre-fetch more folders to ensure instant navigation
      if (parentName === "") {
        const containers = items.filter(
          (i) => i.gxType === "Folder" || i.gxType === "Module",
        );
        // Pre-fetch first 10 containers sequentially in background to not choke the gateway
        (async () => {
          for (const folder of containers.slice(0, 10)) {
            try {
              // Only 1 level deep for auto-prefetch
              const result = await this.callGateway({
                module: "Search",
                action: "Query",
                target: `parent:"${folder.gxName}"`,
                limit: 50,
              });
              // Store in cache directly without full getChildren recursion
              const subObjects: GxObject[] =
                result.results || result.Results || (Array.isArray(result) ? result : []);
              if (subObjects.length > 0) {
                const subItems = subObjects.map((obj) => {
                  const isSubContainer =
                    obj.type === "Module" || obj.type === "Folder";
                  return new GxTreeItem(
                    obj.name,
                    obj.type,
                    (folder.gxParentPath ? folder.gxParentPath + "/" : "") +
                      folder.gxName,
                    isSubContainer
                      ? vscode.TreeItemCollapsibleState.Collapsed
                      : vscode.TreeItemCollapsibleState.None,
                    this.extensionUri,
                  );
                });
                this._cache.set(folder.gxName, {
                  items: subItems,
                  time: Date.now(),
                });
              }
            } catch {}
            await new Promise((r) => setTimeout(r, 200));
          }
        })();
      }

      return items;
    } catch (e) {
      console.error(`[Nexus IDE] TreeProvider error for ${parentName}:`, e);
      return [];
    }
=======
    this._cache.set(cacheKey, { items, time: Date.now() });
    return items;
>>>>>>> upstream/main
  }
}

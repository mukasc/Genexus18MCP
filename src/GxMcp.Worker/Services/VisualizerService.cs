using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GxMcp.Worker.Models;
using Newtonsoft.Json;

namespace GxMcp.Worker.Services
{
    public class VisualizerService
    {
        private readonly string _indexPath;
        private readonly string _outputDir;

        public VisualizerService()
        {
            _indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "search_index.json");
            _outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html");
        }

        public string GenerateGraph(string filterDomain = null)
        {
            try
            {
                if (!File.Exists(_indexPath))
                    return "{\"error\": \"Search Index not found. Run analyze first.\"}";

                var index = SearchIndex.FromJson(File.ReadAllText(_indexPath));
                if (index == null || index.Objects.Count == 0)
                    return "{\"error\": \"Search Index is empty.\"}";

                var nodes = new List<object>();
                var edges = new List<object>();
                var addedNodes = new HashSet<string>();

                IEnumerable<SearchIndex.IndexEntry> sourceObjects = index.Objects.Values;

                // 1. Apply Domain Filter
                if (!string.IsNullOrEmpty(filterDomain) && filterDomain != "All")
                {
                    sourceObjects = sourceObjects.Where(e => string.Equals(e.BusinessDomain, filterDomain, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // 2. Safety Limit: If no specific filter, take top 1000 by Authority (CalledBy count)
                    // This prevents browser hanging with 37k+ nodes
                    sourceObjects = sourceObjects
                        .OrderByDescending(e => (e.CalledBy?.Count ?? 0))
                        .Take(1000);
                }

                var relevantObjects = sourceObjects.ToList();
                var relevantNames = new HashSet<string>(relevantObjects.Select(o => o.Name), StringComparer.OrdinalIgnoreCase);

                // Build Graph Data
                foreach (var entry in relevantObjects)
                {
                    // Node
                    if (!addedNodes.Contains(entry.Name))
                    {
                        nodes.Add(new
                        {
                            data = new
                            {
                                id = entry.Name,
                                label = entry.Name.Contains(":") ? entry.Name.Split(':')[1] : entry.Name,
                                type = entry.Type ?? "Other",
                                domain = entry.BusinessDomain ?? "Unknown",
                                size = 20 + ((entry.CalledBy?.Count ?? 0) * 2)
                            }
                        });
                        addedNodes.Add(entry.Name);
                    }

                    // Edges (Outgoing calls) - Use HashSet for O(1) lookup
                    foreach (var target in entry.Calls)
                    {
                        if (relevantNames.Contains(target))
                        {
                             edges.Add(new 
                             { 
                                 data = new 
                                 { 
                                     source = entry.Name, 
                                     target = target,
                                     id = entry.Name + "->" + target
                                 } 
                             });
                        }
                    }
                }

                var graphData = new { nodes, edges };
                string jsonGraph = JsonConvert.SerializeObject(graphData);

                // Generate HTML
                string html = GetHtmlTemplate(jsonGraph);
                
                if (!Directory.Exists(_outputDir)) Directory.CreateDirectory(_outputDir);
                string filePath = Path.Combine(_outputDir, "graph.html");
                File.WriteAllText(filePath, html);

                return "{\"status\": \"Success\", \"url\": \"" + filePath.Replace("\\", "/") + "\", \"nodes\": " + nodes.Count + ", \"edges\": " + edges.Count + "}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + CommandDispatcher.EscapeJsonString(ex.Message) + "\"}";
            }
        }

        private string GetHtmlTemplate(string jsonData)
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <title>GeneXus KB Visualizer</title>
    <script src='https://cdnjs.cloudflare.com/ajax/libs/cytoscape/3.28.1/cytoscape.min.js'></script>
    <style>
        body { font-family: 'Segoe UI', sans-serif; margin: 0; padding: 0; display: flex; height: 100vh; overflow: hidden; }
        #sidebar { width: 300px; background: #f4f4f4; border-right: 1px solid #ccc; padding: 20px; box-shadow: 2px 0 5px rgba(0,0,0,0.1); z-index: 10; display: flex; flex-direction: column; }
        #cy { flex-grow: 1; background: #fff; }
        h2 { margin-top: 0; color: #333; }
        .stat { margin-bottom: 10px; font-size: 0.9em; color: #666; }
        #details { margin-top: 20px; padding-top: 20px; border-top: 1px solid #ddd; flex-grow: 1; overflow-y: auto; }
        .tag { display: inline-block; padding: 2px 6px; background: #e0e0e0; border-radius: 4px; font-size: 0.8em; margin-right: 5px; margin-bottom: 5px; }
        .legend { margin-top: auto; padding-top: 10px; font-size: 0.85em; }
        .legend-item { display: flex; align-items: center; margin-bottom: 4px; }
        .dot { width: 12px; height: 12px; border-radius: 50%; margin-right: 8px; }
        
        .type-Trn { background-color: #0074D9; }
        .type-Prc { background-color: #2ECC40; }
        .type-Wbp { background-color: #FF851B; }
        .type-Tbl { background-color: #B10DC9; }
        .type-Folder { background-color: #FFDC00; }
        .type-KBCategory { background-color: #F012BE; }

        .prop-label { font-weight: bold; color: #555; font-size: 0.8em; margin-top: 8px; }
        .prop-val { font-size: 0.9em; word-break: break-all; }
        .semitransp { opacity: 0.1; }
        .highlight { border-width: 3px; border-color: #000; }
    </style>
</head>
<body>
    <div id='sidebar'>
        <h2>KB Visualizer</h2>
        <div class='stat'>Nodes: <span id='nodeCount'>0</span></div>
        <div class='stat'>Edges: <span id='edgeCount'>0</span></div>
        
        <div id='details'>
            <p>Select a node to see dependencies.</p>
        </div>

        <div class='legend'>
            <div class='legend-item'><div class='dot' style='background:#0074D9'></div>Transaction</div>
            <div class='legend-item'><div class='dot' style='background:#2ECC40'></div>Procedure</div>
            <div class='legend-item'><div class='dot' style='background:#FF851B'></div>WebPanel</div>
            <div class='legend-item'><div class='dot' style='background:#B10DC9'></div>Table</div>
            <div class='legend-item'><div class='dot' style='background:#FFDC00'></div>Folder</div>
            <div class='legend-item'><div class='dot' style='background:#F012BE'></div>Category</div>
        </div>
    </div>
    <div id='cy'></div>

    <script>
        const graphData = " + jsonData + @";
        
        document.getElementById('nodeCount').innerText = graphData.nodes.length;
        document.getElementById('edgeCount').innerText = graphData.edges.length;

        const cy = cytoscape({
            container: document.getElementById('cy'),
            elements: graphData,
            style: [
                {
                    selector: 'node',
                    style: {
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'color': '#fff',
                        'text-outline-width': 2,
                        'text-outline-color': '#555',
                        'background-color': '#999',
                        'width': 'data(size)',
                        'height': 'data(size)',
                        'font-size': '10px',
                        'z-index': 10
                    }
                },
                { selector: 'node[type=""Trn""]', style: { 'background-color': '#0074D9', 'text-outline-color': '#0074D9' } },
                { selector: 'node[type=""Transaction""]', style: { 'background-color': '#0074D9', 'text-outline-color': '#0074D9' } },
                { selector: 'node[type=""Prc""]', style: { 'background-color': '#2ECC40', 'text-outline-color': '#2ECC40' } },
                { selector: 'node[type=""Procedure""]', style: { 'background-color': '#2ECC40', 'text-outline-color': '#2ECC40' } },
                { selector: 'node[type=""Wbp""]', style: { 'background-color': '#FF851B', 'text-outline-color': '#FF851B' } },
                { selector: 'node[type=""WebPanel""]', style: { 'background-color': '#FF851B', 'text-outline-color': '#FF851B' } },
                { selector: 'node[type=""Tbl""]', style: { 'background-color': '#B10DC9', 'text-outline-color': '#B10DC9' } },
                { selector: 'node[type=""Table""]', style: { 'background-color': '#B10DC9', 'text-outline-color': '#B10DC9' } },
                { selector: 'node[type=""Folder""]', style: { 'background-color': '#FFDC00', 'text-outline-color': '#FFDC00' } },
                { selector: 'node[type=""KBCategory""]', style: { 'background-color': '#F012BE', 'text-outline-color': '#F012BE' } },
                {
                    selector: 'edge',
                    style: {
                        'width': 1,
                        'line-color': '#ddd',
                        'target-arrow-color': '#ddd',
                        'target-arrow-shape': 'triangle',
                        'curve-style': 'bezier',
                        'opacity': 0.6
                    }
                },
                {
                    selector: ':selected',
                    style: {
                        'border-width': 4,
                        'border-color': '#333'
                    }
                },
                { selector: '.semitransp', style: { 'opacity': '0.1' } }
            ],
            layout: {
                name: 'cose',
                animate: false,
                idealEdgeLength: 150,
                nodeOverlap: 40,
                refresh: 20,
                fit: true,
                padding: 30,
                randomize: true,
                componentSpacing: 150,
                nodeRepulsion: 800000,
                edgeElasticity: 150,
                nestingFactor: 5,
                gravity: 100,
                numIter: 1000,
                initialTemp: 300,
                coolingFactor: 0.95,
                minTemp: 1.0
            }
        });

        cy.on('tap', 'node', function(evt){
            var node = evt.target;
            var d = node.data();
            
            var html = '<h3>' + d.label + '</h3>';
            html += '<div class=""prop-label"">Full Name</div><div class=""prop-val"">' + d.id + '</div>';
            html += '<div class=""prop-label"">Type</div><div class=""prop-val"">' + d.type + '</div>';
            html += '<div class=""prop-label"">Domain</div><div class=""prop-val"">' + d.domain + '</div>';
            html += '<div class=""prop-label"">Incoming (References)</div><div class=""prop-val"">' + (node.degree() - node.outdegree()) + '</div>';
            html += '<div class=""prop-label"">Outgoing (Calls)</div><div class=""prop-val"">' + node.outdegree() + '</div>';
            
            document.getElementById('details').innerHTML = html;

            // Highlight connections
            cy.elements().addClass('semitransp');
            node.removeClass('semitransp');
            node.connectedEdges().removeClass('semitransp');
            node.neighborhood().removeClass('semitransp');
        });

        cy.on('tap', function(e){
            if(e.target === cy){
                cy.elements().removeClass('semitransp');
                document.getElementById('details').innerHTML = '<p>Select a node to see dependencies.</p>';
            }
        });

    </script>
</body>
</html>";
        }
    }
}

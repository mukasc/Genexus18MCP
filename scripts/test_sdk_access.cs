using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Linq;

public class SdkTester {
    public static void Main() {
        string gxPath = @"C:\Program Files (x86)\GeneXus\GeneXus18Trial";
        string kbPath = @"C:\kbs\TestesMCP";
        
        try {
            var sw = Stopwatch.StartNew();
            Console.WriteLine("Step 1: Loading Assemblies...");
            Assembly.LoadFrom(Path.Combine(gxPath, "Artech.Common.dll"));
            Assembly.LoadFrom(Path.Combine(gxPath, "Artech.Architecture.Common.dll"));
            Assembly.LoadFrom(Path.Combine(gxPath, "Artech.Architecture.UI.Framework.dll"));
            Assembly.LoadFrom(Path.Combine(gxPath, "Artech.Genexus.Common.dll"));
            var connAsm = Assembly.LoadFrom(Path.Combine(gxPath, "Connector.dll"));
            
            Console.WriteLine("Step 2: Initializing Connector...");
            var uiType = Assembly.LoadFrom(Path.Combine(gxPath, "Artech.Architecture.UI.Framework.dll")).GetType("Artech.Architecture.UI.Framework.Services.UIServices");
            uiType.GetMethod("SetDisableUI", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { true });
            
            var connType = connAsm.GetType("Artech.Core.Connector");
            connType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            connType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            
            var kbType = typeof(Artech.Architecture.Common.Objects.KnowledgeBase);
            kbType.GetProperty("KBFactory", BindingFlags.Public | BindingFlags.Static).SetValue(null, Activator.CreateInstance(connAsm.GetType("Connector.KBFactory")));
            
            var initType = Assembly.LoadFrom(Path.Combine(gxPath, "Artech.Genexus.Common.dll")).GetType("Artech.Genexus.Common.KBModelObjectsInitializer");
            initType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Console.WriteLine("Initialization took {0}ms", sw.ElapsedMilliseconds);

            sw.Restart();
            Console.WriteLine("Step 3: Opening KB...");
            var options = new Artech.Architecture.Common.Objects.KnowledgeBase.OpenOptions(kbPath);
            options.EnableMultiUser = true;
            options.AvoidIndexing = true;
            
            var kb = Artech.Architecture.Common.Objects.KnowledgeBase.Open(options);
            Console.WriteLine("KB Open SUCCESS. DesignModel: {0}", kb.DesignModel.Name);

            sw.Restart();
            Console.WriteLine("Step 4: Listing objects (Top 10)...");
            var count = 0;
            foreach (var obj in kb.DesignModel.Objects) {
                Console.WriteLine("Found: {0} ({1})", obj.Name, obj.Type);
                count++;
                if (count >= 10) break;
            }
            Console.WriteLine("Total objects found in sample: {0}", count);
            
            kb.Close();
        } catch (Exception ex) {
            Console.WriteLine("ERROR: {0}", ex.ToString());
        }
    }
}

using System;
using System.Reflection;

public class TestInspector {
    public static void Main() {
        try {
            var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\GeneXus\GeneXus18\Abstracta.GXtest.Common.dll");
            foreach (var type in asm.GetTypes()) {
                if (type.IsPublic) {
                    Console.WriteLine(type.FullName);
                }
            }
        } catch (Exception e) {
            Console.WriteLine(e.ToString());
        }
    }
}

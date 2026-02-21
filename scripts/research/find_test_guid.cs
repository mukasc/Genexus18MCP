using System;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common.Objects;

public class GuidFinder {
    public static void Main() {
        try {
            // Em vez de var, vou listar tipos explicitamente para evitar problemas de compatibilidade
            foreach (Guid type in KBObjectDescriptor.GetKBObjectTypes()) {
                KBObjectDescriptor descriptor = KBObjectDescriptor.Get(type);
                if (descriptor.Name.ToLower().Contains("test") || descriptor.Description.ToLower().Contains("test")) {
                    Console.WriteLine("Found: " + descriptor.Name + " (GUID: " + type.ToString() + ")");
                }
            }
        } catch (Exception e) {
            Console.WriteLine(e.ToString());
        }
    }
}

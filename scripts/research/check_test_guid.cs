using System;
using Artech.Architecture.Common.Objects;
using Artech.Genexus.Common;

public class TestInspector {
    public static void Main() {
        try {
            Console.WriteLine("Unit Test: " + KBObjectDescriptor.Get(new Guid("7b138244-8848-4354-97c7-c46645a27814")).Name);
        } catch (Exception) {
            Console.WriteLine("Guid not found.");
        }
    }
}

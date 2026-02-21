using System;
using System.Reflection;
using Artech.Genexus.Common;

public class EnumLister {
    public static void Main() {
        foreach (string name in Enum.GetNames(typeof(eDBType))) {
            Console.WriteLine(name);
        }
    }
}

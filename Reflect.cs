using System;
using System.Reflection;
var asm = Assembly.LoadFrom("/root/.nuget/packages/dangl.aspnetcore.filehandling/0.8.4/lib/netstandard2.0/Dangl.AspNetCore.FileHandling.dll");
foreach(var t in asm.GetTypes()) {
    Console.WriteLine(t.FullName);
    foreach(var m in t.GetMethods()) Console.WriteLine("  " + m);
}

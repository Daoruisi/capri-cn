using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Replaces UI string literals in a copy of Capri.exe with Chinese, touching only the listed methods.
internal static class Patch
{
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception error) { Console.Error.WriteLine(error.GetType().FullName + ": " + error.Message + "\n" + error.StackTrace); return 1; }
    }

    private static int Run(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("usage: patch.exe <in.exe> <out.exe> <trans.tsv> <scopes.txt>");
            return 2;
        }
        string input = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);

        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(args[2], Encoding.UTF8))
        {
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int tab = line.IndexOf('\t');
            if (tab <= 0) { Console.Error.WriteLine("bad line: " + line); return 3; }
            map[line.Substring(0, tab)] = line.Substring(tab + 1);
        }

        HashSet<string> scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(args[3], Encoding.UTF8))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0) scopes.Add(trimmed);
        }

        DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(input));
        ReaderParameters read = new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false };
        int replaced = 0, missed = 0;

        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(input, read))
        {
            foreach (ModuleDefinition module in assembly.Modules)
            {
                foreach (TypeDefinition type in module.Types) Walk(type, map, scopes, ref replaced);
            }
            assembly.Write(output, new WriterParameters { WriteSymbols = false });
        }

        Console.WriteLine("replaced " + replaced + " literals -> " + output);
        foreach (KeyValuePair<string, string> entry in map)
        {
            if (!used.Contains(entry.Key)) { Console.WriteLine("unused: " + entry.Key); missed++; }
        }
        Console.WriteLine("unused entries: " + missed);
        return 0;
    }

    private static readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

    private static void Walk(TypeDefinition type, Dictionary<string, string> map, HashSet<string> scopes, ref int replaced)
    {
        foreach (MethodDefinition method in type.Methods)
        {
            if (!method.HasBody) continue;
            string key = type.FullName + "::" + method.Name;
            if (!scopes.Contains(key)) continue;
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Ldstr) continue;
                string value = instruction.Operand as string;
                string chinese;
                if (value == null || !map.TryGetValue(value, out chinese)) continue;
                instruction.Operand = chinese;
                used.Add(value);
                replaced++;
            }
        }
        foreach (TypeDefinition nested in type.NestedTypes) Walk(nested, map, scopes, ref replaced);
    }
}


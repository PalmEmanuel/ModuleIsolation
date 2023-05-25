using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Runtime.Loader;

namespace PipeHow.ModuleIsolation;

/// <summary>
/// Resolves assemblies for a module by finding RootModule and NestedModules from a module manifest in the same directory.
/// </summary>
public class ModuleIsolationAssemblyResolver : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private static readonly Assembly currentAssembly = Assembly.GetExecutingAssembly();
    private static readonly string assemblyRootPath = Path.GetDirectoryName(currentAssembly.Location)!;
    // The directory of the module manifest in the same directory as the assembly
    private static readonly string manifestPath = GetModuleManifestPath(assemblyRootPath);
    // The dependencies directory in the module root path
    private static readonly string dependenciesPath = assemblyRootPath; // Path.Combine(assemblyRootPath, "dependencies");
    // Create the AssemblyLoadContext with the name of the module from the manifest, and the full path to the dependency directory
    private static readonly ModuleIsolationAssemblyLoadContext context = new($"{Path.GetFileNameWithoutExtension(manifestPath)}Context", dependenciesPath);
    // Find a list of all modules to isolate from the manifest, excluding the current assembly that must come first in NestedModules
    private static readonly List<string> modulesToIsolate = FindModulesToIsolate(manifestPath, $"{currentAssembly.GetName().Name}.dll");

    // Set up resolving event subscriptions for importing and removing module
    public void OnImport() => AssemblyLoadContext.Default.Resolving += ResolveIsolatedAssembly;
    public void OnRemove(PSModuleInfo psModuleInfo) => AssemblyLoadContext.Default.Resolving -= ResolveIsolatedAssembly;

    // Resolve assembly in custom ALC if it's found in the modules to isolate
    private static Assembly? ResolveIsolatedAssembly(AssemblyLoadContext defaultAlc, AssemblyName assemblyToResolve)
        => modulesToIsolate.Contains($"{assemblyToResolve.Name}.dll") ? context.LoadFromAssemblyName(assemblyToResolve) : null;

    /// <summary>
    /// Find a module manifest path (*.psd1) from the specified directory
    /// </summary>
    /// <returns>The path to the module manifest.</returns>
    private static string GetModuleManifestPath(string directoryPath) => Directory.GetFiles(Directory.GetParent(directoryPath)!.FullName, "*.psd1").FirstOrDefault() ??
            throw new FileNotFoundException($"Could not find module manifest (.psd1) in directory '{directoryPath}'!");

    /// <summary>
    /// Gets all module names from RootModule and NestedModules from the module manifest in the same directory, except this assembly.
    /// </summary>
    /// <returns>The list of names of modules to isolate.</returns>
    private static List<string> FindModulesToIsolate(string moduleManifestPath, params string[] excludedModules)
    {
        // Get the abstract syntax tree from the PowerShell data file
        var ast = Parser.ParseFile(moduleManifestPath, out _, out ParseError[] errors);
        // Find the hashtable part of the syntax tree
        var data = ast.Find(static a => a is HashtableAst, false);
        if (errors.Any() || data is null)
        {
            throw new InvalidOperationException($"The file {moduleManifestPath} could not be parsed as a PowerShell Data File!");
        }

        // Get the hashtable as an object
        Hashtable manifestData = (Hashtable)data.SafeGetValue();
        // Get RootModule from the manifest
        string? rootModule = (string?)manifestData["RootModule"];
        // Get all .dll files specified by name in NestedModules from the manifest, excluding the current assembly (ModuleIsolation.dll)
        var nestedModules = ((object[]?)manifestData["NestedModules"])?
            .Select(m => (string)m)
            .Where(m =>
                !m.Contains(Path.DirectorySeparatorChar) && // Only handle direct module references
                !m.Contains(Path.AltDirectorySeparatorChar) &&
                m.EndsWith(".dll") && // Only DLL files
                !excludedModules.Any(ex => ex.Contains(m))); // No reference or path to excluded modules

        // Create and return a list of the gathered modules
        var moduleList = new List<string>();
        if (rootModule is not null) moduleList.Add(rootModule);
        if (nestedModules?.Any() == true) moduleList.AddRange(nestedModules);

        return moduleList;
    }
}

/// <summary>
/// An AssemblyLoadContext with a custom name that finds assemblies in a specific directory for dependencies.
/// </summary>
internal class ModuleIsolationAssemblyLoadContext : AssemblyLoadContext
{
    // The path to the directory where dependency assemblies should be loaded from
    private readonly string dependenciesPath;

    public ModuleIsolationAssemblyLoadContext(string name, string directory) : base(name) => dependenciesPath = directory;

    // Load the assembly into this custom ALC using LoadFromAssemblyPath if it exists, otherwise return null to let PowerShell know it's not here
    protected override Assembly? Load(AssemblyName assemblyName) =>
       File.Exists(GetAssemblyDependencyPath(assemblyName)) ? LoadFromAssemblyPath(GetAssemblyDependencyPath(assemblyName)) : null;

    // Get the full path to the assembly from dependency directory, name and .dll extension
    private string GetAssemblyDependencyPath(AssemblyName assemblyName) =>
        Path.Combine(dependenciesPath, $"{assemblyName.Name}.dll");
}
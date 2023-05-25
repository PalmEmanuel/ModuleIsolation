# ModuleIsolation

A library for simplifying AssemblyLoadContext implementations for PowerShell modules.

Deploy and reference **ModuleIsolation** together with your PowerShell module, and it will take care of loading your entire module and all assembly dependencies into its own AssemblyLoadContext.

## How to use

1. Add the package to your project.
2. Modify the `NestedModules` of your PowerShell module manifest to have `<relative-path>\ModuleIsolation.dll` as the first module in the list.
3. Ensure that the assembly file `ModuleIsolation.dll` is deployed together with your module, to the specified location.
4. Done! When importing the module, **ModuleIsolation** will hook into the assembly loading process and ensure that all assemblies stored in the same folder (including your module) are loaded into a custom AssemblyLoadContext specific to your module.

### CSharp

Add the package as a reference to your module project.

```plaintext
dotnet add package PipeHow.ModuleIsolation
```

Or add it as a package reference to the `.csproj` file.

```plaintext
<PackageReference Include="PipeHow.ModuleIsolation" Version="1.0.0" />
```

### PowerShell

Build a project to get the `ModuleManifest.dll` file, and deploy it together with your module.

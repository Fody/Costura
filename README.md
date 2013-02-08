## This is an add-in for [Fody](https://github.com/Fody/Fody/) 

Embeds dependencies as resources.

[Introduction to Fody](http://github.com/Fody/Fody/wiki/SampleUsage)

## Nuget package http://nuget.org/packages/Costura.Fody 

What it actually does to your assembly

# How it works

## Merge assemblies as embedded resources.

This approach uses a combination of two methods

 * Jeffrey Richter's suggestion of using [embedded resources as a method of merging assemblies](http://blogs.msdn.com/b/microsoft_press/archive/2010/02/03/jeffrey-richter-excerpt-2-from-clr-via-c-third-edition.aspx)
 * Einar Egilsson's suggestion [using cecil to create module initializers](http://tech.einaregilsson.com/2009/12/16/module-initializers-in-csharp/)

## Details 

This Task performs the following changes

 * Take all assemblies (and pdbs) that have been marked as "Copy Local" and embed them as resources in the target assembly.
 * Injects the following code into the module initializer of the target assembly. This code will be called when the assembly is loaded into memory

eg 

    static <Module>()
    {
        ILTemplate.Attach();
    }

 * Injects the following class into the target assembly. This means if an assembly load fails it will be loaded from the embedded resources

eg

    static class ILTemplate
    {
        public static void Attach()
        {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += OnCurrentDomainOnAssemblyResolve;
        }

        public static Assembly OnCurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            var assemblyResourceName = string.Format("WeavingTask.{0}.dll", name);
            var executingAssembly = Assembly.GetExecutingAssembly();

            using (var assemblyStream = executingAssembly.GetManifestResourceStream(assemblyResourceName))
            {
                var assemblyData = new Byte[assemblyStream.Length];
                assemblyStream.Read(assemblyData, 0, assemblyData.Length);

                using (var pdbStream = executingAssembly.GetManifestResourceStream(Path.ChangeExtension(assemblyResourceName, "pdb")))
                {
                    if (pdbStream != null)
                    {
                        var pdbData = new Byte[pdbStream.Length];
                        pdbStream.Read(pdbData, 0, pdbData.Length);
                        return Assembly.Load(assemblyData, pdbData);
                    }
                }
                return Assembly.Load(assemblyData);
            }
        }
    }

# Configuration Options

All config options are access by modifying the `Costura` node in FodyWeavers.xml

## CreateTemporaryAssemblies

This will copy embedded files to disk before loading them into memory. This is helpful for some scenarios that expected an assembly to be loaded from a physical file.

*Defaults to `false`*

    <Costura CreateTemporaryAssemblies='true'/>
    
## IncludeDebugSymbols

Controls if .pdbs for reference assemblies are also embedded.

*Defaults to `false`*

    <Costura IncludeDebugSymbols='false'/>
    
## ExcludeAssemblies

A list of assembly names to exclude from the default action of "embed all Copy Local references".

Do not include `.exe` or `.dll` in the names.

Can not be defiend with `IncludeAssemblies`.

Can take two forms. 

As an element

    <Costura>
        <ExcludeAssemblies>
            Foo
            Bar
        </ExcludeAssemblies>
    </Costura>
    
Or as a attribute

    <Costura ExcludeAssemblies='Foo|Bar'/>
    
        
## IncludeAssemblies

A list of assembly names to include from the default action of "embed all Copy Local references".

Do not include `.exe` or `.dll` in the names.

Can not be defiend with `ExcludeAssemblies`.

Can take two forms. 

As an element

    <Costura>
        <IncludeAssemblies>
            Foo
            Bar
        </IncludeAssemblies>
    </Costura>
    
Or as a attribute

    <Costura IncludeAssemblies='Foo|Bar'/>

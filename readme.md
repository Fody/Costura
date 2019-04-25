### <img src="https://raw.githubusercontent.com/Fody/Costura/master/package_icon.png" height="28px"> Costura is an add-in for [Fody](https://github.com/Fody/Home/)

Embeds dependencies as resources.

[![Chat on Gitter](https://img.shields.io/gitter/room/fody/fody.svg?style=flat)](https://gitter.im/Fody/Fody)
[![NuGet Status](http://img.shields.io/nuget/v/Costura.Fody.svg?style=flat)](https://www.nuget.org/packages/Costura.Fody/)


### NuGet installation

Install the [Costura.Fody NuGet package](https://nuget.org/packages/Costura.Fody/) and update the [Fody NuGet package](https://nuget.org/packages/Fody/):

```powershell
PM> Install-Package Fody
PM> Install-Package Costura.Fody
```

The `Install-Package Fody` is required since NuGet always defaults to the oldest, and most buggy, version of any dependency.


### Add to FodyWeavers.xml

Add `<Costura/>` to [FodyWeavers.xml](https://github.com/Fody/Home/blob/master/pages/usage.md#add-fodyweaversxml)

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Weavers>
  <Costura/>
</Weavers>
```


## How it works


### Merge assemblies as embedded resources

This approach uses a combination of two methods

 * Jeffrey Richter's suggestion of using [embedded resources as a method of merging assemblies](http://blogs.msdn.com/b/microsoft_press/archive/2010/02/03/jeffrey-richter-excerpt-2-from-clr-via-c-third-edition.aspx)
 * Einar Egilsson's suggestion [using cecil to create module initializers](http://tech.einaregilsson.com/2009/12/16/module-initializers-in-csharp/)


### Details

This Task performs the following changes

 * Take all assemblies (and pdbs) that have been marked as "Copy Local" and embed them as resources in the target assembly.
 * Injects the following code into the module initializer of the target assembly. This code will be called when the assembly is loaded into memory

eg 

```csharp
static <Module>()
{
    ILTemplate.Attach();
}
```

 * Injects the following class into the target assembly. This means if an assembly load fails it will be loaded from the embedded resources.
    * [ILTemplate.cs](https://github.com/Fody/Costura/blob/master/Costura.Template/ILTemplate.cs)
    * [ILTemplateWithTempAssembly.cs](https://github.com/Fody/Costura/blob/master/Costura.Template/ILTemplateWithTempAssembly.cs)


## Configuration Options

All config options are accessed by modifying the `Costura` node in FodyWeavers.xml.

Default FodyWeavers.xml:

```xml
<Weavers>
  <Costura />
</Weavers>
```


### CreateTemporaryAssemblies

This will copy embedded files to disk before loading them into memory. This is helpful for some scenarios that expected an assembly to be loaded from a physical file.

*Defaults to `false`*

```xml
<Costura CreateTemporaryAssemblies='true' />
```


### IncludeDebugSymbols

Controls if .pdbs for reference assemblies are also embedded.

*Defaults to `true`*

```xml
<Costura IncludeDebugSymbols='false' />
```


### DisableCompression

Embedded assemblies are compressed by default, and uncompressed when they are loaded. You can turn compression off with this option.

*Defaults to `false`*


```xml
<Costura DisableCompression='true' />
```


### DisableCleanup

As part of Costura, embedded assemblies are no longer included as part of the build. This cleanup can be turned off.

*Defaults to `false`*

```xml
<Costura DisableCleanup='true' />
```


### LoadAtModuleInit

Costura by default will load as part of the module initialization. This flag disables that behaviour. Make sure you call `CosturaUtility.Initialize()` somewhere in your code.

*Defaults to `true`*

```xml
<Costura LoadAtModuleInit='false' />
```


### IgnoreSatelliteAssemblies

Costura will by default use assemblies with a name like 'resources.dll' as a satellite resource and prepend the output path. This flag disables that behavior.

*Defaults to `false`*

```xml
<Costura IgnoreSatelliteAssemblies='true' />
```


### ExcludeAssemblies

A list of assembly names to exclude from the default action of "embed all Copy Local references".

Do not include `.exe` or `.dll` in the names.

Can not be defined with `IncludeAssemblies`.

Can use wildcards for partial assembly name matching. For example `System.*` will exclude all assemblies that start with `System.`. Wildcards may only be used at the end of an entry so for example, `System.*.Private.*` would not work.

Can take two forms.

As an element with items delimited by a newline.

```xml
<Costura>
  <ExcludeAssemblies>
    Foo
    Bar
  </ExcludeAssemblies>
</Costura>
```

Or as an attribute with items delimited by a pipe `|`.

```xml
<Costura ExcludeAssemblies='Foo|Bar' />
```


### IncludeAssemblies

A list of assembly names to include from the default action of "embed all Copy Local references".

Do not include `.exe` or `.dll` in the names.

Can not be defined with `ExcludeAssemblies`.

Can use wildcards at the end of the name for partial matching.

Can take two forms. 

As an element with items delimited by a newline.

```xml
<Costura>
  <IncludeAssemblies>
    Foo
    Bar
  </IncludeAssemblies>
</Costura>
```

Or as an attribute with items delimited by a pipe `|`.

```xml
<Costura IncludeAssemblies='Foo|Bar' />
```


### Unmanaged32Assemblies & Unmanaged64Assemblies

Mixed-mode assemblies cannot be loaded the same way as managed assemblies.

Therefore, to help Costura identify which assemblies are mixed-mode, and in what environment to load them in you should include their names in one or both of these lists.

Do not include `.exe` or `.dll` in the names.

Can use wildcards at the end of the name for partial matching.

Can take two forms. 

As an element with items delimited by a newline.

```xml
<Costura>
  <Unmanaged32Assemblies>
    Foo32
    Bar32
  </Unmanaged32Assemblies>
  <Unmanaged64Assemblies>
    Foo64
    Bar64
  </Unmanaged64Assemblies>
</Costura>
```

Or as a attribute with items delimited by a pipe `|`.

```xml
<Costura
    Unmanaged32Assemblies='Foo32|Bar32' 
    Unmanaged64Assemblies='Foo64|Bar64' />
```


### Native Libraries and PreloadOrder

Native libraries can be loaded by Costura automatically. To include a native library include it in your project as an Embedded Resource in a folder called `costura32` or `costura64` depending on the bittyness of the library.

Optionally you can also specify the order that preloaded libraries are loaded. When using temporary assemblies from disk mixed mode assemblies are also preloaded.

To specify the order of preloaded assemblies add a `PreloadOrder` element to the config.

```xml
<Costura>
  <PreloadOrder>
    Foo
    Bar
  </PreloadOrder>
</Costura>
```

Or as a attribute with items delimited by a pipe `|`.

```xml
<Costura PreloadOrder='Foo|Bar' />
```


## CosturaUtility

`CosturaUtility` is a class that gives you access to initialize the Costura system manually in your own code. This is mainly for scenarios where the module initializer doesn't work, such as libraries and Mono.

To use, call `CosturaUtility.Initialize()` somewhere in your code, as early as possible.

```csharp
class Program
{
    static Program()
    {
        CosturaUtility.Initialize();
    }

    static void Main(string[] args) { ... }
}
```


## Unit Testing

Most unit test frameworks need the `.dll`s files in order to discover and perform the unit tests.  You may need to add Costura and a configuration like the below to your testing assembly. 

```xml
<Weavers>
    <Costura ExcludeAssemblies='TargetExe|TargetExeTest'
             CreateTemporaryAssemblies='true'
             DisableCleanup='true'/>
</Weavers>
```


## Icon

<a href="http://thenounproject.com/noun/merge/#icon-No256" target="_blank">Merge</a> from The Noun Project
using System.Diagnostics.CodeAnalysis;

// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

[assembly: SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1010:Property '[property]' must notify when value changes.", Justification = "Don't enforce this")]
[assembly: SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1011:Implement INotifyPropertyChanged.", Justification = "Don't enforce this")]
[assembly: SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Used to register language resources and types")]

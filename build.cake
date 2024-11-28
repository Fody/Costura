//=======================================================
// DEFINE PARAMETERS
//=======================================================

// Define the required parameters
var Parameters = new Dictionary<string, object>();
Parameters["SolutionName"] = "Costura.Fody";
Parameters["Company"] = "Fody";
Parameters["RepositoryUrl"] = string.Format("https://github.com/{0}/{1}", GetBuildServerVariable("SolutionName"), GetBuildServerVariable("SolutionName"));
Parameters["StartYear"] = "2015";
Parameters["UseVisualStudioPrerelease"] = "true";
Parameters["SkipComponentsThatAreNotDeployable"] = "false";
Parameters["BuildCostura"] = "true";
Parameters["DeployCostura"] = "false";
Parameters["NuGet_NoDependencies"] = "false";
Parameters["TestProcessBit"] = "X86";

// Note: the rest of the variables should be coming from the build server,
// see `/deployment/cake/*-variables.cake` for customization options
// 
// If required, more variables can be overridden by specifying them via the 
// Parameters dictionary, but the build server variables will always override
// them if defined by the build server. For example, to override the code
// sign wild card, add this to build.cake
//
// Parameters["CodeSignWildcard"] = "Orc.EntityFramework";

//=======================================================
// DEFINE COMPONENTS TO BUILD / PACKAGE
//=======================================================

Dependencies.Add("Costura");
Dependencies.Add("Costura.Template");
Dependencies.Add("ExeToReference");
Dependencies.Add("AssemblyToReference");
Dependencies.Add("AssemblyToReferenceNative");
Dependencies.Add("AssemblyToReferenceMixed");
Dependencies.Add("AssemblyToReferencePreEmbedded");
Dependencies.Add("AssemblyToReferenceWithRuntimeReferences");
Dependencies.Add("AssemblyWithoutInitialize");
Dependencies.Add("AssemblyToProcess");
Dependencies.Add("ExeToProcess");
Dependencies.Add("ExeToProcessWithNative");
Dependencies.Add("ExeToProcessWithNativeAndEmbeddedMixed");
Dependencies.Add("ExeToProcessWithMultipleNative");

Components.Add("Costura.Fody");
Components.Add("Costura");

TestProjects.Add("Costura.Fody.Tests");

//=======================================================
// REQUIRED INITIALIZATION, DO NOT CHANGE
//=======================================================

// Now all variables are defined, include the tasks, that
// script will take care of the rest of the magic

#l "./deployment/cake/tasks.cake"
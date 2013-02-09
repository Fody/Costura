// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the ASSEMBLYTOREFERENCENATIVE_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// ASSEMBLYTOREFERENCENATIVE_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef ASSEMBLYTOREFERENCENATIVE_EXPORTS
#define ASSEMBLYTOREFERENCENATIVE_API __declspec(dllexport)
#else
#define ASSEMBLYTOREFERENCENATIVE_API __declspec(dllimport)
#endif

EXTERN_C ASSEMBLYTOREFERENCENATIVE_API char* SayHelloFromNative();

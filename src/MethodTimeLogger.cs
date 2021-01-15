using System.Reflection;
using System;
using System.Globalization;

/// <summary>
/// Note: do not rename this class or put it inside a namespace.
/// </summary>
internal static class MethodTimeLogger
{
    #region Methods
    public static void Log(MethodBase methodBase, long milliseconds, string message)
    {
        Log(methodBase.DeclaringType ?? typeof(object), methodBase.Name, milliseconds, message);
    }

    public static void Log(Type type, string methodName, long milliseconds, string message)
    {

    }
    #endregion
}
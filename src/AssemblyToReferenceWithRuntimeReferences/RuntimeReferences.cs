using System;
using Microsoft.Data.SqlClient;

public static class RuntimeReferences
{
    public static string UseAssemblyWithRuntimeAssemblies()
    {
        var connection = new SqlConnection("Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;");

        try
        {
            connection.Open();
        }
        catch (Exception)
        {
            // ignore
        }

        return "Hello";
    }
}

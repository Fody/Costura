using System.Reflection;

public class ClassToTest
{
    public void MethodThatDoesLoading()
    {
        Assembly.Load("BadAssemblyName");
    }
}

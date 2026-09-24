using Fody;

// weaving tests write assemblies to shared folders and load them into the same process
[NotInParallel]
public abstract class BaseCosturaTest
{
    public abstract TestResult TestResult { get; }
}

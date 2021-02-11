using System;
using Fody;
using NUnit.Framework;

[TestFixture]
public abstract class BaseCosturaTest
{
    public abstract TestResult TestResult { get; }

    protected string GetScenarioName()
    {
        var scenarioName = GetType().Name;

#if NETCORE
        scenarioName += ".netcore";
#else
        scenarioName += ".net";
#endif

#if DEBUG
        scenarioName += ".debug";
#else
        scenarioName += ".release";
#endif

        return scenarioName;
    }
}

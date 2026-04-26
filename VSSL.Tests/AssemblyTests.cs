using System.Reflection;
using VSSL.Ui.Views;
using Xunit.Abstractions;

namespace VSSL.Tests;

public class AssemblyTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void TestFindAllServices()
    {
        var serviceTypes = Assembly.Load("VSSL.Services")
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.FullName!.EndsWith("Service"))
            .ToList();
        Assert.NotEmpty(serviceTypes);
        foreach (var serviceType in serviceTypes)
        {
            var interfaces = serviceType.GetInterfaces();
            if (interfaces.Length == 0) continue;

            foreach (var @interface in interfaces)
            {
                testOutputHelper.WriteLine(@interface.FullName);
                testOutputHelper.WriteLine(serviceType.FullName);
                testOutputHelper.WriteLine("------------------------");
            }
        }
    }

    [Fact]
    public void TestFindAllViews()
    {
        var viewTypes = Assembly.GetAssembly(typeof(MainWindow))!
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.FullName!.EndsWith("View"))
            .ToList();
        Assert.NotEmpty(viewTypes);
        foreach (var viewType in viewTypes) testOutputHelper.WriteLine(viewType.FullName);
    }
}
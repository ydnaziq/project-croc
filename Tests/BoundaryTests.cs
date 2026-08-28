using System;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class BoundaryTests
{
    [Fact]
    public void CoreAssemblyDoesNotReferenceGodot()
    {
        var referenced = typeof(JawZone).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        Assert.DoesNotContain(referenced, name =>
            name.Contains("Godot", StringComparison.OrdinalIgnoreCase));
    }
}

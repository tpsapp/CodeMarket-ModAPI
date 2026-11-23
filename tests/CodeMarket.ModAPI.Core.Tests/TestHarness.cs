using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeMarket.ModAPI.Core.Tests;

public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

public static class TestHarness
{
    private readonly record struct TestEntry(string Suite, string Name, Action Execute);

    private static readonly List<TestEntry> Tests = new();

    public static void Register(string suite, string name, Action test)
    {
        if (string.IsNullOrWhiteSpace(suite)) throw new ArgumentException("Suite name is required.", nameof(suite));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Test name is required.", nameof(name));
        if (test is null) throw new ArgumentNullException(nameof(test));

        Tests.Add(new TestEntry(suite, name, test));
    }

    public static int Run(string? suiteFilter = null)
    {
        List<TestEntry> selected = string.IsNullOrWhiteSpace(suiteFilter)
            ? Tests
            : Tests.Where(t => string.Equals(t.Suite, suiteFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selected.Count == 0)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(suiteFilter)
                ? "ERROR: No tests registered."
                : $"ERROR: Unknown or empty test suite '{suiteFilter}'.");
            return 1;
        }

        int passed = 0;
        int failed = 0;

        foreach (TestEntry entry in selected)
        {
            try
            {
                entry.Execute();
                passed++;
                Console.WriteLine($"  [PASS] {entry.Suite}/{entry.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"  [FAIL] {entry.Suite}/{entry.Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Summary: {passed}/{selected.Count} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    public static void CheckTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }

    public static void CheckEq<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"{message} (expected: '{expected}', actual: '{actual}')");
        }
    }

    public static void CheckThrows<TException>(Action action, string message) where TException : Exception
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"{message} (expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message})");
        }

        throw new AssertionException($"{message} (expected {typeof(TException).Name}, but no exception was thrown)");
    }
}

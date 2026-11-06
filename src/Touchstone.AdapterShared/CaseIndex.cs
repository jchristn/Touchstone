namespace Touchstone.Adapters
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using Touchstone.Core;

    /// <summary>
    /// Builds and caches test identity lookups for adapters. Compiled into each adapter assembly.
    /// </summary>
    internal static class CaseIndex
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, TestCaseDescriptor>> _Providers =
            new ConcurrentDictionary<Type, IReadOnlyDictionary<string, TestCaseDescriptor>>();

        internal static IReadOnlyList<TestCaseDescriptor> Flatten(IReadOnlyList<TestSuiteDescriptor> suites, string paramName)
        {
            if (suites == null) throw new ArgumentNullException(paramName);

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            HashSet<string> suiteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TestSuiteDescriptor? suite in suites)
            {
                if (suite == null) throw new ArgumentException("The suite list contains null.", paramName);
                if (!suiteIds.Add(suite.SuiteId))
                    throw new DuplicateTestIdException(suite.SuiteId, "More than one suite has identity " + suite.SuiteId + ".", paramName);
                cases.AddRange(suite.Cases);
            }

            return cases;
        }

        internal static TestCaseDescriptor Resolve(Type providerType, string testId)
        {
            IReadOnlyDictionary<string, TestCaseDescriptor> index = _Providers.GetOrAdd(providerType, Build);
            if (index.TryGetValue(testId, out TestCaseDescriptor? testCase)) return testCase;

            throw new InvalidOperationException(
                "Touchstone case " + testId + " is not supplied by " + providerType.FullName
                + ". The suites changed between discovery and execution; rebuild and rediscover the tests.");
        }

        internal static IReadOnlyList<TestSuiteDescriptor> Suites(Type providerType)
        {
            ITestSuiteProvider provider = Create(providerType);
            return provider.Suites ?? throw new InvalidOperationException(providerType.FullName + ".Suites returned null.");
        }

        internal static Type ProviderType(string assemblyQualifiedName)
        {
            Type? type = Type.GetType(assemblyQualifiedName, throwOnError: false);
            if (type == null)
                throw new InvalidOperationException("Touchstone suite provider type " + assemblyQualifiedName + " cannot be loaded.");
            return type;
        }

        private static IReadOnlyDictionary<string, TestCaseDescriptor> Build(Type providerType)
        {
            Dictionary<string, TestCaseDescriptor> index = new Dictionary<string, TestCaseDescriptor>(StringComparer.Ordinal);
            foreach (TestCaseDescriptor testCase in Flatten(Suites(providerType), "suites"))
                index[testCase.TestId] = testCase;
            return index;
        }

        private static ITestSuiteProvider Create(Type providerType)
        {
            if (!typeof(ITestSuiteProvider).IsAssignableFrom(providerType))
                throw new InvalidOperationException(providerType.FullName + " does not implement ITestSuiteProvider.");

            object? instance = Activator.CreateInstance(providerType);
            return instance as ITestSuiteProvider
                ?? throw new InvalidOperationException(providerType.FullName + " could not be created.");
        }
    }
}

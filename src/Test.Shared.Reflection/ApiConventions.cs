namespace Test.Shared.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    using Test.Shared;
    using Touchstone.Core;

    /// <summary>
    /// Catalog section 11.24: reflection checks over a shipping assembly. Adapter test projects reuse these checks
    /// for their own assemblies.
    /// </summary>
    public static class ApiConventions
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>
        /// The API convention suite for one assembly.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="assembly">Assembly under test.</param>
        /// <param name="extensionPoints">Public non-sealed classes that are intended extension points.</param>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor Suite(string suiteId, Assembly assembly, params Type[] extensionPoints)
        {
            string name = assembly.GetName().Name!;
            Func<string, string, Action, TestCaseDescriptor> check = (id, title, body) =>
                new TestCaseDescriptor(suiteId, id, id + " " + name + ": " + title, ct =>
                {
                    body();
                    return Task.CompletedTask;
                });

            return new TestSuiteDescriptor(suiteId, "API conventions: " + name, new[]
            {
                check("API-01", "CancellationToken is the last parameter", () =>
                {
                    foreach (MethodBase method in PublicMethods(assembly))
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        int index = Array.FindIndex(parameters, p => p.ParameterType == typeof(CancellationToken));
                        if (index >= 0) TestAssert.Equal(parameters.Length - 1, index, Describe(method) + " token position");
                    }
                }),
                check("API-02", "Public async methods accept a CancellationToken", () =>
                {
                    foreach (MethodInfo method in PublicMethods(assembly).OfType<MethodInfo>().Where(IsAsync))
                        TestAssert.True(method.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)), Describe(method) + " accepts a token");
                }),
                check("API-03", "Public async method names end with Async", () =>
                {
                    foreach (MethodInfo method in PublicMethods(assembly).OfType<MethodInfo>().Where(IsAsync))
                        TestAssert.True(method.Name.EndsWith("Async", StringComparison.Ordinal), Describe(method) + " name");
                }),
                check("API-04", "Public classes are sealed, static, or abstract", () =>
                {
                    foreach (Type type in PublicTypes(assembly).Where(t => t.IsClass && !t.IsAbstract && !t.IsSealed))
                        TestAssert.True(extensionPoints.Contains(type), type.FullName + " is unsealed without being a listed extension point");
                }),
                check("API-05", "Public types live in the assembly's root namespace", () =>
                {
                    foreach (Type type in PublicTypes(assembly))
                        TestAssert.Equal(name, type.Namespace, type.FullName + " namespace");
                }),
                check("API-06", "No public member exposes a tuple", () =>
                {
                    foreach (MethodBase method in PublicMethods(assembly))
                    {
                        IEnumerable<Type> types = method.GetParameters().Select(p => p.ParameterType);
                        if (method is MethodInfo info) types = types.Append(info.ReturnType);
                        foreach (Type type in types)
                            TestAssert.False(IsTuple(type), Describe(method) + " uses a tuple");
                    }
                }),
                check("API-07", "Every public type and member is documented in the shipped XML file", () =>
                {
                    Dictionary<string, XElement> docs = Docs(assembly);
                    List<string> missing = new List<string>();
                    foreach (Type type in PublicTypes(assembly))
                    {
                        if (!docs.ContainsKey("T:" + DocName(type))) missing.Add("T:" + DocName(type));
                        foreach (MemberInfo member in type.GetMembers(Declared).Where(IsVisible))
                        {
                            string? id = DocId(member);
                            if (id == null || IsInheritedOrImplicit(member)) continue;
                            if (!docs.ContainsKey(id)) missing.Add(id);
                        }
                    }

                    TestAssert.Equal(string.Empty, string.Join("; ", missing), "undocumented members");
                }),
                check("API-08", "Methods document every parameter and their return value", () =>
                {
                    Dictionary<string, XElement> docs = Docs(assembly);
                    List<string> missing = new List<string>();
                    foreach (MethodBase method in PublicMethods(assembly))
                    {
                        string? id = DocId(method);
                        if (id == null || !docs.TryGetValue(id, out XElement? doc) || doc.Element("inheritdoc") != null) continue;
                        foreach (ParameterInfo parameter in method.GetParameters())
                        {
                            if (!doc.Elements("param").Any(p => (string?)p.Attribute("name") == parameter.Name)) missing.Add(id + " param " + parameter.Name);
                        }

                        if (method is MethodInfo info && info.ReturnType != typeof(void) && doc.Element("returns") == null) missing.Add(id + " returns");
                        foreach (Type generic in method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes)
                        {
                            if (!doc.Elements("typeparam").Any(p => (string?)p.Attribute("name") == generic.Name)) missing.Add(id + " typeparam " + generic.Name);
                        }
                    }

                    foreach (Type type in PublicTypes(assembly).Where(t => t.IsGenericTypeDefinition))
                    {
                        if (docs.TryGetValue("T:" + DocName(type), out XElement? doc))
                        {
                            foreach (Type generic in type.GetGenericArguments())
                            {
                                if (!doc.Elements("typeparam").Any(p => (string?)p.Attribute("name") == generic.Name)) missing.Add("T:" + DocName(type) + " typeparam " + generic.Name);
                            }
                        }
                    }

                    TestAssert.Equal(string.Empty, string.Join("; ", missing), "incomplete documentation");
                }),
                check("API-09", "No documentation cites the generic Exception type", () =>
                {
                    foreach (KeyValuePair<string, XElement> doc in Docs(assembly))
                    {
                        foreach (XElement exception in doc.Value.Elements("exception"))
                            TestAssert.False((string?)exception.Attribute("cref") == "T:System.Exception", doc.Key + " documents System.Exception");
                    }
                }),
                check("API-10", "Documentation contains no em-dash or dash stand-in", () =>
                {
                    foreach (KeyValuePair<string, XElement> doc in Docs(assembly))
                    {
                        string text = doc.Value.Value;
                        TestAssert.False(text.Contains('\u2014'), doc.Key + " contains an em-dash");
                        TestAssert.False(text.Contains(" -- ", StringComparison.Ordinal), doc.Key + " contains a dash stand-in");
                    }
                }),
                check("API-11", "Public exceptions are sealed and have the standard constructors", () =>
                {
                    foreach (Type type in PublicTypes(assembly).Where(t => typeof(Exception).IsAssignableFrom(t)))
                    {
                        TestAssert.True(type.IsSealed, type.FullName + " is sealed");
                        TestAssert.NotNull(type.GetConstructor(Type.EmptyTypes), type.FullName + "()");
                        TestAssert.NotNull(type.GetConstructor(new[] { typeof(string) }), type.FullName + "(string)");
                        TestAssert.NotNull(type.GetConstructor(new[] { typeof(string), typeof(Exception) }), type.FullName + "(string, Exception)");
                    }
                }),
                check("API-13", "Every public class and interface states its thread safety", () =>
                {
                    Dictionary<string, XElement> docs = Docs(assembly);
                    List<string> missing = new List<string>();
                    foreach (Type type in PublicTypes(assembly).Where(t => (t.IsClass || t.IsInterface) && !typeof(Delegate).IsAssignableFrom(t)))
                    {
                        if (!docs.TryGetValue("T:" + DocName(type), out XElement? doc) || !(doc.Element("summary")?.Value ?? string.Empty).Contains("Thread safety", StringComparison.Ordinal))
                            missing.Add(type.FullName!);
                    }

                    TestAssert.Equal(string.Empty, string.Join("; ", missing), "types without a thread-safety statement");
                }),
                check("API-12", "Public enums have contiguous values starting at zero", () =>
                {
                    foreach (Type type in PublicTypes(assembly).Where(t => t.IsEnum))
                    {
                        int[] values = Enum.GetValues(type).Cast<object>().Select(Convert.ToInt32).OrderBy(v => v).ToArray();
                        TestAssert.Equal(string.Join(",", Enumerable.Range(0, values.Length)), string.Join(",", values), type.FullName + " values");
                    }
                }),
            });
        }

        private static IEnumerable<Type> PublicTypes(Assembly assembly)
        {
            return assembly.GetExportedTypes().Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), false));
        }

        private static IEnumerable<MethodBase> PublicMethods(Assembly assembly)
        {
            foreach (Type type in PublicTypes(assembly))
            {
                foreach (MethodBase method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
                {
                    if (!IsVisible(method) || method.IsSpecialName && !(method is ConstructorInfo)) continue;
                    yield return method;
                }
            }
        }

        private static bool IsVisible(MemberInfo member)
        {
            switch (member)
            {
                case MethodBase method: return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
                case FieldInfo field: return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
                case PropertyInfo property: return (property.GetMethod ?? property.SetMethod) is MethodInfo accessor && IsVisible(accessor);
                case EventInfo e: return e.AddMethod != null && IsVisible(e.AddMethod);
                case Type nested: return nested.IsNestedPublic || nested.IsNestedFamily;
                default: return false;
            }
        }

        private static bool IsInheritedOrImplicit(MemberInfo member)
        {
            if (member is MethodInfo method)
            {
                if (method.IsSpecialName) return true;
                if (method.GetBaseDefinition() != method) return true;
                if (method.DeclaringType != null && method.DeclaringType.GetInterfaces().Any(i => i.GetMethods().Any(m => m.Name == method.Name))) return false;
            }

            return member is FieldInfo field && field.DeclaringType != null && field.DeclaringType.IsEnum && field.Name == "value__";
        }

        private static bool IsAsync(MethodInfo method)
        {
            Type type = method.ReturnType;
            return type == typeof(Task) || type == typeof(ValueTask)
                || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)));
        }

        private static bool IsTuple(Type type)
        {
            if (type.IsGenericType)
            {
                string? full = type.GetGenericTypeDefinition().FullName;
                if (full != null && (full.StartsWith("System.ValueTuple", StringComparison.Ordinal) || full.StartsWith("System.Tuple", StringComparison.Ordinal))) return true;
                return type.GetGenericArguments().Any(IsTuple);
            }

            return type.IsArray && IsTuple(type.GetElementType()!);
        }

        private static string Describe(MethodBase method)
        {
            return method.DeclaringType!.FullName + "." + method.Name;
        }

        private static Dictionary<string, XElement> Docs(Assembly assembly)
        {
            string path = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(path)) throw new TestAssertionException("Documentation file " + path + " was not found beside the assembly.");
            return XDocument.Load(path).Descendants("member")
                .GroupBy(m => (string)m.Attribute("name")!)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }

        private static string DocName(Type type)
        {
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static string? DocId(MemberInfo member)
        {
            string owner = DocName(member.DeclaringType!);
            switch (member)
            {
                case Type nested: return "T:" + DocName(nested);
                case FieldInfo field: return "F:" + owner + "." + field.Name;
                case PropertyInfo property: return "P:" + owner + "." + property.Name + Parameters(property.GetIndexParameters(), Type.EmptyTypes, Type.EmptyTypes);
                case EventInfo e: return "E:" + owner + "." + e.Name;
                case ConstructorInfo ctor: return "M:" + owner + "." + (ctor.IsStatic ? "#cctor" : "#ctor") + Parameters(ctor.GetParameters(), member.DeclaringType!.GetGenericArguments(), Type.EmptyTypes);
                case MethodInfo method:
                    string generic = method.IsGenericMethod ? "``" + method.GetGenericArguments().Length : string.Empty;
                    return "M:" + owner + "." + method.Name + generic + Parameters(method.GetParameters(), member.DeclaringType!.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes);
                default: return null;
            }
        }

        private static string Parameters(ParameterInfo[] parameters, Type[] typeArguments, Type[] methodArguments)
        {
            if (parameters.Length == 0) return string.Empty;
            return "(" + string.Join(",", parameters.Select(p => TypeName(p.ParameterType, typeArguments, methodArguments))) + ")";
        }

        private static string TypeName(Type type, Type[] typeArguments, Type[] methodArguments)
        {
            if (type.IsGenericParameter)
                return type.DeclaringMethod != null ? "``" + type.GenericParameterPosition : "`" + type.GenericParameterPosition;
            if (type.IsByRef) return TypeName(type.GetElementType()!, typeArguments, methodArguments) + "@";
            if (type.IsArray) return TypeName(type.GetElementType()!, typeArguments, methodArguments) + "[]";
            if (type.IsGenericType)
            {
                string definition = type.GetGenericTypeDefinition().FullName!;
                definition = definition.Substring(0, definition.IndexOf('`'));
                return definition.Replace('+', '.') + "{" + string.Join(",", type.GetGenericArguments().Select(a => TypeName(a, typeArguments, methodArguments))) + "}";
            }

            return (type.FullName ?? type.Name).Replace('+', '.');
        }
    }
}

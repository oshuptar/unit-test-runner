using System.Reflection;
using System.Runtime.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace MiniTestRunner;

internal class Program
{

    private static Type GetAttributeTypeByName(string attributeName, Assembly referencedAssembly)
    {
        return referencedAssembly.GetTypes().Where(type => type.Name.Equals(attributeName)).Single();
    }

    private class KeyValuePair
    {
        public int Key;
        public MethodInfo? Value;

        public KeyValuePair(int Key, MethodInfo Value)
        {
            (this.Key, this.Value) = (Key, Value);
        }
    }

    private static void LoadReferencedAssemblies(Assembly assembly, AssemblyLoadContext loadContext, string baseDirectory)
    {
        foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
        {
            string referencedAssemblyPath = Path.Combine(baseDirectory, $"{referencedAssemblyName.Name}.dll");
            if (File.Exists(referencedAssemblyPath))
            {
                loadContext.LoadFromAssemblyPath(referencedAssemblyPath);
            }
        }
    }

    static void Main(string[] args)
    {
        foreach (var path in args)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException();
            }


            AssemblyLoadContext loadContext = new AssemblyLoadContext("loadContext", isCollectible: true);
            Assembly assembly = loadContext.LoadFromAssemblyPath(path);

            Console.WriteLine(assembly.FullName);

            string baseDirectory = Path.GetDirectoryName(path);
            LoadReferencedAssemblies(assembly, loadContext, baseDirectory); // Loads dependencies

            Assembly referencedAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(assemblies => assemblies.GetName().Name.Equals("MiniTest")).Single();
         
            Type testClassAttribute = GetAttributeTypeByName("TestClassAttribute", referencedAssembly);
            Type[] types = assembly.GetTypes().Where(t => t.IsClass && t.IsDefined(testClassAttribute)).ToArray(); //retrieves test classes

            int total_failed = 0;
            int total_passed = 0;

            foreach (Type type in types)
            {
                int no_of_failed = 0;
                int no_of_passed = 0;

                Console.WriteLine($"Running tests from class {type.Name}");

                ConstructorInfo? parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);
                if (parameterlessConstructor is null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: No parameterless constructor found for {type.FullName}");
                    Console.ResetColor();
                    continue;
                }

                object instance = parameterlessConstructor.Invoke(null);

                var beforeEachMethod = type.GetMethods().Where(t => t.IsPublic &&
                t.IsDefined(GetAttributeTypeByName("BeforeEachAttribute", referencedAssembly))).SingleOrDefault();
                var afterEachMethod = type.GetMethods().Where(t => t.IsPublic
                && t.IsDefined(GetAttributeTypeByName("AfterEachAttribute", referencedAssembly))).SingleOrDefault();

                if (beforeEachMethod != null)
                {
                    Action beforeEachAction = (Action)Delegate.CreateDelegate(typeof(Action), instance, beforeEachMethod);
                    beforeEachAction?.Invoke();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: No BeforeEach method found for {type.FullName}");
                    Console.ResetColor();
                }

                Type dataRowAttributeType = GetAttributeTypeByName("DataRowAttribute", referencedAssembly);
                Type priorityAttribute = GetAttributeTypeByName("PriorityAttribute", referencedAssembly);

                var testMethods = type.GetMethods().Where(t => t.IsPublic
                && t.IsDefined(GetAttributeTypeByName("TestMethodAttribute", referencedAssembly)));

                List<KeyValuePair> priorityMethods = new List<KeyValuePair>();

                foreach (var testMethod in testMethods)
                {
                    Attribute? attr = testMethod.GetCustomAttribute(priorityAttribute);
                    if (attr is null)
                    {
                        priorityMethods.Add(new KeyValuePair(0, testMethod));
                    }
                    else
                    {
                        int priority = (int)attr.GetType().GetProperty("Priority").GetValue(attr);
                        KeyValuePair priorityMethod = new KeyValuePair(priority, testMethod);
                        priorityMethods.Add(priorityMethod);
                    }
                }

                IEnumerable<KeyValuePair> collection = priorityMethods;
                collection.OrderBy(t => t.Key).ThenBy(t => t.Value.Name);
                foreach (var pair in collection)
                {
                    bool failed = false;

                    var method = pair.Value;
                    ParameterInfo[] methodParameters = method.GetParameters();
                    IEnumerable<Attribute> dataRowAttributes = method.GetCustomAttributes().Where(attr => attr.GetType().Equals(dataRowAttributeType));
                    foreach (var dataRowAttribute in dataRowAttributes)
                    {
                        PropertyInfo? dataProperty = dataRowAttribute.GetType().GetProperty("Data");
                        if (dataProperty is null)
                            continue;

                        ParameterInfo[] attributeParameters = dataProperty.GetIndexParameters();
                        if (methodParameters.Length != attributeParameters.Length)
                            continue;

                        bool match = true;
                        for (int i = 0; i < attributeParameters.Length; i++)
                        {
                            if (methodParameters[i].ParameterType != attributeParameters[i].ParameterType)
                            {
                                match = false;
                                break;
                            }
                        }

                        if (!match)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Warning : Parameter mismatch for {method.Name}");
                            Console.ResetColor();
                            continue;
                        }
                        try
                        {
                            if (attributeParameters.Length == 0)
                                method.Invoke(instance, null);
                            else
                            {
                                method.Invoke(instance, dataProperty.GetValue(dataRowAttribute) as object[]);
                            }
                        }
                        catch (Exception ex)
                        {
                            failed = true;
                            no_of_failed++;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{method.Name} - {dataProperty.GetValue(dataRowAttribute)}: FAILED\n  {ex.Message}");
                            Console.ResetColor();
                        }
                    }

                    try
                    {
                        method.Invoke(instance, null);
                    }
                    catch (Exception ex)
                    {
                        failed = true;
                        no_of_failed++;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{method.Name} : FAILED\n  {ex.Message}");
                        Console.ResetColor();
                    }

                    if (!failed)
                    {
                        no_of_passed++;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"{method.Name} : PASSED");
                        Console.ResetColor();
                    }

                }

                if (afterEachMethod != null)
                {
                    Action afterEachAction = (Action)Delegate.CreateDelegate(typeof(Action), instance, afterEachMethod);
                    afterEachAction?.Invoke();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: No AfterEach method found for {type.FullName}");
                    Console.ResetColor();
                }

                Console.WriteLine("**********************");
                Console.WriteLine($"* Tests passed - {no_of_passed}/{no_of_failed + no_of_passed}*\n" +
                    $"*Failed - {no_of_failed}*");
                Console.WriteLine("**********************");

                total_failed += no_of_failed;
                total_passed += no_of_passed;

            }

            Console.WriteLine("####################################");
            Console.WriteLine($"Summary of running tests from {assembly.FullName}");
            Console.WriteLine("**********************");
            Console.WriteLine($"* Tests passed - {total_passed}/{total_failed + total_passed}*\n" +
                $"*Failed - {total_failed}*");
            Console.WriteLine("**********************");
            loadContext.Unload();
        }
    }
}

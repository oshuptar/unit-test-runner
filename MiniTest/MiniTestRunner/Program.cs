using System.Reflection;
using System.Runtime.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace MiniTestRunner;

internal class Program
{
    private class KeyValuePair
    {
        public int Key;
        public MethodInfo? Value;

        public KeyValuePair(int Key, MethodInfo Value)
        {
            (this.Key, this.Value) = (Key, Value);
        }
    }

    private static Type GetAttributeTypeByName(string attributeName, Assembly referencedAssembly) //helper method to extract the type of the attribute given its name
    {
        return referencedAssembly.GetTypes().Where(type => type.Name.Equals(attributeName)).Single();
    }

    private static void LoadReferencedAssemblies(Assembly assembly, AssemblyLoadContext loadContext, string baseDirectory) //helper method to resolve dependencies
    {
        foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
        {
            string referencedAssemblyPath = Path.Combine(baseDirectory, $"{referencedAssemblyName.Name}.dll");
            if (File.Exists(referencedAssemblyPath))
            {
                Assembly reference = loadContext.LoadFromAssemblyPath(referencedAssemblyPath);
                LoadReferencedAssemblies(reference, loadContext, baseDirectory);
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


            AssemblyLoadContext loadContext = new AssemblyLoadContext("loadContext", isCollectible: true); // creating my own load context
            Assembly assembly = loadContext.LoadFromAssemblyPath(path);

            string baseDirectory = Path.GetDirectoryName(path);
            LoadReferencedAssemblies(assembly, loadContext, baseDirectory); // Loads dependencies

            Assembly referencedAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(assemblies => assemblies.GetName().Name.Equals("MiniTest")).Single(); // extract the assembly of MiniTest library

            Type testClassAttribute = GetAttributeTypeByName("TestClassAttribute", referencedAssembly);
            Type[] types = assembly.GetTypes().Where(t => t.IsClass && t.IsDefined(testClassAttribute)).ToArray(); //retrieves test classes

            int total_failed = 0;
            int total_passed = 0;

            foreach (Type type in types)
            {
                int no_of_failed = 0;
                int no_of_passed = 0;

                Console.WriteLine($"Running tests from class {type.Name}");

                ConstructorInfo? parameterlessConstructor = type.GetConstructor(Type.EmptyTypes); // Check for a parameterless constructor
                if (parameterlessConstructor is null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: No parameterless constructor found for {type.FullName}");
                    Console.ResetColor();
                    continue; // if not defined - continue
                }

                object instance = parameterlessConstructor.Invoke(null);

                var beforeEachMethod = type.GetMethods().Where(t => t.IsPublic &&
                t.IsDefined(GetAttributeTypeByName("BeforeEachAttribute", referencedAssembly))).SingleOrDefault(); //Extracts the method with beforeEach attribute
                var afterEachMethod = type.GetMethods().Where(t => t.IsPublic
                && t.IsDefined(GetAttributeTypeByName("AfterEachAttribute", referencedAssembly))).SingleOrDefault(); // Extracts the method with afterEach attribute

                if (beforeEachMethod != null)
                {
                    Action beforeEachAction = (Action)Delegate.CreateDelegate(typeof(Action), instance, beforeEachMethod); // binding a method to a delegate
                    beforeEachAction?.Invoke();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: No BeforeEach method found for {type.FullName}");
                    Console.ResetColor();
                }

                Type dataRowAttributeType = GetAttributeTypeByName("DataRowAttribute", referencedAssembly); // Retrieves the type of the attribute
                Type priorityAttribute = GetAttributeTypeByName("PriorityAttribute", referencedAssembly); // Retrieves the type of the attribute
                Type descriptionType = GetAttributeTypeByName("DescriptionAttribute", referencedAssembly); // Retrieves the type of the attribute

                var testMethods = type.GetMethods().Where(t => t.IsPublic
                && t.IsDefined(GetAttributeTypeByName("TestMethodAttribute", referencedAssembly))); // Extracting methods with TestMethod attribute

                List<KeyValuePair> priorityMethods = new List<KeyValuePair>(); // Used to store elements with their priorities

                foreach (var testMethod in testMethods)
                {
                    Attribute? attr = testMethod.GetCustomAttribute(priorityAttribute); // Retrieves the instance of Attribute of type PriorityAttribute
                    if (attr is null)
                    {
                        priorityMethods.Add(new KeyValuePair(0, testMethod)); // assigns 0 in case the attribute is not specified
                    }
                    else
                    {
                        int priority = (int)attr.GetType().GetProperty("Priority").GetValue(attr); // Retrieves priority of the attribute
                        KeyValuePair priorityMethod = new KeyValuePair(priority, testMethod);
                        priorityMethods.Add(priorityMethod);
                    }
                }

                IEnumerable<KeyValuePair> collection = priorityMethods.OrderBy(t => t.Key).ThenBy(t => t.Value.Name); // Sorts methods by priority value and Name
                foreach (var pair in collection)
                {
                    bool failed = false;
                    var method = pair.Value; // retrieves a specific method to be executed

                    Attribute? description = method.GetCustomAttribute(descriptionType);
                    Console.WriteLine($"{method.Name} - {description?.GetType().GetProperty("Description")?.GetValue(description)?.ToString() ?? "No Description provided"} ");

                    ParameterInfo[] methodParameters = method.GetParameters();
                    IEnumerable<Attribute> dataRowAttributes = method.GetCustomAttributes().Where(attr => Attribute.IsDefined(method, dataRowAttributeType)); // retrieves all DataRow Attributes
                    if (dataRowAttributes.ToArray().Length == 0)
                    {
                        Console.WriteLine("No data row parameters");
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

                    foreach (var dataRowAttribute in dataRowAttributes)
                    {
                        Console.WriteLine("Entered datarow attribute");

                        failed = false;
                        PropertyInfo? dataProperty = dataRowAttribute.GetType().GetProperty("Data"); // Retrives the value of the property data
                        if (dataProperty is null)
                        {
                            continue;
                        }

                        bool match = true;
                        //ParameterInfo[] attributeParameters = dataProperty.
                        object[]? attributeParameters = dataProperty.GetValue(dataRowAttribute) as object[];
                        if (attributeParameters is null)
                        {
                            Console.WriteLine("Cast failure");
                            continue;
                        }

                        Console.WriteLine($"Parameters length - {attributeParameters.Length}");


                        //if (methodParameters.Length != attributeParameters.Length)// Parameter match checking
                        //{
                        //    match = false;
                        //}
                        //for (int i = 0; i < attributeParameters.Length; i++) // Parameter match checking
                        //{
                        //    if () 
                        //    {
                        //        match = false;
                        //        break;
                        //    }
                        //}

                        if (!match)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Warning : Parameter mismatch for {method.Name}");
                            Console.ResetColor();
                            continue;
                        }

                        try // Call of the method with exception handling
                        {
                            if (attributeParameters.Length == 0)
                                method.Invoke(instance, null);
                            else
                            {
                                //var dataRowValue = dataProperty.GetValue(dataRowAttribute);
                                //object[] paramValues = new object[attributeParameters.Length];
                                //dataProperty.GetValue(dataRowAttribute, paramValues);
                                //method.Invoke(instance, paramValues);

                                method.Invoke(instance, attributeParameters);

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

                        if (!failed)
                        {
                            no_of_passed++;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"{method.Name} : PASSED");
                            Console.ResetColor();
                        }
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

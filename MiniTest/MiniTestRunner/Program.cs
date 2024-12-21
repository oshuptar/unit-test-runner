using System.Reflection;
using System.Runtime.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace MiniTestRunner;

internal class Program
{

    public static Type GetAttributeTypeByName(string attributeName, Assembly referencedAssembly)
    {
        return referencedAssembly.GetTypes().Where(type => type.FullName.Equals(attributeName)).Single();
    }

    static void Main(string[] args)
    {
        foreach(var path in args)
        {
            if(!File.Exists(path))
            {
                throw new FileNotFoundException();
            }

            AssemblyLoadContext loadContext = new AssemblyLoadContext("loadContext", isCollectible : true);
            //AssemblyLoadContext permanentLoadContext = AssemblyLoadContext.Default;
            Assembly assembly = loadContext.LoadFromAssemblyPath(path);
            Assembly referencedAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(assemblies => assemblies.GetName().Name.Equals("MiniTest")).Single();

            Type testClassAttribute = GetAttributeTypeByName("TestClassAttribute", referencedAssembly);
            Type[] types = assembly.GetTypes().Where(t => t.IsClass && t.IsDefined(testClassAttribute)).ToArray(); //retrieves test classes

            foreach(Type type in types)
            {
                ConstructorInfo? parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);
                if (parameterlessConstructor is null) {
                    Console.WriteLine($"Warning: No parameterless constructor found for {type.FullName}");
                    continue;
                }

                object instance = parameterlessConstructor.Invoke(Type.EmptyTypes);

                var beforeEachMethod = type.GetMethods().Where(t => t.IsPublic &&
                t.IsDefined(GetAttributeTypeByName("BeforeEachAttribute", referencedAssembly))).SingleOrDefault();
                var afterEachMethod = type.GetMethods().Where(t => t.IsPublic 
                && t.IsDefined(GetAttributeTypeByName("AfterEachAttribute", referencedAssembly))).SingleOrDefault();

                if(beforeEachMethod is null)
                    Console.WriteLine($"Warning: No BeforeEach method found for {type.FullName }");
                if(afterEachMethod is null)
                    Console.WriteLine($"Warning: No AfterEach method found for {type.FullName}");

                Action[] actions = {(Action)Delegate.CreateDelegate(typeof(Action), instance, beforeEachMethod),
                    (Action)Delegate.CreateDelegate(typeof(Action), instance, afterEachMethod)};

                actions[0]?.Invoke();

                Type dataRowAttributeType = GetAttributeTypeByName("DataRowAttribute", referencedAssembly);
                var methods = type.GetMethods().Where(t => t.IsPublic
                && t.IsDefined(dataRowAttributeType));

                foreach(MethodInfo method in methods)
                {
                    ParameterInfo[] methodParameters = method.GetParameters();
                    IEnumerable<Attribute> dataRowAttributes = method.GetCustomAttributes().Where(attr => attr.GetType().Equals(dataRowAttributeType));
                    foreach(var dataRowAttribute in dataRowAttributes)
                    {
                        PropertyInfo? dataProperty = dataRowAttribute.GetType().GetProperty("Data");
                        if (dataProperty is null)
                            continue;
                        ParameterInfo[] attributeParameters = dataProperty.GetIndexParameters();
                        if (methodParameters.Length != attributeParameters.Length)
                            continue;

                        bool match = true;
                        for(int i = 0; i < attributeParameters.Length; i++)
                        {
                            if (methodParameters[i].ParameterType != attributeParameters[i].ParameterType)
                            {
                                match = false;
                                break;
                            }
                        }

                        if (!match)
                        {
                            Console.WriteLine($"Warning : Parameter mismatch for {method.Name}");
                            continue;
                        }

                        
                    }
                    
                }

                actions[1]?.Invoke();

            }
            loadContext.Unload();
        }   
    }
}

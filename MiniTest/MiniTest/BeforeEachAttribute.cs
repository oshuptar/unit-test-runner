using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniTest;

[AttributeUsage(AttributeTargets.Method)]
public class BeforeEachAttribute : Attribute
{
    public object[]? Data { get; } = null;
    public string? Description { get; } = null;

    public BeforeEachAttribute(object[] data, string description = "")
    {
        Data = data;
        Description = description;
    }

}

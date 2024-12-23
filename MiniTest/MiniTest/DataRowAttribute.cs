using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniTest;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DataRowAttribute : Attribute
{
    public object?[] Data { get; }
    public string? Description { get; set; }

    public DataRowAttribute(string description, params object[] data)
    {
        this.Data = data;
        this.Description = description;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniTest;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DataRowAttribute : Attribute
{
    public object?[] Data { get; set; }
    
    public string? Description { get; set; }

    public DataRowAttribute()
    {
        this.Data = [null];
    }

    public DataRowAttribute(params object[] data)
    {
        this.Data = data;
    }
}

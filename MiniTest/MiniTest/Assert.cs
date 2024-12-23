using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;



namespace MiniTest;

public static class Assert
{
    public static void ThrowsException<TException>(Action action, string message = "")
    {
        bool thrown = false;
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            thrown = true;
            if (ex.GetType() != typeof(TException))
            {
                string exceptionMessage = String.Format($"Expected exception type:<{typeof(TException)}>. Actual exception type:<{ex.GetType()}>. {message}");
                throw new AssertionException(exceptionMessage);
            }
        }
        if (!thrown)
        {
            string exceptionMessage = String.Format($"Expected exception type:<{typeof(TException)}> but no exception was thrown. {message}");
            throw new AssertionException(exceptionMessage);
        }
    }

    public static void AreEqual<T>(T? expected, T? actual, string message = "")
    {
        if (expected != null && actual != null)
        {
            if (!expected.Equals(actual))
            {
                string exceptionMessage = string.Format($"Expected: {expected?.ToString() ?? "null"}. Actual: {actual?.ToString() ?? "null"}. {message}");
                throw new AssertionException(exceptionMessage);
            }
        }
        if((expected is null && actual is not null) || (expected is not null && actual is null))
        {
            string exceptionMessage = string.Format($"Expected: {expected?.ToString() ?? "null"}. Actual: {actual?.ToString() ?? "null"}. {message}");
            throw new AssertionException(exceptionMessage);
        }
    }
    public static void AreNotEqual<T>(T? notExpected, T? actual, string message = "")
    {
        string excpetionMessage = String.Format($"Expected any value except: {notExpected?.ToString() ?? "null"}. Actual: {actual?.ToString() ?? "null"}. {message}");
        if (notExpected != null && actual != null)
        {
            if (notExpected.Equals(actual))
            {
                string exceptionMessage = string.Format($"Expected: {notExpected?.ToString() ?? "null"}. Actual: {actual?.ToString() ?? "null"}. {message}");
                throw new AssertionException(exceptionMessage);
            }
        }
        if(notExpected is null && actual is null)
        {
            string exceptionMessage = string.Format($"Expected: {notExpected?.ToString() ?? "null"}. Actual: {actual?.ToString() ?? "null"}. {message}");
            throw new AssertionException(exceptionMessage);
        }
    }

    public static void IsTrue(bool condition, string message = "")
    {
        if (!condition)
        {
            throw new AssertionException($"Expected condition is true. {message}");
        }
    }

    public static void IsFalse(bool condition, string message = "")
    {
        if (condition)
        {
            throw new AssertionException($"Expected condition is false. {message}");
        }
    }

    public static void Fail(string message = "")
    {
        throw new AssertionException(message);
    }

}

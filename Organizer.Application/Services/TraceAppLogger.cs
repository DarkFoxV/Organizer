using System;
using System.Diagnostics;

namespace Organizer.Application.Services;

public sealed class TraceAppLogger : IAppLogger
{
    public void Info(string message)
    {
        Write($"[Organizer] {message}");
    }

    public void Error(string message, Exception exception)
    {
        Write($"[Organizer] {message}: {exception}");
    }

    private static void Write(string message)
    {
        Trace.WriteLine(message);
        Console.WriteLine(message);
    }
}

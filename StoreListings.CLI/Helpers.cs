using System.Runtime.InteropServices;

namespace StoreListings.CLI;

internal static class Helpers
{
    public static void WriteLoadingProgressBar()
    {
        Console.WriteLine("\x1b]9;4;3;0\x07");
    }

    public static void HideProgressBar()
    {
        Console.WriteLine("\x1b]9;4;0;0\x07");
    }

    public static void WriteError(Exception exception, string action)
    {
        HideProgressBar();
        if (exception is OperationCanceledException)
            return;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"An error occurred while {action}.");
        Console.WriteLine(exception.Message);
        Console.ResetColor();
    }

    public static void WriteField(ReadOnlySpan<char> fieldName, ReadOnlySpan<char> fieldValue)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Out.Write(fieldName);
        Console.Write(": ");
        Console.ResetColor();
        Console.Out.WriteLine(fieldValue);
    }
}

using System.Runtime.InteropServices;

namespace StoreListings.CLI;

internal static class Helpers
{
    public static void WriteLoadingProgressBar()
    {
        ReadOnlySpan<byte> loadingProc = [0x1B, 0x00, 0x5D, 0x00, 0x39, 0x00, 0x3B, 0x00, 0x34, 0x00, 0x3B, 0x00, 0x33, 0x00, 0x3B, 0x00, 0x30, 0x00, 0x7, 0x00];
        Console.Out.Write(MemoryMarshal.Cast<byte, char>(loadingProc));
    }

    public static void HideProgressBar()
    {
        ReadOnlySpan<byte> loadingProc = [0x1B, 0x00, 0x5D, 0x00, 0x39, 0x00, 0x3B, 0x00, 0x34, 0x00, 0x3B, 0x00, 0x30, 0x00, 0x3B, 0x00, 0x30, 0x00, 0x7, 0x00];
        Console.Out.Write(MemoryMarshal.Cast<byte, char>(loadingProc));
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

namespace StoreListings.Library.Internal;

internal static class CorrelationVector
{
    private static string baseVector;
    private static int currentVector;

    private const string base64CharSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    private const int id0Length = 16;

    internal enum Settings
    {
        SYNCREFRESHINTERVAL,
        QUEUEDRAININTERVAL,
        SNAPSHOTSCHEDULEINTERVAL,
        MAXEVENTSIZEINBYTES,
        MAXEVENTSPERPOST,
        SAMPLERATE,
        MAXFILESSPACE,
        UPLOADENABLED,
        PERSISTENCE,
        LATENCY,
        HTTPTIMEOUTINTERVAL,
        THREADSTOUSEWITHEXECUTOR,
        MAXCORRELATIONVECTORLENGTH,
        MAXCRITICALCANADDATTEMPTS,
        MAXRETRYPERIOD,
        BASERETRYPERIOD,
        CONSTANTFORRETRYPERIOD,
        NORMALEVENTMEMORYQUEUESIZE,
        CLLSETTINGSURL,
        HOSTSETTINGSETAG,
        CLLSETTINGSETAG,
        VORTEXPRODURL
    }

    static CorrelationVector()
    {
        baseVector = SeedCorrelationVector();
        currentVector = 1;
    }

    private static int getCllSettingsAsInt(Settings setting)
    {
        int asInt = (int)setting;
        return asInt;
    }

    private static bool CanExtend()
    {
        int vectorSize = (int)Math.Floor(Math.Log10(currentVector) + 1);

        return baseVector.Length + 1 + vectorSize + 1 + 1 <= getCllSettingsAsInt(Settings.MAXCORRELATIONVECTORLENGTH);
    }

    private static bool CanIncrement(int newVector)
    {
        if (newVector - 1 == int.MaxValue)
        {
            return false;
        }

        int vectorSize = (int)double.Floor(double.Log10(newVector) + 1);

        // Get the length of the existing string + length of the new extension + the length of the dot
        return baseVector.Length + vectorSize + 1 <= getCllSettingsAsInt(Settings.MAXCORRELATIONVECTORLENGTH);
    }

    internal static string Extend()
    {
        if (CanExtend())
        {
            baseVector = GetValue();
            currentVector = 1;
        }

        return GetValue();
    }

    internal static string GetValue()
    {
        return $"{baseVector}.{currentVector}";
    }

    internal static string Increment()
    {
        int newVector = currentVector + 1;
        // Check if we can increment
        if (CanIncrement(newVector))
        {
            currentVector = newVector;
        }

        return GetValue();
    }

    private static bool IsValid(string vector)
    {
        if (vector.Length > getCllSettingsAsInt(Settings.MAXCORRELATIONVECTORLENGTH))
        {
            return false;
        }

        string validationPattern = $"^[{base64CharSet}]{{16}}(.[0-9]+)+$";
        return vector == validationPattern;
    }

    private static string SeedCorrelationVector()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        return string.Create<object>(id0Length, null, (span, _) =>
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        {
            for (int i = 0; i < id0Length; i++)
                span[i] = base64CharSet[Random.Shared.Next(base64CharSet.Length)];
        });
    }

    internal static void SetValue(string vector)
    {
        if (IsValid(vector))
        {
            int lastDot = vector.LastIndexOf('.');
            baseVector = vector[..lastDot];
            currentVector = int.Parse(vector.AsSpan()[(lastDot + 1)..]);
        }
        else
        {
            throw new Exception("Cannot set invalid correlation vector value");
        }
    }
}
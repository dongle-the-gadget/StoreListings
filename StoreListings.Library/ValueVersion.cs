using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StoreListings.Library;

public readonly struct Version : IComparable, IComparable<Version?>, IEquatable<Version>, ISpanParsable<Version>
{
    public uint Major { get; }

    public uint Minor { get; }

    public uint Build { get; }

    public uint Revision { get; }

    public Version(uint major, uint minor, uint build, uint revision)
    {
        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
    }

    public readonly override string ToString()
    {
        return $"{Major}.{Minor}.{Build}.{Revision}";
    }

    public readonly ulong AsWindowsRepresentation()
    {
        return ((ulong)Major << 48) | ((ulong)Minor << 32) | ((ulong)Build << 16) | (ulong)Revision;
    }

    public static Version FromWindowsRepresentation(ulong value)
    {
        return new Version(
            (uint)((value & 0xFFFF000000000000) >> 48),
            (uint)((value & 0x0000FFFF00000000) >> 32),
            (uint)((value & 0x00000000FFFF0000) >> 16),
            (uint)(value & 0x000000000000FFFF)
        );
    }

    public static Version Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return ParseVersion(s, throwOnFailure: true)!.Value;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out Version result)
    {
        Version? parsedVersion = ParseVersion(s, false);
        result = parsedVersion.GetValueOrDefault();
        return parsedVersion.HasValue;
    }

    public static Version Parse(string s, IFormatProvider? provider = null)
    {
        return ParseVersion(s, throwOnFailure: true)!.Value;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Version result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = default;
            return false;
        }
        Version? parsedVersion = ParseVersion(s, false);
        result = parsedVersion.GetValueOrDefault();
        return parsedVersion.HasValue;
    }

    private static Version? ParseVersion(ReadOnlySpan<char> input, bool throwOnFailure)
    {
        // Find the separator between major and minor.  It must exist.
        int majorEnd = input.IndexOf('.');
        if (majorEnd < 0)
        {
            if (throwOnFailure) throw new ArgumentException("Invalid version string.", nameof(input));
            return null;
        }

        // Find the ends of the optional minor and build portions.
        // We musn't have any separators after build.
        int buildEnd = -1;
        int minorEnd = input.Slice(majorEnd + 1).IndexOf('.');
        if (minorEnd >= 0)
        {
            minorEnd += (majorEnd + 1);
            buildEnd = input.Slice(minorEnd + 1).IndexOf('.');
            if (buildEnd >= 0)
            {
                buildEnd += (minorEnd + 1);
                if (input.Slice(buildEnd + 1).Contains('.'))
                {
                    if (throwOnFailure) throw new ArgumentException("Version string portion was too short or too long.", nameof(input));
                    return null;
                }
            }
        }

        uint minor, build, revision;

        // Parse the major version
        if (!TryParseComponent(input.Slice(0, majorEnd), nameof(input), throwOnFailure, out uint major))
        {
            return null;
        }

        if (minorEnd != -1)
        {
            // If there's more than a major and minor, parse the minor, too.
            if (!TryParseComponent(input.Slice(majorEnd + 1, minorEnd - majorEnd - 1), nameof(input), throwOnFailure, out minor))
            {
                return null;
            }

            if (buildEnd != -1)
            {
                // major.minor.build.revision
                return
                    TryParseComponent(input.Slice(minorEnd + 1, buildEnd - minorEnd - 1), nameof(build), throwOnFailure, out build) &&
                    TryParseComponent(input.Slice(buildEnd + 1), nameof(revision), throwOnFailure, out revision) ?
                        new Version(major, minor, build, revision) :
                        null;
            }
            else
            {
                // major.minor.build
                return TryParseComponent(input.Slice(minorEnd + 1), nameof(build), throwOnFailure, out build) ?
                    new Version(major, minor, build, 0) :
                    null;
            }
        }
        else
        {
            // major.minor
            return TryParseComponent(input.Slice(majorEnd + 1), nameof(input), throwOnFailure, out minor) ?
                new Version(major, minor, 0, 0) :
                null;
        }
    }

    private static bool TryParseComponent(ReadOnlySpan<char> component, string componentName, bool throwOnFailure, out uint parsedComponent)
    {
        if (throwOnFailure)
        {
            parsedComponent = uint.Parse(component, NumberStyles.Integer, CultureInfo.InvariantCulture);
            ArgumentOutOfRangeException.ThrowIfNegative(parsedComponent, componentName);
            return true;
        }

        return uint.TryParse(component, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedComponent) && parsedComponent >= 0;
    }

    public static bool operator ==(Version left, Version right) => left.Equals(right);

    public static bool operator !=(Version left, Version right) => !left.Equals(right);

    public static bool operator <(Version left, Version right) => left.CompareTo(right) < 0;

    public static bool operator <=(Version left, Version right) => left.CompareTo(right) <= 0;

    public static bool operator >(Version left, Version right) => left.CompareTo(right) > 0;

    public static bool operator >=(Version left, Version right) => left.CompareTo(right) >= 0;

    public bool Equals(Version other)
    {
        return Major == other.Major && Minor == other.Minor && Build == other.Build && Revision == other.Revision;
    }

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Build, Revision);

    public override bool Equals(object? obj)
    {
        return obj is Version version && Equals(version);
    }

    public int CompareTo(object? version)
    {
        if (version is null)
        {
            return 1;
        }

        if (version is Version v)
        {
            return CompareTo(v);
        }

        throw new ArgumentException("Object must be of type Version.", nameof(version));
    }

    public int CompareTo(Version? other)
    {
        if (!other.HasValue)
            return 1;

        ref readonly Version version = ref Nullable.GetValueRefOrDefaultRef(ref other);

        if (Major != version.Major)
            return Major > version.Major ? 1 : -1;

        if (Minor != version.Minor)
            return Minor > version.Minor ? 1 : -1;

        if (Build != version.Build)
            return Build > version.Build ? 1 : -1;

        if (Revision != version.Revision)
            return Revision > version.Revision ? 1 : -1;

        return 0;
    }
}
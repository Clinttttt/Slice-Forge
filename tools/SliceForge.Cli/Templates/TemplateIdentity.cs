using System.Reflection;

namespace SliceForge.Cli.Templates;

internal static class TemplateIdentity
{
    public const string PackageId = "SliceForge.Templates";
    public const string ShortName = "sliceforge-api";

    public static string Version
    {
        get
        {
            string? informationalVersion = typeof(TemplateIdentity).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (string.IsNullOrWhiteSpace(informationalVersion))
            {
                return "unknown";
            }

            int metadataSeparator = informationalVersion.IndexOf('+');
            return metadataSeparator >= 0
                ? informationalVersion[..metadataSeparator]
                : informationalVersion;
        }
    }

    public static string InstallCommand => $"dotnet new install {PackageId}@{Version}";
}

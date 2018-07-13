#addin nuget:?package=Cake.FileHelpers&version=3.0.0

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var tag = Argument("tag", "5.12.0.273");

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("DownloadSource")
    .Does(() =>
{
    var GetSourceFiles = new Func<DirectoryPath, string, string[]>((dest, url) => {
        EnsureDirectoryExists(dest);
        var file = url.Substring(url.LastIndexOf("/") + 1);
        var dir = url.Substring(0, url.LastIndexOf("/"));
        var destFile = dest.CombineWithFilePath(file);
        if (!FileExists(destFile)) {
            Information($"Downloading '{file}' to '{dest}'...");
            DownloadFile(url, destFile);
        }
        return FileReadLines(destFile)
            .Where(f => !f.StartsWith("."))
            .Select(f => $"{dir}/{f}")
            .ToArray();
    });

    var DownloadFiles = new Action<DirectoryPath, string[]>((dest, urls) => {
        EnsureDirectoryExists(dest);
        foreach (var url in urls) {
            var file = url.Substring(url.LastIndexOf("/") + 1);
            var destFile = dest.CombineWithFilePath(file);
            if (!FileExists(destFile)) {
                Information($"Downloading '{file}' to '{dest}'...");
                DownloadFile(url, destFile);
            }
        }
    });

    Information("Downloading sources...");

    var rootUrl = $"https://github.com/mono/mono/raw/mono-{tag}";

    // mono-api-info
    var MonoApiInfoSources = GetSourceFiles(
        "externals/mono-api-info", 
        $"{rootUrl}/mcs/tools/corcompare/mono-api-info.exe.sources");
    DownloadFiles("externals/mono-api-info", MonoApiInfoSources);

    // mono-api-diff
    var MonoApiDiffSources = new [] {
        $"{rootUrl}/mcs/tools/corcompare/mono-api-diff.cs",
    };
    DownloadFiles("externals/mono-api-diff", MonoApiDiffSources);

    // mono-api-html
    var MonoApiHtmlSources = GetSourceFiles(
        "externals/mono-api-html", 
        $"{rootUrl}/mcs/tools/mono-api-html/mono-api-html.exe.sources");
    DownloadFiles("externals/mono-api-html", MonoApiHtmlSources);

    Information("All sources downloaded.");
});

Task("Build")
    .IsDependentOn("DownloadSource")
    .Does(() =>
{
    MSBuild("source/mono-api-tools.sln", new MSBuildSettings {
        Configuration = configuration,
        Verbosity = Verbosity.Minimal,
        Restore = true,
    });

    EnsureDirectoryExists("output/tools");
    CopyFiles($"source/*/bin/{configuration}/*/*.dll", "output/tools");
    CopyFiles($"source/*/bin/{configuration}/*/*.exe", "output/tools");
});

Task("NuGet")
    .IsDependentOn("Build")
    .Does(() =>
{
    EnsureDirectoryExists("output");

    // build the "preview" nuget
    NuGetPack("nuget/mono-api-tools.nuspec", new NuGetPackSettings {
        Version = tag + "-preview",
        BasePath = ".",
        OutputDirectory = "output",
    });

    // build the "stable" nuget
    NuGetPack("nuget/mono-api-tools.nuspec", new NuGetPackSettings {
        Version = tag,
        BasePath = ".",
        OutputDirectory = "output",
    });
});

Task("Default")
    .IsDependentOn("DownloadSource")
    .IsDependentOn("Build")
    .IsDependentOn("NuGet")
    .Does(() =>
{
});

RunTarget(target);
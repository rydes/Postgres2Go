// Tools
#tool                   "xunit.runner.console"
#tool "nuget:?package=GitVersion.CommandLine"

// Parameters
var configuration       = Argument("Configuration", "Release");
var target              = Argument("Target", "Default");

// Variables
GitVersion versionInfo = null;

var outputDir           = Directory("./artifacts");
var projectDir          = GetFiles("./src/Postgres2Go/*.csproj").FirstOrDefault();
var solution            = GetFiles("./src/*.sln").FirstOrDefault();
var tests               = GetFiles("./src/Postgres2Go.Tests/*.csproj").FirstOrDefault();
var testResultsDir      = Directory("./test-results/");


// Tasks
Task("Clean")
    .Does(() => {
        Information("Removing previous build artifacts");
        
        if(DirectoryExists(testResultsDir.Path))
            DeleteDirectory(testResultsDir.Path, 
                new DeleteDirectorySettings()
                {
                    Recursive = true
                });
        
        if(DirectoryExists(outputDir.Path))
            DeleteDirectory(outputDir.Path, 
                new DeleteDirectorySettings()
                {
                    Recursive = true
                });
    });

Task("Prepare-Directories")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        Information("Prepare directory for tests output");
        EnsureDirectoryExists(testResultsDir.Path);

        Information("Prepare directory for lib output");
        EnsureDirectoryExists(outputDir.Path);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Prepare-Directories")
    .Does(() => {
        Information("Restoring nuget packages for solution");
        DotNetCoreRestore(solution.FullPath);
    });

Task("Get-Version-Info")
    .Does(() => {
        Information("Get version:");
        
        versionInfo = GitVersion(
            new GitVersionSettings {
                UpdateAssemblyInfo = true,
                OutputType = GitVersionOutput.Json
            });
        
        Information("Version.FullSemVer = " + versionInfo.FullSemVer);
        Information("Version.InformationalVersion = " + versionInfo.InformationalVersion);
        Information("Version.LegacySemVer = " + versionInfo.LegacySemVer);
        Information("Version.LegacySemVerPadded = " + versionInfo.LegacySemVerPadded);
        Information("Version.MajorMinorPatch = " + versionInfo.MajorMinorPatch);
        Information("Version.NuGetVersion = " + versionInfo.NuGetVersion);
        Information("Version.NuGetVersionV2 = " + versionInfo.NuGetVersionV2);
        Information("Version.PreReleaseLabel = " + versionInfo.PreReleaseLabel);
        Information("Version.PreReleaseTag = " + versionInfo.PreReleaseTag);
        Information("Version.PreReleaseTagWithDash = " + versionInfo.PreReleaseTagWithDash);
        Information("Version.SemVer = " + versionInfo.SemVer);
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Get-Version-Info")
    .Does(() => {
        Information("Build solution");
        DotNetCoreBuild(solution.FullPath,
            new DotNetCoreBuildSettings()
            {
                Configuration = configuration,
                ArgumentCustomization = args => args
                    .Append("--no-restore")
                    .Append("/p:SemVer=" + versionInfo.NuGetVersion) ,

            });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        
        var resultsFile = $"..\\..\\..\\test-results\\{tests.GetFilenameWithoutExtension()}.xml";
        var testProject = tests.FullPath;
        
        Information($"Run tests of {tests.GetFilenameWithoutExtension()}");

        DotNetCoreTest(testProject,
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = true,
                    ArgumentCustomization = args => args
                        .Append($"-r {testResultsDir.Path.FullPath}")
                        .Append($"-l trx;logfilename={resultsFile}")
                });

    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() => {
                
        Information("Creating nuget package");
        DotNetCorePack(
                projectDir.GetDirectory().FullPath,
                new DotNetCorePackSettings()
                {
                    Configuration = configuration,
                    NoBuild = true,
                    OutputDirectory = outputDir.Path,
                    ArgumentCustomization = args=> args
                        .Append(" --include-symbols")
                        .Append("/p:PackageVersion=" + versionInfo.NuGetVersion)
                });
    });

Task("Default")
    .IsDependentOn("Pack");

// Run
RunTarget(target);

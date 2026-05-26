var branch = GetArg(1);
if (string.IsNullOrWhiteSpace(branch))
{
	branch = InferSteamBranch();
}

Warn($"Branch: {branch}");
var useInstalledStagingManagedFiles = ShouldUseInstalledStagingManagedFiles();

DotNet.Run("build", PathEnquotes(Home, "tools", "depot", "DepotDownloader"));
DotNet.Run("build", PathEnquotes(Home, "src", "Carbon.Tools", "Carbon.Publicizer"));
DotNet.Run("build", PathEnquotes(Home, "src", "Carbon.Tools", "Carbon.Generator"));

System.Threading.Tasks.Task.WaitAll(
    System.Threading.Tasks.Task.Run(() => DownloadRustFiles("windows")),
    System.Threading.Tasks.Task.Run(() => DownloadRustFiles("linux"))
);

void DownloadRustFiles(string platform)
{
	var managedPath = Path(Home, "rust", platform, "RustDedicated_Data", "Managed");
	if (TryCopyInstalledStagingManagedFiles(platform, managedPath))
	{
		PublicizeRustFiles(managedPath);
		return;
	}

	Log($"Downloading {platform} Rust files..");
	DotNet.Run("run", "--no-build", "--project", PathEnquotes(Home, "tools", "depot", "DepotDownloader"),
		"-os", platform, 
		"-validate", 
		"-app 258550",
		"-branch", branch, 
		"-filelist", PathEnquotes(Home, "tools", "helpers", "258550_refs.txt"),
		"-dir", PathEnquotes(Home, "rust", platform));

	PublicizeRustFiles(managedPath);
}

void PublicizeRustFiles(string managedPath)
{
	var hash = Files.Hash(Path(managedPath, "Assembly-CSharp.dll")).ToString();
	Files.Create(Path(managedPath, ".hash"), hash);
	Log($"Assembly-CSharp = {hash} [hash]");

	DotNet.Run("run", "--no-build", "--project", PathEnquotes(Home, "src", "Carbon.Tools", "Carbon.Publicizer"),
		PathEnquotes(managedPath));
}

string InferSteamBranch()
{
	var gitBranch = Git.RunOutput("-C", Home, "rev-parse", "--abbrev-ref", "HEAD").Trim();

	return gitBranch switch
	{
		"rust_beta/staging" => "staging",
		"rust_beta/aux01" => "aux01-staging",
		"rust_beta/aux02" => "aux02-staging",
		"rust_beta/aux03" => "aux03-staging",
		_ => "release"
	};
}

bool TryCopyInstalledStagingManagedFiles(string platform, string destinationManagedPath)
{
	if (!useInstalledStagingManagedFiles || !string.Equals(platform, "linux", StringComparison.OrdinalIgnoreCase))
	{
		return false;
	}

	var installedManagedPath = GetVariable("CARBON_STAGING_MANAGED_PATH");
	if (string.IsNullOrWhiteSpace(installedManagedPath))
	{
		installedManagedPath = "/home/johnm/rust-staging-autoupdate/server/RustDedicated_Data/Managed";
	}

	if (!System.IO.Directory.Exists(installedManagedPath))
	{
		Error($"Installed staging managed path not found: {installedManagedPath}");
		Exit(1);
		return false;
	}

	var installedFiles = System.IO.Directory.GetFiles(installedManagedPath, "*.dll")
		.ToDictionary(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase);
	if (!installedFiles.ContainsKey("Assembly-CSharp.dll") || !installedFiles.ContainsKey("Facepunch.Console.dll"))
	{
		Error($"Installed staging managed path is missing required Rust DLLs: {installedManagedPath}");
		Exit(1);
		return false;
	}

	Warn($"Copying installed staging managed DLLs from: {installedManagedPath}");
	System.IO.Directory.CreateDirectory(destinationManagedPath);

	foreach (var file in installedFiles.Values)
	{
		System.IO.File.Copy(file, System.IO.Path.Combine(destinationManagedPath, System.IO.Path.GetFileName(file)), true);
	}

	Warn($"Copied {installedFiles.Count} installed staging DLLs.");
	return true;
}

bool ShouldUseInstalledStagingManagedFiles()
{
	return string.Equals(branch, "staging", StringComparison.OrdinalIgnoreCase);
}

var generatorRustManagedPath = useInstalledStagingManagedFiles
	? Path(Home, "rust", "linux", "RustDedicated_Data", "Managed")
	: Path(Home, "rust", "windows", "RustDedicated_Data", "Managed");

DotNet.Run("run", "--no-build", "--project", PathEnquotes(Home, "src", "Carbon.Tools", "Carbon.Generator"),
				  "--plugininput", PathEnquotes(Home, "src", "Carbon.Components", "Carbon.Common", "src", "Carbon", "CorePlugin"),
				  "--rust", PathEnquotes(generatorRustManagedPath));

var modules = new System.Collections.Generic.List<string>();
modules.AddRange(Directories.Get(Path(Home, "src", "Carbon.Components", "Carbon.Common", "src", "Carbon", "Modules")));
modules.AddRange(Directories.Get(Path(Home, "src", "Carbon.Components", "Carbon.Modules", "src")));

var modulePaths = string.Join(";", modules);
DotNet.Run("run", "--no-build", "--project", PathEnquotes(Home, "src", "Carbon.Tools", "Carbon.Generator"),
	"--plugininput", $"\"{modulePaths}\"",
	"--pluginnamespace", "Carbon.Modules",
	"--basename", "module",
	"--rust", PathEnquotes(generatorRustManagedPath));

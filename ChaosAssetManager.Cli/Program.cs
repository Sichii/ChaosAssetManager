using System.CommandLine;
using ChaosAssetManager.Cli.Commands;

var archiveArgument = new Argument<FileInfo>("archive")
{
    Description = "path to the .dat archive"
}.AcceptExistingOnly();

var outputDirOption = new Option<DirectoryInfo>("--output", "-o")
{
    Description = "output directory (default: current directory)",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory())
};

//list
var extensionOption = new Option<string?>("--extension", "-e")
{
    Description = "only list entries with this extension (e.g. .epf)"
};

var listCommand = new Command("list", "list all entries in an archive")
{
    archiveArgument,
    extensionOption
};

listCommand.SetAction(
    parseResult => ArchiveCommandHandlers.List(
        parseResult.GetValue(archiveArgument)!,
        parseResult.GetValue(extensionOption)));

//extract-all
var extractAllCommand = new Command("extract-all", "extract every entry from an archive to a directory")
{
    archiveArgument,
    outputDirOption
};

extractAllCommand.SetAction(
    parseResult => ArchiveCommandHandlers.ExtractAll(
        parseResult.GetValue(archiveArgument)!,
        parseResult.GetValue(outputDirOption)!));

//extract-by-name
var namesArgument = new Argument<string[]>("names")
{
    Description = "one or more entry names to extract (e.g. mb001.epf)"
};

var extractByNameCommand = new Command("extract-by-name", "extract one or more named entries from an archive to a directory")
{
    archiveArgument,
    namesArgument,
    outputDirOption
};

extractByNameCommand.SetAction(
    parseResult => ArchiveCommandHandlers.ExtractByName(
        parseResult.GetValue(archiveArgument)!,
        parseResult.GetValue(namesArgument)!,
        parseResult.GetValue(outputDirOption)!));

//patch
var filesArgument = new Argument<FileInfo[]>("files")
{
    Description = "one or more files to patch into the archive, keyed by file name"
}.AcceptExistingOnly();

var patchOutputOption = new Option<FileInfo?>("--output", "-o")
{
    Description = "where to save the patched archive (default: overwrite the input archive in place)"
};

var patchCommand = new Command("patch", "patch one or more files into an archive and save it")
{
    archiveArgument,
    filesArgument,
    patchOutputOption
};

patchCommand.SetAction(
    parseResult => ArchiveCommandHandlers.Patch(
        parseResult.GetValue(archiveArgument)!,
        parseResult.GetValue(filesArgument)!,
        parseResult.GetValue(patchOutputOption)));

var rootCommand = new RootCommand("ChaosAssetManager CLI - manages .dat archives")
{
    listCommand,
    extractAllCommand,
    extractByNameCommand,
    patchCommand
};

return rootCommand.Parse(args).Invoke();

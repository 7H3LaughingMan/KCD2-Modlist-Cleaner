using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using Windows.Win32;

namespace KCD2;

partial class Program
{
    private static readonly NuGet.Versioning.SemanticVersion CurrentVersion = new(1, 0, 3);

    private static readonly VelopackLocator velopackLocator = VelopackLocator.GetDefault(null);

    [GeneratedRegex(@"<UsedMods>([\S\s]*)<\/UsedMods>")]
    private static partial Regex UsedMods();

    static void Main(string[] args)
    {
        VelopackApp
            .Build()
            .SetLocator(velopackLocator)
            .WithFirstRun(Exit)
            .WithAfterInstallFastCallback(CreateRegistry)
            .WithAfterUpdateFastCallback(CreateRegistry)
            .WithBeforeUninstallFastCallback(DeleteRegistry)
            .Run();

        Console.WriteLine($"KCD2 Modlist Cleaner v{CurrentVersion}");
        Console.WriteLine();

        if (args.Length == 1)
            ProccessFile(args[0]);

        Console.Write("Press any key to continue . . . ");
        Console.ReadKey();

        Update();
    }

    static void Exit(NuGet.Versioning.SemanticVersion _) => Environment.Exit(0);

    static unsafe void CreateRegistry(NuGet.Versioning.SemanticVersion _)
    {
        var applicationPath = Path.Combine(AppContext.BaseDirectory, "KCD2-Modlist-Cleaner.exe");

        var shell = Registry.CurrentUser.CreateSubKey(@"Software\Classes\SystemFileAssociations\.whs\shell\KCD2-Modlist-Cleaner");
        shell.SetValue("", "Clean Modlist");
        shell.SetValue("Icon", applicationPath);

        var command = shell.CreateSubKey("command");
        command.SetValue("", @$"""{applicationPath}"" ""%1""");

        PInvoke.SHChangeNotify(Windows.Win32.UI.Shell.SHCNE_ID.SHCNE_ASSOCCHANGED, Windows.Win32.UI.Shell.SHCNF_FLAGS.SHCNF_IDLIST);
    }

    static unsafe void DeleteRegistry(NuGet.Versioning.SemanticVersion _)
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.whs\shell\KCD2-Modlist-Cleaner");
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\.whs\shell\KCD2-Modlist-Cleaner");

        PInvoke.SHChangeNotify(Windows.Win32.UI.Shell.SHCNE_ID.SHCNE_ASSOCCHANGED, Windows.Win32.UI.Shell.SHCNF_FLAGS.SHCNF_IDLIST);
    }

    static void ProccessFile(string path)
    {
        if (!File.Exists(path) || Path.GetExtension(path) != ".whs")
        {
            Console.WriteLine("You must provide a KCD2 save file!");
            Console.WriteLine();
            return;
        }

        var fileInfo = new FileInfo(path);
        var directoryInfo = fileInfo.Directory;

        if (fileInfo.Length == 0)
        {
            Console.WriteLine("You appear to have provided a corrupted filed!");
            Console.WriteLine("Please provide a file that is not 0 KB.");
            return;
        }

        var backup = Path.Combine(directoryInfo.FullName, $"{fileInfo.Name}.bak");

        if (File.Exists(backup))
        {
            Console.WriteLine("You already have a backup of your save, please delete this before trying again.");
            Console.WriteLine(backup);
            Console.WriteLine();
            return;
        }

        fileInfo.CopyTo(backup);

        var bytes = File.ReadAllBytes(fileInfo.FullName);
        var fileHeader = BitConverter.ToUInt32(bytes, 0);

        if (fileHeader != uint.MaxValue)
        {
            Console.WriteLine("The save file you provided does not have the proper signature!");
            Console.WriteLine();
            return;
        }

        var saveDescriptionLength = BitConverter.ToInt32(bytes, 4);
        var saveDescription = Encoding.UTF8.GetString(bytes, 8, saveDescriptionLength);

        var modifiedSaveDescription = UsedMods().Replace(saveDescription, "<UsedMods></UsedMods>");

        using var fileStream = File.Open(fileInfo.FullName, FileMode.Create);

        fileStream.Write(BitConverter.GetBytes(uint.MaxValue));
        fileStream.Write(BitConverter.GetBytes(modifiedSaveDescription.Length));
        fileStream.Write(Encoding.UTF8.GetBytes(modifiedSaveDescription));
        fileStream.Write(bytes, 8 + saveDescriptionLength, bytes.Length - 8 - saveDescriptionLength);

        Console.WriteLine($"Cleaned {fileInfo.FullName}");
        Console.WriteLine();
    }

    static void Update()
    {
        if (velopackLocator.AppId is null)
            return;

        var manager = new UpdateManager(new GithubSource("https://github.com/7H3LaughingMan/KCD2-Modlist-Cleaner", null, false));

        var newVersion = manager.CheckForUpdates();
        if (newVersion is null)
            return;

        manager.DownloadUpdates(newVersion);
        manager.ApplyUpdatesAndExit(null);
    }
}

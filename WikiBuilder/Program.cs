using System.Text;

var entries = InternalConfigDef.GetConfigSectionsAndItems(@"C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\BepInEx\config\ShaosilGaming.GeneralImprovements.cfg");
Console.WriteLine($"Loaded {entries.Count} sections and {entries.Count} total entries.");
Console.WriteLine();

foreach (var section in entries.Where(e => e.Value.Any()))
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write($"Parsing section [{section.Key}]... ");

    var curWiki = new StringBuilder();
    curWiki.AppendLine("| Setting | Description | Accepted Values | Default |");
    curWiki.AppendLine("| --- | --- | --- | --- |");
    Func<string, string> replacePipes = s => s.Replace("|", "\\|");
    foreach (var item in section.Value)
    {
        curWiki.AppendLine($"| {replacePipes(item.Name)} | {replacePipes(item.Description)} | {replacePipes(item.AcceptableValuesDescription)} | {replacePipes(item.DefaultValue)} |");
    }

    string filePath = Path.Combine(Environment.CurrentDirectory, $@"..\..\..\Output\{section.Key}.txt");
    string contents = curWiki.ToString().Trim();
    if (File.Exists(filePath) && File.ReadAllText(filePath) == contents)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("No changes.");
    }
    else
    {
        File.WriteAllText(filePath, contents);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Done!");
    }
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.White;
Console.Write("Press any key to continue...");
Console.ReadKey();
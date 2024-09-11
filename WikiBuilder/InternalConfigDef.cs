using System.Text.RegularExpressions;

internal class InternalConfigDef
{
    public readonly string Section;
    public readonly string Description;
    public readonly string Name;
    public readonly string Value;
    public readonly string DefaultValue;

    public InternalConfigDef(string section, string description, string name, string value, string defaultValue)
    {
        Section = section;
        Description = description;
        Name = name;
        Value = value;
        DefaultValue = defaultValue;
    }

    internal static Dictionary<string, List<InternalConfigDef>> GetConfigSectionsAndItems(string filePath)
    {
        string[] configLines = File.ReadAllLines(filePath);
        var ourEntries = new Dictionary<string, List<InternalConfigDef>> { { string.Empty, new List<InternalConfigDef>() } };
        string curSection = string.Empty;
        string curDescription = string.Empty;
        string curDefaultValue = string.Empty;

        foreach (string line in configLines.Select(l => l.Trim()))
        {
            if (line.StartsWith('#'))
            {
                if (line.StartsWith("##")) curDescription = line.Substring(2);
                else
                {
                    var match = Regex.Match(line, "# Default value: (.+)");
                    if (match.Groups[1].Success)
                    {
                        curDefaultValue = match.Groups[1].Value;
                    }
                }
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                curSection = line.Substring(1, line.Length - 2);
                ourEntries.TryAdd(curSection, new List<InternalConfigDef>());
                continue;
            }

            string[] entry = line.Split('=');
            if (entry.Length == 2)
            {
                ourEntries[curSection].Add(new InternalConfigDef(curSection, curDescription, entry[0].Trim(), entry[1].Trim(), curDefaultValue));

                curDescription = string.Empty;
                curDefaultValue = string.Empty;
            }
        }

        return ourEntries;
    }
}
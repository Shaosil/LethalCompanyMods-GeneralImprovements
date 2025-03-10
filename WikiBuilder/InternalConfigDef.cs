using System.Text.RegularExpressions;

internal class InternalConfigDef(string section, string description, string settingType, string defaultValue, string acceptedValues, string name, string value)
{
    public string Section => section;
    public string Description => description;
    public string SettingType => settingType;
    public string DefaultValue => defaultValue;
    public string AcceptableValuesDescription => acceptedValues;
    public string Name => name;
    public string Value => value;

    internal static Dictionary<string, List<InternalConfigDef>> GetConfigSectionsAndItems(string filePath)
    {
        string[] configLines = File.ReadAllLines(filePath);
        var ourEntries = new Dictionary<string, List<InternalConfigDef>> { { string.Empty, new List<InternalConfigDef>() } };
        string curSection = string.Empty;
        string curDescription = string.Empty;
        string curSettingType = string.Empty;
        string curDefaultValue = string.Empty;
        string curAcceptedValues = string.Empty;

        foreach (string line in configLines.Select(l => l.Trim()))
        {
            if (line.StartsWith('#'))
            {
                if (line.StartsWith("##")) curDescription = line[2..];
                else
                {
                    var settingTypeMatch = Regex.Match(line, "# Setting type: (.+)");
                    var defaultMatch = Regex.Match(line, "# Default value: (.+)");
                    var acceptableMatch = Regex.Match(line, "# Accept.+: (.+)");
                    if (settingTypeMatch.Groups[1].Success)
                    {
                        curSettingType = settingTypeMatch.Groups[1].Value;

                        switch (curSettingType)
                        {
                            case "Boolean": curAcceptedValues = "true, false"; break;
                        }
                    }
                    else if (defaultMatch.Groups[1].Success)
                    {
                        curDefaultValue = defaultMatch.Groups[1].Value;
                    }
                    else if (acceptableMatch.Groups[1].Success)
                    {
                        curAcceptedValues = acceptableMatch.Groups[1].Value;
                    }
                }
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                curSection = line[1..^1];
                ourEntries.TryAdd(curSection, []);
                continue;
            }

            string[] entry = line.Split('=');
            if (entry.Length == 2)
            {
                ourEntries[curSection].Add(new InternalConfigDef(curSection, curDescription, curSettingType, curDefaultValue, curAcceptedValues, entry[0].Trim(), entry[1].Trim()));

                curDescription = string.Empty;
                curSettingType = string.Empty;
                curDefaultValue = string.Empty;
                curAcceptedValues = string.Empty;
            }
        }

        return ourEntries;
    }
}
using System.Text.RegularExpressions;

internal class InternalConfigDef
{
    public readonly string Section;
    public readonly string Description;
    public readonly string SettingType;
    public readonly string DefaultValue;
    public readonly string AcceptableValuesDescription;
    public readonly string Name;
    public readonly string Value;

    public InternalConfigDef(string section, string description, string settingType, string defaultValue, string acceptedValues, string name, string value)
    {
        Section = section;
        Description = description;
        SettingType = settingType;
        DefaultValue = defaultValue;
        AcceptableValuesDescription = acceptedValues;
        Name = name;
        Value = value;
    }

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
                if (line.StartsWith("##")) curDescription = line.Substring(2);
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
                curSection = line.Substring(1, line.Length - 2);
                ourEntries.TryAdd(curSection, new List<InternalConfigDef>());
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
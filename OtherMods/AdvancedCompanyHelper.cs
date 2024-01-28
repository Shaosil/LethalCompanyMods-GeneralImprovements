using System;
using System.Linq;

namespace GeneralImprovements.OtherMods
{
    internal static class AdvancedCompanyHelper
    {
        public static bool IsActive { get; private set; }

        public static void Initialize()
        {
            // Check for conflicting mods
            IsActive = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.Contains("AdvancedCompany,"));
        }
    }
}
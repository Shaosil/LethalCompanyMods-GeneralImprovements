using System.Collections;
using GeneralImprovements.Utilities;

namespace GeneralImprovements.Patches.Other
{
    internal static class BuyRateSettingsPatch
    {
        internal static void RefreshPatch()
        {
            MonitorsHelper.UpdateCompanyBuyRateMonitors();
        }

        internal static IEnumerator BuyRateSetterPatch(IEnumerator original)
        {
            while (original != null && original.MoveNext())
            {
                yield return original.Current;
            }

            MonitorsHelper.UpdateCompanyBuyRateMonitors();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;

namespace WinFontReplacer
{
    internal sealed class FontListItem
    {
        public string FamilyName { get; set; }
        public string DisplayName { get; set; }
        public string Subtitle { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal static class FontCatalog
    {
        private static readonly string[] SymbolNameParts = new string[]
        {
            "MDL2",
            "Fluent Icons",
            "Segoe UI Emoji",
            "Segoe UI Symbol",
            "Webdings",
            "Wingdings",
            "Marlett",
            "Material Icons",
            "Font Awesome",
            "Cambria Math"
        };

        public static List<FontListItem> Load()
        {
            List<FontListItem> result = new List<FontListItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (InstalledFontCollection collection = new InstalledFontCollection())
            {
                foreach (FontFamily family in collection.Families)
                {
                    string name;
                    try
                    {
                        name = family.Name;
                    }
                    catch
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(name) || name.StartsWith("@", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!seen.Add(name))
                    {
                        continue;
                    }

                    bool available;
                    try
                    {
                        available = family.IsStyleAvailable(FontStyle.Regular);
                    }
                    catch
                    {
                        available = false;
                    }
                    if (!available || IsSymbolFont(name))
                    {
                        continue;
                    }

                    string localized = GetLocalizedName(family);
                    string display = string.IsNullOrEmpty(localized) ? name : localized;
                    string subtitle = string.Equals(display, name, StringComparison.OrdinalIgnoreCase)
                        ? name
                        : name;

                    result.Add(new FontListItem
                    {
                        FamilyName = name,
                        DisplayName = display,
                        Subtitle = subtitle
                    });
                }
            }

            result.Sort(delegate(FontListItem a, FontListItem b)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(a.DisplayName, b.DisplayName);
            });

            return result;
        }

        private static bool IsSymbolFont(string name)
        {
            foreach (string part in SymbolNameParts)
            {
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetLocalizedName(FontFamily family)
        {
            try
            {
                string name = family.GetName(0x0804);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch
            {
                // Some fonts do not expose a localized family name.
            }

            try
            {
                return family.GetName(CultureInfo.CurrentUICulture.LCID);
            }
            catch
            {
                return null;
            }
        }

        public static bool Exists(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
            {
                return false;
            }

            try
            {
                using (FontFamily family = new FontFamily(familyName))
                {
                    return family.IsStyleAvailable(FontStyle.Regular);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

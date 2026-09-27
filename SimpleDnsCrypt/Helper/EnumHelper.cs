using SimpleDnsCrypt.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SimpleDnsCrypt.Helper
{
    public static class EnumHelper
    {
        public static string Description(this Enum eValue)
        {
            var descriptionAttribute = eValue.GetType().GetField(eValue.ToString())?.GetCustomAttribute<DescriptionAttribute>(false);
            if (descriptionAttribute != null)
                return descriptionAttribute.Description;

            var oTi = CultureInfo.CurrentCulture.TextInfo;
            return oTi.ToTitleCase(oTi.ToLower(eValue.ToString()));
        }

        public static IEnumerable<ValueDescription> GetAllValuesAndDescriptions(Type t)
        {
            if (!t.IsEnum)
                throw new ArgumentException("t must be an enum type");

            return Enum.GetValues(t).Cast<Enum>().Select((e) => new ValueDescription() { Value = e, Description = e.Description() }).ToList();
        }
    }
}

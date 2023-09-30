using System;
using System.Linq;
using PugMod;

namespace BetterChat.Scripts
{
    internal static class Util
    {
        public static T GetField<T>(this object obj, string fieldName)
        {
            var field = obj.GetType()
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals(fieldName));
            if (field == null)
                throw new MissingFieldException(obj.GetType().GetNameChecked(), fieldName);

            return (T)API.Reflection.GetValue(field, obj);
        }
    }
}
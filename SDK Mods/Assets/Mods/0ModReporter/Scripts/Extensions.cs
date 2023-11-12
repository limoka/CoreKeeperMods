using System;
using System.Linq;
using PugMod;

namespace ModReporter.Scripts
{
    public static  class Extensions
    {
        internal static readonly char[] PathSeparatorChars = new char[]
        {
            '/',
            '\\'
        };
		
        public static string GetDirectoryName(this string path)
        {
            if (path == string.Empty)
            {
                throw new ArgumentException("Invalid path");
            }
            if (path == null)
            {
                return null;
            }
            if (path.Trim().Length == 0)
            {
                throw new ArgumentException("Argument string consists of whitespace characters only.");
            }
            int num = path.LastIndexOfAny(PathSeparatorChars);
            if (num == 0)
            {
                num++;
            }
            if (num <= 0)
            {
                return string.Empty;
            }
            string text = path.Substring(0, num);

            return text;
        }
        
        public static T GetValue<T>(this Type type, string fieldName)
        {
            var field = type
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals(fieldName));
            if (field == null)
                throw new MissingFieldException(type.GetNameChecked(), fieldName);

            return (T)API.Reflection.GetValue(field, null);
        }
        
        public static T GetValue<T>(this object obj, string fieldName)
        {
            var field = obj.GetType()
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals(fieldName));
            if (field == null)
                throw new MissingFieldException(obj.GetType().GetNameChecked(), fieldName);

            return (T)API.Reflection.GetValue(field, obj);
        }
        
        public static void SetValue<T>(this object obj, string fieldName, T value)
        {
            var field = obj.GetType()
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals(fieldName));
            if (field == null)
                throw new MissingFieldException(obj.GetType().GetNameChecked(), fieldName);

            API.Reflection.SetValue(field, obj, value);
        }
        
        public static T Invoke<T>(this object obj, string fieldName, params object[] args) 
        {
            var field = obj.GetType()
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals(fieldName));
            if (field == null)
                throw new MissingFieldException(obj.GetType().GetNameChecked(), fieldName);

            return (T)API.Reflection.Invoke(field, obj, args);
        }
    }
}
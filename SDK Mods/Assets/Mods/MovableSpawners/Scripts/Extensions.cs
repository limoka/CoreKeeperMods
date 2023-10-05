﻿using System;
using System.Linq;
using PugMod;

namespace MovableSpawners.Util
{
    public static class Extensions
    {
        public static T GetValue<T>(this object obj, string fieldName)
        {
            var field = obj.GetType()
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals(fieldName));
            if (field == null)
                throw new MissingFieldException(obj.GetType().GetNameChecked(), fieldName);

            return (T)API.Reflection.GetValue(field, obj);
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
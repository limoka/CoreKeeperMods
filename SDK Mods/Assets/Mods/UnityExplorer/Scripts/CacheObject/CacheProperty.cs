using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityExplorer.Inspectors;

namespace UnityExplorer.CacheObject
{
    public class CacheProperty : CacheMember
    {
        public PropertyInfo PropertyInfo { get; internal set; }
        public override Type DeclaringType => PropertyInfo.DeclaringType;
        public override bool CanWrite => PropertyInfo.CanWrite;
        public override bool IsStatic => m_isStatic ?? (bool)(m_isStatic = PropertyInfo.GetAccessors(true)[0].IsStatic);
        private bool? m_isStatic;

        public override bool RefreshFromSource => PropertyInfo.PropertyType.IsValueType;

        public override bool ShouldAutoEvaluate => !HasArguments;

        public CacheProperty(PropertyInfo pi)
        {
            this.PropertyInfo = pi;
        }

        public override void SetInspectorOwner(ReflectionInspector inspector, MemberInfo member)
        {
            base.SetInspectorOwner(inspector, member);

            Arguments = PropertyInfo.GetIndexParameters();
        }

        public override object TryEvaluate()
        {
            try
            {
                object ret;
                
                bool isByRef = PropertyInfo.GetMethod != null && PropertyInfo.GetMethod.ReturnType.IsByRef;

                if (isByRef)
                {
                    if (!HasArguments)
                        ret = GetByRefValue(DeclaringInstance);
                    else
                        throw new Exception("Reading such values not supported!");
                }
                else
                {
                    if (HasArguments)
                        ret = PropertyInfo.GetValue(DeclaringInstance, Evaluator.TryParseArguments());
                    else
                        ret = PropertyInfo.GetValue(DeclaringInstance, null);
                }

                LastException = null;
                return ret;
            }
            catch (Exception ex)
            {
                LastException = ex;
                return null;
            }
        }

        private object GetByRefValue(object instance)
        {
            var getMethod = PropertyInfo.GetMethod;
            if (getMethod == null) return null;

            Type returnType = getMethod.ReturnType.GetElementType(); // The T in 'ref T'
            Type declaringType = PropertyInfo.DeclaringType;
            if (declaringType == null) return null;
            
            var dm = new DynamicMethod(
                $"GetRef_{PropertyInfo.Name}",
                typeof(object),
                new[] { typeof(object) },
                PropertyInfo.Module,
                true);

            var il = dm.GetILGenerator();
            var nullLabel = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            if (declaringType.IsValueType)
                il.Emit(OpCodes.Unbox, declaringType); // Address for struct
            else
                il.Emit(OpCodes.Castclass, declaringType); // Cast for class

            if (declaringType.IsValueType)
                il.Emit(OpCodes.Call, getMethod);
            else
                il.Emit(OpCodes.Callvirt, getMethod);
            
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Beq, nullLabel);

            il.Emit(OpCodes.Ldobj, returnType);
            il.Emit(OpCodes.Box, returnType);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(nullLabel);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);

            return dm.Invoke(null, new[] { instance });
        }

        protected override void TrySetValue(object value)
        {
            if (!CanWrite)
                return;

            try
            {
                bool _static = PropertyInfo.GetAccessors(true)[0].IsStatic;

                if (HasArguments)
                    PropertyInfo.SetValue(DeclaringInstance, value, Evaluator.TryParseArguments());
                else
                    PropertyInfo.SetValue(DeclaringInstance, value, null);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning(ex);
            }
        }
    }
}

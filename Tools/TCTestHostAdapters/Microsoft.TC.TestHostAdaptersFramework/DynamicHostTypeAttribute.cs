using System;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.TC.TestHostAdapters
{
    public class DynamicHostTypeAttribute: Attribute
    {
        public Type HostType { get; private set; }

        public DynamicHostTypeAttribute(Type hostType)
        {
            this.HostType = hostType;
        }

        public  static Type GetDynamicHostType(string testName, string targetClassName, string targetClassCodebase)
        {
            Type attributeType = typeof(DynamicHostTypeAttribute);
            Assembly targetAssembly = Assembly.LoadFrom(targetClassCodebase);
            Type targetLoadedType = targetAssembly.GetType(targetClassName);
            MethodInfo targetMethod = targetLoadedType.GetMethod(testName);
            object[] attributes = targetMethod.GetCustomAttributes(attributeType, false);
            if (attributes.Length != 1)
            {
                throw new InvalidOperationException("Unable to find single DynamicHostType attribute");
            }
            DynamicHostTypeAttribute attribute = attributes[0] as DynamicHostTypeAttribute;
            Debug.Assert(attribute != null, "DynamicHostType is not expected type");
            Type hostType = attribute.HostType;
            return hostType;
        }

    }
}

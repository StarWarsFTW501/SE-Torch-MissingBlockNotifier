using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    public class MyPatchUtilities
    {
        public static string GetMethodCaller(int stackFrameIndex = 3)
        {
            var stack = new StackTrace();
            var frame = stack.GetFrame(stackFrameIndex);
            var method = frame?.GetMethod();
            return GetMethodStringInfo(method);
        }
        public static string GetMethodStringInfo(MethodBase method)
        {
            if (method == null)
                return "indeterminate method";

            string outputPrefix = (
                method.IsPublic ? "public" :
                method.IsPrivate ? "private" :
                method.IsFamily ? "protected" :
                method.IsAssembly ? "internal" : "") + (

                method.IsStatic ? " static" : " instance") + (

                method is ConstructorInfo ? " constructor" :
                    (method as MethodInfo).ReturnType.IsVoid() ? " void" :
                    (" " + GetTypeInformationRecursive((method as MethodInfo).ReturnType)));

            var parameters = method.GetParameters();
            string parameterDescription = "";
            if (parameters.Length != 0)
            {
                parameterDescription = GetParameterDescription(parameters[0]);
                for (int i = 1; i < parameters.Length; i++)
                    parameterDescription += $", {GetParameterDescription(parameters[i])}";
            }


            return $"{outputPrefix} {(method.DeclaringType == null ? "[indeterminate owner]" : GetTypeInformationRecursive(method.DeclaringType))}.{((method is MethodInfo methodInfo) ? methodInfo.Name : (method as ConstructorInfo).Name)}({parameterDescription})";
        }
        public static string GetFieldStringInfo(FieldInfo field)
        {
            if (field == null)
                return "indeterminate field";

            string outputPrefix = (
                field.IsPublic ? "public" :
                field.IsPrivate ? "private" :
                field.IsFamily ? "protected" :
                field.IsAssembly ? "internal" : "") + (

                field.IsStatic ? " static " : " instance ") +

                GetTypeInformationRecursive(field.FieldType);

            return $"{outputPrefix} {field.Name}";
        }
        static string GetParameterDescription(ParameterInfo parameter)
        {
            return $"{(parameter.ParameterType.IsByRef ? parameter.IsOut ? "out " : "ref " : parameter.IsIn ? "in " : "")}{GetTypeInformationRecursive(parameter.ParameterType)}";
        }
        static string GetTypeInformationRecursive(Type type)
        {
            if (type == null)
                return null;
            return $"{type.Namespace}.{type.Name}{(type.IsGenericType ? $"<{string.Join(", ", type.GetGenericArguments().Select(t => GetTypeInformationRecursive(t) ?? "indeterminate generic"))}>" : "")}";
        }
    }
}

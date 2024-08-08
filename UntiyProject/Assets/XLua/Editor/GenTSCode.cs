using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GenCode
{
    public static class GenTSCode
    {
        //  ¹Ø¼ü×ÖÌæ»»
        //  ºÚÃûµ¥
        private class FuncInfo
        {
            public bool IsStatic = false;
            public string Name = "";
            public string Type = "";
            public List<(string, string)> Params = new();
        }

        private class PropInfo
        {
            public bool IsStatic = false;
            public string Name = "";
            public string Type = "";
        }

        private class ClassInfo
        {
            public string Name = "";
            public List<FuncInfo> MemberFuncs = new();
            public List<PropInfo> MemberProps = new();
        }

        private class NamespaceInfo
        {
            public string Name = "";
            public List<ClassInfo>     Classs = new();
            public List<NamespaceInfo> Spaces = new();

            public ClassInfo FindClass(string name)
            {
                return Classs.Find(v => v.Name == name);
            }

            public NamespaceInfo FindSpace(string name)
            {
                return Spaces.Find(v => v.Name == name);
            }
        }

        private class Contex
        {
            public string FileName;
            public System.IO.TextWriter          FWriter;
            public Queue<System.Type>            GenTypes;
            public Dictionary<System.Type, bool> HasTypes;

            public NamespaceInfo GenTree = new();
        }

        private static Dictionary<string, string> KeyWordMap = new()
        {
            { "System.Void", "void" },
            { "System.Single", "number" },
            { "System.Double", "number" },
            { "System.Int64", "number" },
            { "System.Int32", "number" },
            { "System.UInt32", "number" },
            { "System.UInt64", "number" },
            { "System.UInt16", "number" },
            { "System.String", "string" },
            { "System.Byte[]", "string" },
            { "System.Boolean", "boolean" },
        };

        private static string T(System.Type type)
        {
            {
                var keyword = type.Namespace + "." + type.Name;
                if (KeyWordMap.TryGetValue(keyword, out var v))
                {
                    return v;
                }
            }

            {
                var isArray = type.IsArray;
                if (isArray) { type = type.GetElementType(); }
                var keyword = type.Namespace + "." + type.Name;
                if (KeyWordMap.TryGetValue(keyword, out var v))
                {
                    keyword = v;
                }
                return keyword + (isArray ? "[]" : "");
            }
        }


        [MenuItem("Test/GenTSCode")]
        public static void GenTest()
        {
            List<System.Type> types = new()
            {
                typeof(Vector3),
                typeof(GameObject)
            };
            Gen(types, "F:/1.d.ts");
        }

        public static void Gen(List<System.Type> gentypes, string filename)
        {
            Contex ctx = new()
            {
                FileName = filename,GenTypes = new(gentypes),
            };
            ctx.HasTypes = new();
            gentypes.ForEach(v => ctx.HasTypes.Add(v, true));

            BeginContex(ref ctx);
            ApplyContex(ctx);
            EndedContex(ctx);
        }

        private static void BeginContex(ref Contex ctx)
        {
            ctx.FWriter = new System.IO.StreamWriter(ctx.FileName, false);
        }

        private static void EndedContex(Contex ctx)
        {
            System.Text.StringBuilder outputBuffer = new();
            outputBuffer.Append("declare namespace T");
            GenCode(ctx.GenTree, 0, outputBuffer);
            ctx.FWriter.Write(outputBuffer.ToString());

            ctx.FWriter.Dispose();
        }

        private static void ApplyContex(Contex ctx)
        {
            while (ctx.GenTypes.Count != 0)
            {
                var type = ctx.GenTypes.Dequeue();
                var spaceInfo = HandleNamespace(ctx, type);
                var classInfo = HandleClass(ctx, spaceInfo, type);
                if (classInfo != null && ctx.HasTypes[type])
                {
                    HandlePropertys(ctx, classInfo, type.GetProperties());
                    HandleMethods(ctx, classInfo, type.GetMethods());
                    HandleFields(ctx, classInfo, type.GetFields());
                }
            }
        }

        private static void GenCode(NamespaceInfo spaceNode, int ident, System.Text.StringBuilder outputBuffer)
        {
            if (spaceNode.Name != "")
            {
                outputBuffer.Append(Ident(ident));
                outputBuffer.Append("namespace ");
                outputBuffer.Append(spaceNode.Name);
            }
            outputBuffer.Append("\n");
            outputBuffer.Append(Ident(ident));
            outputBuffer.Append("{\n");

            foreach (var spaceInfo in spaceNode.Spaces)
            {
                GenCode(spaceInfo, ident + 1, outputBuffer);
            }

            foreach (var classInfo in spaceNode.Classs)
            {
                GenCode(classInfo, ident + 1, outputBuffer);
            }

            outputBuffer.Append(Ident(ident));
            outputBuffer.Append("}\n");
        }

        private static void GenCode(ClassInfo classInfo, int ident, System.Text.StringBuilder outputBuffer)
        {
            System.Text.StringBuilder sb = new();
            sb.AppendFormat("{0}class {1}\n{0}{{\n", Ident(ident), classInfo.Name);

            //  func
            foreach (var funcInfo in classInfo.MemberFuncs)
            {
                sb.Append(Ident(ident + 1));
                sb.Append(funcInfo.Name);
                sb.Append("(");
                for (var i = 0; i != funcInfo.Params.Count; ++i)
                {
                    if (i != 0) { sb.Append(", "); }
                    sb.Append(funcInfo.Params[i].Item2);
                    sb.Append(": ");
                    sb.Append(funcInfo.Params[i].Item1);
                }
                sb.Append("): ");
                sb.Append(funcInfo.Type);
                sb.Append(";\n");
            }

            //  prop
            foreach (var propInfo in classInfo.MemberProps)
            {
                sb.Append(Ident(ident + 1));
                sb.Append(propInfo.Name);
                sb.Append(" : ");
                sb.Append(propInfo.Type);
                sb.Append(";\n");
            }
            sb.Append(Ident(ident));
            sb.Append("}\n");
            outputBuffer.Append(sb.ToString());
        }

        private static string Ident(int count)
        {
            System.Text.StringBuilder sb = new();
            for (var i = 0; i != count; ++i)
            {
                sb.Append("    ");
            }
            return sb.ToString();
        }

        private static NamespaceInfo HandleNamespace(Contex ctx, System.Type typeInfo)
        {
            var treeNode = ctx.GenTree;
            foreach (var name in typeInfo.Namespace.Split("."))
            {
                var nextNode = treeNode.FindSpace(name);
                if (nextNode == null)
                {
                    nextNode = new() { Name = name };
                    treeNode.Spaces.Add(nextNode);
                }
                treeNode = nextNode;
            }
            return treeNode;
        }

        private static ClassInfo HandleClass(Contex ctx, NamespaceInfo space, System.Type type)
        {
            if (type.IsGenericType) { return null; }
            var classInfo = space.FindClass(type.Name);
            if (classInfo == null)
            {
                classInfo = new() { Name = type.Name };
                space.Classs.Add(classInfo);
            }
            return classInfo;
        }

        private static void HandleMethods(Contex ctx, ClassInfo classInfo, System.Reflection.MethodInfo[] methodInfos)
        {
            foreach (var methodInfo in methodInfos)
            {
                if (!methodInfo.IsPublic || methodInfo.IsSpecialName ||
                    methodInfo.ContainsGenericParameters) { continue; }

                if (!CheckParamVaild(methodInfo.GetParameters()))
                {
                    continue;
                }

                var funcInfo = new FuncInfo()
                {
                    Name = methodInfo.Name,
                    Type = T(methodInfo.ReturnType),
                    IsStatic = methodInfo.IsStatic,
                };

                foreach (var param in methodInfo.GetParameters())
                {
                    funcInfo.Params.Add((T(param.ParameterType), param.Name));
                    PushType(ctx, param.ParameterType);
                }

                PushType(ctx,methodInfo.ReturnType);
                classInfo.MemberFuncs.Add(funcInfo);
            }
        }

        private static void HandlePropertys(Contex ctx, ClassInfo classInfo, System.Reflection.PropertyInfo[] propertyInfos)
        {
            foreach (var propertyInfo in propertyInfos)
            {
                var propInfo = new PropInfo()
                {
                    IsStatic = false, Name = propertyInfo.Name,
                    Type = T(propertyInfo.PropertyType),
                };
                classInfo.MemberProps.Add(propInfo);
                PushType(ctx, propertyInfo.PropertyType);
            }
        }

        private static void HandleFields(Contex ctx, ClassInfo classInfo, System.Reflection.FieldInfo[] fieldInfos)
        {
            foreach (var fieldInfo in fieldInfos)
            {
                if (!fieldInfo.IsPublic) { continue; }

                var propInfo = new PropInfo()
                {
                    IsStatic = fieldInfo.IsStatic,
                    Type = T(fieldInfo.FieldType),
                    Name = fieldInfo.Name,
                };
                classInfo.MemberProps.Add(propInfo);
                PushType(ctx, fieldInfo.FieldType);
            }
        }

        private static void PushType(Contex ctx, System.Type type)
        {
            if (type.IsArray) { type = type.GetElementType(); }

            if (ctx.HasTypes.ContainsKey(type) || type.IsPrimitive || type.IsPointer)
            {
                return;
            }

            ctx.HasTypes.Add(type, false);
            ctx.GenTypes.Enqueue(type);
        }

        private static bool CheckParamVaild(System.Reflection.ParameterInfo[] paramInfos)
        {
            foreach (var paramInfo in paramInfos)
            {
                if (paramInfo.ParameterType.Name.Contains("&")          ||
                    paramInfo.ParameterType.ContainsGenericParameters   ||
                    paramInfo.ParameterType.IsGenericType               ||
                    paramInfo.ParameterType.IsPointer                   ||
                    paramInfo.ParameterType.IsInterface) { return false; }
            }
            return true;
        }
    }
}
﻿// <copyright file="CompiledClassFactory.cs" company="Tom Luppi">
//     Copyright (c) Tom Luppi.  All rights reserved.
// </copyright>

namespace CompiledDefinitionSourceGenerator
{
    using System.Collections.Generic;
    using System.Linq;

    using LegendsGenerator.ContractsGenerator.Writer;

    /// <summary>
    /// Factory which creates the compiled source file.
    /// </summary>
    internal static class CompiledClassFactory
    {
        /// <summary>
        /// The usings in the class.
        /// </summary>
        private static readonly string[] Usings = new string[]
        {
            "System",
            "System.Collections.Generic",
            "System.Diagnostics",
            "System.Linq",
            "LegendsGenerator.Contracts",
            "LegendsGenerator.Contracts.Compiler",
            "LegendsGenerator.Contracts.Definitions",
            "LegendsGenerator.Contracts.Definitions.Events",
        };

        /// <summary>
        /// Generates a comparible Compiled Definition from the class info.
        /// </summary>
        /// <param name="classInfo">The class info to generate.</param>
        /// <returns>The class's C# code.</returns>
        public static string Generate(ClassInfo classInfo)
        {
            bool additionalParametersForClassDefined =
                classInfo.AdditionalParametersForMethods.Any(x => x.Equals(ClassInfo.AdditionalParamtersForClassMethod));

            bool classAddParams = additionalParametersForClassDefined || classInfo.UsesAdditionalParametersForHoldingClass;

            ClassWriter sb = new ClassWriter(
                new ClassDefinition(classInfo.Namespace, classInfo.TypeName)
                {
                    Nullable = true,
                    Partial = true,
                    Usings = Usings,
                });

            FieldDefinitions(sb, classInfo);
            CompileMethod(sb, classInfo);
            AttachMethod(sb, classInfo);
            CombinedAdditionalParametersForClass(sb, additionalParametersForClassDefined);

            foreach (var prop in classInfo.CompiledProps)
            {
                IsComplexProperty(sb, prop);
            }

            foreach (var prop in classInfo.CompiledDictionaryProps)
            {
                IsComplexProperty(sb, prop);
            }

            foreach (var prop in classInfo.CompiledProps)
            {
                EvalConditionMethod(sb, prop, classInfo.AdditionalParametersForMethods, classAddParams);
            }

            foreach (var prop in classInfo.CompiledDictionaryProps)
            {
                EvalDictionaryConditionMethod(sb, prop, classInfo.AdditionalParametersForMethods, classAddParams);
            }

            foreach (var prop in classInfo.CompiledProps)
            {
                GetParametersMethod(sb, prop, classInfo.AdditionalParametersForMethods, classAddParams);
            }

            foreach (var prop in classInfo.CompiledDictionaryProps)
            {
                GetParametersMethod(sb, prop, classInfo.AdditionalParametersForMethods, classAddParams);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the field definitions on this class.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="classInfo">The class info.</param>
        private static void FieldDefinitions(ClassWriter sb, ClassInfo classInfo)
        {
            foreach (var prop in classInfo.CompiledProps)
            {
                sb.AddField(
                    $"The lazy-compiled condition for the {prop.Name} property.",
                    $"Lazy<ICompiledCondition<{prop.ReturnType}>>?",
                    $"compiledCondition{prop.Name}");
            }

            foreach (var prop in classInfo.CompiledDictionaryProps)
            {
                sb.AddField(
                    $"The lazy-compiled conditions for the {prop.Name} property.",
                    $"IDictionary<string, Lazy<ICompiledCondition<{prop.ReturnType}>>>",
                    $"compiledCondition{prop.Name}",
                    $"new Dictionary<string, Lazy<ICompiledCondition<{prop.ReturnType}>>>()");
            }
        }

        /// <summary>
        /// Creates a class which combined the class-wide additional parameters class and the upstream parameters.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="classAddParams">If the class has a class-wide additional parameters method.</param>
        private static void CombinedAdditionalParametersForClass(ClassWriter sb, bool classAddParams)
        {
            using var braces = sb.AddMethod(
                new MethodDefinition($"CombinedAdditionalParametersForClass")
                {
                    SummaryDoc = "Returns all class-wide additional parameters for this class, including upstream parameters.",
                    ReturnsDoc = "All class-wide additional parameters.",
                    Access = AccessLevel.Protected,
                    Override = true,
                    Type = "List<string>",
                });

            sb.AppendLine($"List<string> addParams = base.CombinedAdditionalParametersForClass();");

            if (classAddParams)
            {
                sb.AppendLine($"addParams.AddRange(this.{ClassInfo.AdditionalParamtersForClassMethod}());");
            }

            sb.AppendLine("return addParams;");
        }

        /// <summary>
        /// Adds the attach override to the source file.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="classInfo">The class info.</param>
        private static void AttachMethod(ClassWriter sb, ClassInfo classInfo)
        {
            using var braces = sb.AddMethod(
                new MethodDefinition("Attach")
                {
                    Access = AccessLevel.Public,
                    Override = true,
                    Parameters = new ParamDef[]
                    {
                        new ParamDef("IConditionCompiler", "compiler", "The condition compiler."),
                        new ParamDef("BaseDefinition?", "upsteamDefinition", "null", "The upstream definition."),
                    },
                });

            sb.AppendLine("base.Attach(compiler, upsteamDefinition);");
            foreach (var prop in classInfo.CompiledProps)
            {
                sb.AppendLine($"this.compiledCondition{prop.Name} = ");
                sb.AppendLine($"   new Lazy<ICompiledCondition<{prop.ReturnType}>>(() => this.CreateCondition<{prop.ReturnType}>(this.{prop.Name}, {prop.AsFormattedText.ToString().ToLower()}, this.{prop.Name}_IsComplex, this.GetParameters{prop.Name}()));");
            }

            foreach (var prop in classInfo.CompiledDictionaryProps)
            {
                sb.AppendLine($"foreach (var entry in this.{prop.Name})");
                sb.StartBrace();
                sb.AppendLine($"this.compiledCondition{prop.Name}[entry.Key] = ");
                sb.AppendLine($"   new Lazy<ICompiledCondition<{prop.ReturnType}>>(() => this.CreateCondition<{prop.ReturnType}>(this.{prop.Name}[entry.Key], {prop.AsFormattedText.ToString().ToLower()}, this.{prop.Name}_IsComplex, this.GetParameters{prop.Name}()));");
                sb.EndBrace();
                sb.AppendLine();
            }

            foreach (var prop in classInfo.DefinitionProps)
            {
                sb.AppendLine($"this.{prop.Name}?.Attach(compiler, this);");
            }

            foreach (var prop in classInfo.DefinitionArrayProps)
            {
                sb.AppendLine($"foreach (var value in this.{prop.Name})");
                sb.StartBrace();
                sb.AppendLine($"value.Attach(compiler, this);");

                sb.EndBrace();
                sb.AppendLine();
            }

            foreach (var prop in classInfo.DefinitionDictionaryProps)
            {
                sb.AppendLine($"foreach (var value in this.{prop.Name})");
                sb.StartBrace();
                sb.AppendLine($"value.Value.Attach(compiler, this);");

                sb.EndBrace();
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Creates the method which activates all Lazy fields so compile everything.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="classInfo">The class info.</param>
        private static void CompileMethod(ClassWriter sb, ClassInfo classInfo)
        {
            using var braces = sb.AddMethod(
                new MethodDefinition("Compile")
                {
                    Access = AccessLevel.Public,
                    Override = true,
                });

            sb.AppendLine("if (this.Compiler == null)");
            sb.StartBrace();
            sb.AppendLine("throw new InvalidOperationException(\"Definition must be attached before Compile is called.\");");
            sb.EndBrace();
            sb.AppendLine();

            sb.AppendLine("base.Compile();");

            foreach (PropertyInfo prop in classInfo.CompiledProps)
            {
                sb.AppendLine($"_ = this.compiledCondition{prop.Name}?.Value;");
            }

            foreach (PropertyInfo prop in classInfo.CompiledDictionaryProps)
            {
                sb.AppendLine($"this.compiledCondition{prop.Name}?.ToList().ForEach(x => _ = x.Value?.Value);");
            }

            foreach (var prop in classInfo.DefinitionProps)
            {
                sb.AppendLine($"this.{prop.Name}?.Compile();");
            }

            foreach (var prop in classInfo.DefinitionArrayProps)
            {
                sb.AppendLine($"foreach (var value in this.{prop.Name})");
                sb.StartBrace();
                sb.AppendLine($"value.Compile();");
                sb.EndBrace();
                sb.AppendLine();
            }

            foreach (var prop in classInfo.DefinitionDictionaryProps)
            {
                sb.AppendLine($"foreach (var value in this.{prop.Name})");
                sb.StartBrace();
                sb.AppendLine($"value.Value.Compile();");
                sb.EndBrace();
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Generates the property which decides if a compiled property is complex.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="info">The property info.</param>
        private static void IsComplexProperty(ClassWriter sb, PropertyInfo info)
        {
            sb.AddProperty(
                $"Gets or sets a value indicating whether the {info.Name} condition should be compiled as a Complex condition.",
                AccessLevel.Public,
                "bool",
                $"{info.Name}_IsComplex");
        }

        /// <summary>
        /// Creates the method which runs the condition evalulate method.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="info">The property info.</param>
        /// <param name="additionParameterMethods">The list of additional parameter methods.</param>
        /// <param name="classAddParams">True if the class has additional parameters method.</param>
        private static void EvalConditionMethod(
            ClassWriter sb,
            PropertyInfo info,
            IReadOnlyCollection<string> additionParameterMethods,
            bool classAddParams)
        {
            string? matchingAdditionalParamtersMethod =
                additionParameterMethods.FirstOrDefault(x => x.Equals($"{ClassInfo.AdditionalParamtersMethodPrefix}{info.Name}"));

            List<ParamDef> allParameters = new List<ParamDef>()
            {
                new ParamDef("Random", "rdm", "The random number generator"),
            };

            allParameters.AddRange(info.Variables.Select(v =>
                new ParamDef($"BaseThing", v, "One of the variables which will be passed to the compiled condition.")).ToList());

            if (matchingAdditionalParamtersMethod != null || classAddParams)
            {
                allParameters.Add(new ParamDef(
                    "IDictionary<string, BaseThing>",
                    "additionalParameters",
                    $"Additional parameters as defined by AdditionalParametersForClass and AdditionalParametersFor{info.Name} methods, or the upstream additional parameters."));
            }

            using var braces = sb.AddMethod(
                new MethodDefinition($"Eval{info.Name}")
                {
                    SummaryDoc = $"Evalulates the expressions in the {info.Name} property with the given parameters.",
                    ReturnsDoc = "The result of evaluation.",
                    Access = AccessLevel.Public,
                    Type = info.ReturnType,
                    Parameters = allParameters,
                });

            sb.AppendLine($"if (this.Compiler == null || this.compiledCondition{info.Name} == null)");
            sb.StartBrace();
            sb.AppendLine("throw new InvalidOperationException(\"Definition must be attached before any Eval method is called.\");");
            sb.EndBrace();
            sb.AppendLine();

            sb.AddDictionary(
                "Dictionary<string, BaseThing>",
                "param",
                info.Variables.ToDictionary(x => x, x => x));

            if (matchingAdditionalParamtersMethod != null || classAddParams)
            {
                sb.AppendLine("foreach(var additionalParameter in additionalParameters)");
                sb.StartBrace();
                sb.AppendLine("param[additionalParameter.Key] = additionalParameter.Value;");
                sb.EndBrace();
                sb.AppendLine();
            }

            sb.AppendLine($"return this.compiledCondition{info.Name}.Value.Evaluate(rdm, param);");
        }

        /// <summary>
        /// Creates the method which runs the condition evalulate method.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="info">The property info.</param>
        /// <param name="additionParameterMethods">The list of additional parameter methods.</param>
        /// <param name="classAddParams">True if the class has additional parameters method.</param>
        private static void EvalDictionaryConditionMethod(
            ClassWriter sb,
            PropertyInfo info,
            IReadOnlyCollection<string> additionParameterMethods,
            bool classAddParams)
        {
            string? matchingAdditionalParamtersMethod =
                additionParameterMethods.FirstOrDefault(x => x.Equals($"{ClassInfo.AdditionalParamtersMethodPrefix}{info.Name}"));

            List<ParamDef> allParameters = new List<ParamDef>()
            {
                new ParamDef("string", "key", "The key, in the condition dicitonary, to evalulate."),
                new ParamDef("Random", "rdm", "The random number generator"),
            };

            allParameters.AddRange(info.Variables.Select(v =>
                new ParamDef($"BaseThing", v, "One of the variables which will be passed to the compiled condition.")).ToList());

            if (matchingAdditionalParamtersMethod != null || classAddParams)
            {
                allParameters.Add(new ParamDef(
                    "IDictionary<string, BaseThing>",
                    "additionalParameters",
                    $"Additional parameters as defined by AdditionalParametersForClass and AdditionalParametersFor{info.Name} methods, or the upstream additional parameters."));
            }

            using var braces = sb.AddMethod(
                new MethodDefinition($"Eval{info.Name}")
                {
                    SummaryDoc = $"Evalulates the expressions in the {info.Name} property with the given parameters.",
                    ReturnsDoc = "The result of evaluation.",
                    Access = AccessLevel.Public,
                    Type = info.ReturnType,
                    Parameters = allParameters,
                });

            sb.AppendLine($"if (this.Compiler == null || this.compiledCondition{info.Name} == null)");
            sb.StartBrace();
            sb.AppendLine("throw new InvalidOperationException(\"Definition must be attached before any Eval method is called.\");");
            sb.EndBrace();
            sb.AppendLine();

            sb.AddDictionary(
                "Dictionary<string, BaseThing>",
                "param",
                info.Variables.ToDictionary(x => x, x => x));

            if (matchingAdditionalParamtersMethod != null || classAddParams)
            {
                sb.AppendLine("foreach(var additionalParameter in additionalParameters)");
                sb.StartBrace();
                sb.AppendLine("param[additionalParameter.Key] = additionalParameter.Value;");
                sb.EndBrace();
                sb.AppendLine();
            }

            sb.AppendLine($"return this.compiledCondition{info.Name}[key].Value.Evaluate(rdm, param);");
        }

        /// <summary>
        /// Constructs a method which gets the parameters lsit for the specified property.
        /// </summary>
        /// <param name="sb">The class writer.</param>
        /// <param name="info">The property info.</param>
        /// <param name="additionParameterMethods">The list of methods which return addition parameter names.</param>
        /// <param name="classAddParams">True if the class has additional parameters method.</param>
        private static void GetParametersMethod(
            ClassWriter sb,
            PropertyInfo info,
            IReadOnlyCollection<string> additionParameterMethods,
            bool classAddParams)
        {
            string? matchingAdditionalParamtersMethod =
                additionParameterMethods.FirstOrDefault(x => x.Equals($"{ClassInfo.AdditionalParamtersMethodPrefix}{info.Name}"));

            using var braces = sb.AddMethod(
                   new MethodDefinition($"GetParameters{info.Name}")
                   {
                       SummaryDoc = $"Gets all parameters which should be presented to the {info.Name} expression.",
                       ReturnsDoc = "All parameters needed by the expression.",
                       Access = AccessLevel.Public,
                       Type = "IList<string>",
                   });

            sb.AppendLine($"List<string> parameters = new List<string> {{ {string.Join(", ", info.Variables.Select(SurroundInQuotes))} }};");

            if (matchingAdditionalParamtersMethod != null)
            {
                sb.AppendLine($"parameters.AddRange(this.{matchingAdditionalParamtersMethod}());");
            }

            if (classAddParams)
            {
                sb.AppendLine($"parameters.AddRange(this.Combined{ClassInfo.AdditionalParamtersForClassMethod}());");
            }

            sb.AppendLine("return parameters;");
        }

        /// <summary>
        /// Surrounds the input string in quotes.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The string, but in quotes.</returns>
        private static string SurroundInQuotes(string input)
        {
            return "\"" + input + "\"";
        }
    }
}

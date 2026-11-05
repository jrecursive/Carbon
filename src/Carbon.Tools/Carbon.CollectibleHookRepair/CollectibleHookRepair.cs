using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Exact-input offline repair. Never modify an installed assembly in place.
internal static class CollectibleHookRepair
{
    private const string OriginalHash = "78f7071f45775165b83f71e3825d5cdc71db82bcbfe1d880a30c2017601d7245";
    private const string RustHash = "30b6d0925bc40a08b0b59c1c3c85559390435792e00838d9e183061994330f82";
    private const string HookType = "Resource_CollectibleEntity_06dec48365445b6ad8319c8505f6e9a1";
    private const string ParameterAttribute = "API.Hooks.MetadataAttribute/Parameter";

    private static int Main(string[] args)
    {
        try
        {
            Require(args.Length == 4, "Usage: CollectibleHookRepair.exe INPUT RUST_DLL CARBON_MANAGED OUTPUT");
            string input = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[3]);
            Require(input != output && !File.Exists(output), "Candidate must be a new file; in-place repair is forbidden.");
            Require(HashFile(input) == OriginalHash && HashFile(args[1]) == RustHash, "Exact hook/native input hash guard failed.");
            var resolver = new DefaultAssemblyResolver();
            foreach (string directory in new[] { args[2], Path.Combine(args[2], "lib"), Path.GetDirectoryName(args[1]) })
                resolver.AddSearchDirectory(directory);
            using (var assembly = AssemblyDefinition.ReadAssembly(input, new ReaderParameters { AssemblyResolver = resolver }))
            {
                Require(assembly.Modules.Count == 1 && !assembly.Name.HasPublicKey, "Unexpected multi-module or signed hook assembly.");
                TypeDefinition hook = Types(assembly.MainModule.Types).Single(type => type.Name == HookType);
                TypeDefinition state = hook.NestedTypes.Single(type => type.Name == "<Transpiler>d__0");
                MethodDefinition move = state.Methods.Single(method => method.Name == "MoveNext");
                CustomAttribute parameter = hook.CustomAttributes.Single(attribute =>
                    attribute.AttributeType.FullName == ParameterAttribute &&
                    attribute.ConstructorArguments.Count == 3 &&
                    (string)attribute.ConstructorArguments[0].Value == "local9");
                Require((string)parameter.ConstructorArguments[1].Value == "ItemModConsume" &&
                    !(bool)parameter.ConstructorArguments[2].Value, "Original third-argument metadata changed.");
                var beforeMethods = Methods(assembly.MainModule);
                var beforeAttributes = TypeAttributes(assembly.MainModule);
                Instruction index = move.Body.Instructions.Single(instruction =>
                    instruction.OpCode.Code == Code.Ldc_I4 && (int)instruction.Operand == 146);
                Instruction local = move.Body.Instructions.Single(instruction =>
                    instruction.OpCode.Code == Code.Ldc_I4_S && (sbyte)instruction.Operand == 9 &&
                    instruction.Previous != null && instruction.Previous.OpCode.Code == Code.Ldsfld &&
                    instruction.Previous.Operand is FieldReference &&
                    ((FieldReference)instruction.Previous.Operand).FullName == "System.Reflection.Emit.OpCode System.Reflection.Emit.OpCodes::Ldloc_S");
                index.Operand = 128;
                local.Operand = (sbyte)8;
                parameter.ConstructorArguments[0] = new CustomAttributeArgument(parameter.ConstructorArguments[0].Type, "local8");
                parameter.ConstructorArguments[1] = new CustomAttributeArgument(parameter.ConstructorArguments[1].Type, "Item");
                var afterMethods = Methods(assembly.MainModule);
                var afterAttributes = TypeAttributes(assembly.MainModule);
                Require(Changed(beforeMethods, afterMethods).SequenceEqual(new[] { move.FullName }), "Unexpected method-body changes.");
                Require(Changed(beforeAttributes, afterAttributes).SequenceEqual(new[] { hook.FullName }), "Unexpected type-attribute changes.");
                string expected = Model(assembly.MainModule);
                byte[] first = Write(assembly), second = Write(assembly);
                Require(first.SequenceEqual(second), "Cecil output is not deterministic.");
                using (var reread = AssemblyDefinition.ReadAssembly(new MemoryStream(first), new ReaderParameters { AssemblyResolver = resolver }))
                {
                    Require(Model(reread.MainModule) == expected, "Candidate round-trip changed unrelated assembly content.");
                    Require(reread.MainModule.Mvid != Guid.Empty && reread.MainModule.Mvid != AssemblyDefinition.ReadAssembly(input).MainModule.Mvid,
                        "Candidate identity must distinguish repaired content.");
                    using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        stream.Write(first, 0, first.Length);
                    Console.WriteLine("{\"result\":\"PASS\",\"sourceHookSha256\":\"" + OriginalHash +
                        "\",\"nativeAssemblySha256\":\"" + RustHash + "\",\"candidateSha256\":\"" + HashFile(output) +
                        "\",\"structuralComparison\":\"PASS\",\"methodsChecked\":" + beforeMethods.Count +
                        ",\"methodsChanged\":1,\"typeAttributeOwnersChanged\":1,\"deterministic\":true," +
                        "\"structuralFingerprint\":\"" + HashText(expected) + "\",\"candidateMvid\":\"" + reread.MainModule.Mvid + "\"}");
                }
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static byte[] Write(AssemblyDefinition assembly)
    {
        using (var stream = new MemoryStream())
        {
            assembly.Write(stream, new WriterParameters { DeterministicMvid = true, Timestamp = 0 });
            return stream.ToArray();
        }
    }

    private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in Types(type.NestedTypes)) yield return nested;
        }
    }

    private static Dictionary<string, string> Methods(ModuleDefinition module)
    {
        return Types(module.Types).SelectMany(type => type.Methods).ToDictionary(method => method.FullName, Method);
    }

    private static Dictionary<string, string> TypeAttributes(ModuleDefinition module)
    {
        return Types(module.Types).ToDictionary(type => type.FullName, type => Attributes(type));
    }

    private static IEnumerable<string> Changed(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        Require(before.Count == after.Count && before.Keys.All(after.ContainsKey), "Assembly member inventory changed.");
        return before.Keys.Where(key => before[key] != after[key]).OrderBy(key => key, StringComparer.Ordinal);
    }

    private static string Model(ModuleDefinition module)
    {
        var text = new StringBuilder();
        Row(text, "assembly", module.Assembly.Name.FullName, module.Assembly.Name.Attributes, module.Assembly.Name.HashAlgorithm,
            Hex(module.Assembly.Name.PublicKey), Attributes(module.Assembly), Security(module.Assembly));
        Row(text, "module", module.Name, module.Kind, module.Architecture, module.Attributes, module.Characteristics,
            module.RuntimeVersion, module.EntryPoint == null ? "" : module.EntryPoint.FullName, Attributes(module));
        foreach (var reference in module.AssemblyReferences) Row(text, "reference", reference.FullName, reference.Attributes, reference.HashAlgorithm, Hex(reference.Hash));
        foreach (var reference in module.ModuleReferences) Row(text, "module-reference", reference.Name);
        foreach (var resource in module.Resources)
        {
            var embedded = resource as EmbeddedResource;
            var linked = resource as LinkedResource;
            var external = resource as AssemblyLinkedResource;
            Require(embedded != null || linked != null || external != null, "Unsupported resource kind.");
            Row(text, "resource", resource.Name, resource.Attributes, resource.ResourceType,
                embedded == null ? "" : Hex(embedded.GetResourceData()),
                linked == null ? "" : linked.File, linked == null ? "" : Hex(linked.Hash),
                external == null ? "" : external.Assembly.FullName);
        }
        foreach (var type in Types(module.Types))
        {
            Row(text, "type", type.FullName, type.Attributes, type.BaseType, type.PackingSize, type.ClassSize, Attributes(type), Security(type), Generics(type));
            foreach (var item in type.Interfaces) Row(text, "interface", item.InterfaceType.FullName, Attributes(item));
            foreach (var field in type.Fields)
            {
                Require(!field.HasMarshalInfo, "Unexpected field marshalling metadata.");
                Row(text, "field", field.FullName, field.Attributes, field.Offset, field.HasConstant, Value(field.Constant), Hex(field.InitialValue), Attributes(field));
            }
            foreach (var method in type.Methods) Row(text, "method", Method(method));
            foreach (var property in type.Properties)
                Row(text, "property", property.FullName, property.Attributes, property.HasConstant, Value(property.Constant),
                    property.GetMethod, property.SetMethod, string.Join(",", property.OtherMethods.Select(method => method.FullName)), Attributes(property));
            foreach (var item in type.Events)
                Row(text, "event", item.FullName, item.Attributes, item.AddMethod, item.RemoveMethod, item.InvokeMethod,
                    string.Join(",", item.OtherMethods.Select(method => method.FullName)), Attributes(item));
        }
        return text.ToString();
    }

    private static string Method(MethodDefinition method)
    {
        var text = new StringBuilder();
        Row(text, method.FullName, method.Attributes, method.ImplAttributes, method.SemanticsAttributes,
            method.CallingConvention, method.HasThis, method.ExplicitThis, Attributes(method), Security(method), Generics(method));
        if (method.HasPInvokeInfo) Row(text, "pinvoke", method.PInvokeInfo.Attributes, method.PInvokeInfo.EntryPoint, method.PInvokeInfo.Module.Name);
        foreach (var item in method.Overrides) Row(text, "override", item.FullName);
        Require(!method.MethodReturnType.HasMarshalInfo, "Unexpected return marshalling metadata.");
        Row(text, "return", method.ReturnType.FullName, method.MethodReturnType.Attributes, method.MethodReturnType.HasConstant,
            Value(method.MethodReturnType.Constant), Attributes(method.MethodReturnType));
        foreach (var parameter in method.Parameters)
        {
            Require(!parameter.HasMarshalInfo, "Unexpected parameter marshalling metadata.");
            Row(text, "parameter", parameter.Name, parameter.Attributes, parameter.ParameterType.FullName,
                parameter.HasConstant, Value(parameter.Constant), Attributes(parameter));
        }
        if (!method.HasBody) return text.ToString();
        Row(text, "body", method.Body.InitLocals, method.Body.MaxStackSize);
        foreach (var local in method.Body.Variables) Row(text, "local", local.VariableType.FullName);
        foreach (var instruction in method.Body.Instructions) Row(text, instruction.OpCode.Code, Operand(method, instruction.Operand));
        foreach (var handler in method.Body.ExceptionHandlers)
            Row(text, "handler", handler.HandlerType, handler.CatchType, Index(method, handler.TryStart), Index(method, handler.TryEnd),
                Index(method, handler.HandlerStart), Index(method, handler.HandlerEnd), Index(method, handler.FilterStart));
        return text.ToString();
    }

    private static object Operand(MethodDefinition method, object value)
    {
        var instruction = value as Instruction;
        if (instruction != null) return "target:" + Index(method, instruction);
        var targets = value as Instruction[];
        if (targets != null) return string.Join(",", targets.Select(target => Index(method, target).ToString(CultureInfo.InvariantCulture)));
        var local = value as VariableDefinition;
        if (local != null) return "local:" + local.Index + ":" + local.VariableType.FullName;
        var parameter = value as ParameterDefinition;
        if (parameter != null) return "argument:" + parameter.Index + ":" + parameter.ParameterType.FullName;
        return Value(value);
    }

    private static int Index(MethodDefinition method, Instruction instruction) { return instruction == null ? -1 : method.Body.Instructions.IndexOf(instruction); }

    private static string Generics(IGenericParameterProvider provider)
    {
        var text = new StringBuilder();
        foreach (var parameter in provider.GenericParameters)
        {
            Row(text, parameter.Name, parameter.Position, parameter.Attributes, Attributes(parameter));
            foreach (var constraint in parameter.Constraints) Row(text, constraint.ConstraintType.FullName, Attributes(constraint));
        }
        return text.ToString();
    }

    private static string Security(ISecurityDeclarationProvider provider)
    {
        return string.Join(";", provider.SecurityDeclarations.Select(declaration => declaration.Action + ":" + Hex(declaration.GetBlob())));
    }

    private static string Attributes(ICustomAttributeProvider provider)
    {
        var text = new StringBuilder();
        foreach (var attribute in provider.CustomAttributes)
        {
            Row(text, attribute.Constructor.FullName);
            foreach (var argument in attribute.ConstructorArguments) Row(text, "argument", Argument(argument));
            foreach (var field in attribute.Fields) Row(text, "field", field.Name, Argument(field.Argument));
            foreach (var property in attribute.Properties) Row(text, "property", property.Name, Argument(property.Argument));
        }
        return text.ToString();
    }

    private static string Argument(CustomAttributeArgument argument)
    {
        var array = argument.Value as CustomAttributeArgument[];
        return argument.Type.FullName + ":" + (array == null ? Value(argument.Value) : string.Join(",", array.Select(Argument)));
    }

    private static string Value(object value)
    {
        if (value == null) return "<null>";
        if (value is byte[]) return Hex((byte[])value);
        var type = value as TypeReference;
        if (type != null) return type.FullName;
        var member = value as MemberReference;
        if (member != null) return member.FullName;
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static void Row(StringBuilder text, params object[] values)
    {
        foreach (object value in values)
        {
            string encoded = Value(value);
            text.Append(encoded.Length).Append(':').Append(encoded);
        }
        text.Append('\n');
    }

    private static string HashFile(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(stream)); }
    private static string HashText(string value) { using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(value))); }
    private static string Hex(byte[] value) { return value == null ? "" : BitConverter.ToString(value).Replace("-", "").ToLowerInvariant(); }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}

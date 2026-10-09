using Carbon.Compat.Converters;
using Carbon.Compatibility;

namespace Carbon.Compat.Patches;

/// <summary>Adapts legacy Unity truthiness calls fed directly by the current inventory proxy.</summary>
public sealed class PlayerInventoryProxyPatch : IAssemblyPatch
{
	public void Apply(ModuleDefinition assembly, ReferenceImporter importer, ref BaseConverter.Context context)
	{
		// Older Rust layouts still require Unity's component lifetime semantics.
		if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(PlayerInventory)))
			return;
		if (!typeof(EntityComponentProxyBase).IsAssignableFrom(typeof(PlayerInventory)))
			throw new InvalidOperationException("Unreviewed PlayerInventory layout for legacy truthiness conversion.");

		IMethodDescriptor exists = null;
		foreach (TypeDefinition type in assembly.GetAllTypes())
		foreach (MethodDefinition method in type.Methods)
		{
			if (method.MethodBody is not CilMethodBody body)
				continue;
			for (int index = 1; index < body.Instructions.Count; index++)
			{
				CilInstruction instruction = body.Instructions[index];
				if (instruction.OpCode != CilOpCodes.Call ||
					instruction.Operand is not MemberReference truthiness ||
					truthiness.DeclaringType.FullName != "UnityEngine.Object" ||
					truthiness.DeclaringType.DefinitionAssembly()?.Name != "UnityEngine.CoreModule" ||
					truthiness.Name != "op_Implicit" ||
					truthiness.Signature is not MethodSignature signature || signature.HasThis ||
					signature.ReturnType.FullName != "System.Boolean" || signature.ParameterTypes.Count != 1 ||
					signature.ParameterTypes[0].FullName != "UnityEngine.Object" ||
					!LoadsInventory(body.Instructions[index - 1]))
					continue;

				exists ??= importer.ImportMethod(typeof(LegacyPlayerInventory).GetMethod(nameof(LegacyPlayerInventory.Exists)));
				// Keep the same instruction, offsets and branch/exception-handler targets.
				instruction.Operand = exists;
			}
		}
	}

	private static bool LoadsInventory(CilInstruction instruction)
	{
		if ((instruction.OpCode == CilOpCodes.Call || instruction.OpCode == CilOpCodes.Callvirt) &&
			instruction.Operand is MemberReference getter &&
			getter.DeclaringType.FullName == "BasePlayer" &&
			getter.DeclaringType.DefinitionAssembly()?.Name == "Assembly-CSharp" &&
			getter.Name == "get_inventory" && getter.Signature is MethodSignature signature &&
			signature.HasThis && signature.ParameterTypes.Count == 0 && IsInventory(signature.ReturnType))
			return true;
		return instruction.OpCode == CilOpCodes.Ldfld && instruction.Operand is MemberReference field &&
			field.Signature is FieldSignature fieldSignature && IsInventory(fieldSignature.FieldType);
	}

	private static bool IsInventory(TypeSignature type)
	{
		return type.FullName == "PlayerInventory" && type.DefinitionAssembly()?.Name == "Assembly-CSharp";
	}
}

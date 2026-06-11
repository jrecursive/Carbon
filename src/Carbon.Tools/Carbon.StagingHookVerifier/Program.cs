using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using Carbon.Hooks;

namespace Carbon.StagingHookVerifier;

internal static class Program
{
	private const int HookFlagsStatic = 1;
	private const int HookFlagsPatch = 2;
	private const int HookFlagsMetadataOnly = 16;
	private const string HarmonyId = "carbon.staging.hook.verifier";

	private static readonly List<string> AssemblySearchPaths = new();
	private static readonly Dictionary<string, Assembly> AssemblyCache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> ResolveMisses = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Harmony PatchHarmony = new(HarmonyId);

	private static int Main(string[] args)
	{
		Options options;

		try
		{
			options = Options.Parse(args);
			options.Validate();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.Message);
			Options.PrintUsage();
			return 2;
		}

		ConfigureAssemblyResolution(options);

		List<VerificationFailure> failures = new();
		VerificationStats stats = new();
		HashSet<string> seenHookFullNames = new(StringComparer.Ordinal);
		List<HookMetadata> hooks = new();

		try
		{
			LoadBootstrapAssemblies(options);

			foreach (string hookFile in Directory.EnumerateFiles(options.HooksDir, "Carbon.Hooks.*.dll").OrderBy(Path.GetFileName))
			{
				CollectHookAssembly(hookFile, hooks, seenHookFullNames, stats);
			}

			if (!string.IsNullOrWhiteSpace(options.DumpHookFullName))
			{
				return DumpHookTarget(hooks, options.DumpHookFullName) ? 0 : 1;
			}

			VerifyHooks(hooks, options, failures, stats);

			if (options.VerifyCompatibilityShims)
			{
				VerifyStagingCompatibilityManifest(seenHookFullNames, failures, stats);
			}
		}
		catch (Exception ex)
		{
			failures.Add(new VerificationFailure("<verifier>", "fatal", ex.Message));
		}
		finally
		{
			try
			{
				PatchHarmony.UnpatchAll(HarmonyId);
			}
			catch
			{
				// Nothing to clean up if Harmony never reached patch installation.
			}
		}

		PrintSummary(stats, failures);
		return failures.Count == 0 ? 0 : 1;
	}

	private static void CollectHookAssembly(
		string hookFile,
		List<HookMetadata> hooks,
		HashSet<string> seenHookFullNames,
		VerificationStats stats)
	{
		Assembly assembly = LoadAssembly(hookFile);

		foreach (TypeInfo type in assembly.DefinedTypes)
		{
			if (!TryReadHookMetadata(type, out HookMetadata metadata))
			{
				continue;
			}

			stats.TotalHooks++;
			seenHookFullNames.Add(metadata.HookFullName);
			hooks.Add(metadata);
		}
	}

	private static void VerifyHooks(
		List<HookMetadata> hooks,
		Options options,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		Dictionary<string, List<HookVerificationItem>> hookItemsByFullName = new(StringComparer.Ordinal);
		List<HookVerificationItem> hookItems = new();
		List<HookVerificationItem> patchItems = new();

		foreach (HookMetadata metadata in hooks)
		{
			if (StagingHookCompatManifest.ShouldSuppressGeneratedHook(metadata.HookFullName, out StagingHookCompatEntry entry)
			    && !options.StrictNoSuppression)
			{
				if (!options.AllowedSuppressions.Contains(metadata.HookFullName))
				{
					failures.Add(new VerificationFailure(metadata.DisplayName, "suppression", "Generated hook suppression requires --allow-suppression-file."));
					continue;
				}

				stats.SuppressedHooks++;

				switch (entry.Action)
				{
					case StagingHookCompatAction.Shim:
						stats.ShimHooks++;
						break;

					case StagingHookCompatAction.DisabledObsolete:
						stats.DisabledHooks++;
						break;
				}

				continue;
			}

			if (!options.AllGeneratedHooks && !StagingHookCompatManifest.TryGet(metadata.HookFullName, out _))
			{
				stats.UnwatchedHooks++;
				continue;
			}

			stats.WatchedHooks++;

			if (metadata.IsMetadataOnly)
			{
				stats.MetadataOnlyHooks++;
				continue;
			}

			if (string.IsNullOrEmpty(metadata.Target) || string.IsNullOrEmpty(metadata.Method))
			{
				failures.Add(new VerificationFailure(metadata.DisplayName, "metadata", "Missing target type or target method."));
				continue;
			}

			Type? targetType = FindType(metadata.Target);
			if (targetType == null)
			{
				failures.Add(new VerificationFailure(metadata.DisplayName, "signature", $"Target type '{metadata.Target}' was not found."));
				continue;
			}

			MethodBase? targetMethod = ResolveTargetMethod(targetType, metadata);
			if (targetMethod == null)
			{
				failures.Add(new VerificationFailure(metadata.DisplayName, "signature", $"Signature for '{metadata.Target}.{metadata.Method}({string.Join(", ", metadata.MethodArgs)})' was not found."));
				continue;
			}

			stats.ResolvedHooks++;

			if (!metadata.IsPatch)
			{
				stats.NonPatchHooks++;
			}

			MethodInfo? prefix = AccessTools.Method(metadata.Type.AsType(), "Prefix");
			MethodInfo? postfix = AccessTools.Method(metadata.Type.AsType(), "Postfix");
			MethodInfo? transpiler = AccessTools.Method(metadata.Type.AsType(), "Transpiler");

			if (prefix == null && postfix == null && transpiler == null)
			{
				failures.Add(new VerificationFailure(metadata.DisplayName, "patch", "No Prefix, Postfix, or Transpiler method was found."));
				continue;
			}

			HookVerificationItem item = new(metadata, targetMethod, prefix, postfix, transpiler);
			hookItems.Add(item);
			if (metadata.IsPatch)
			{
				patchItems.Add(item);
			}

			if (!hookItemsByFullName.TryGetValue(metadata.HookFullName, out List<HookVerificationItem>? sameName))
			{
				sameName = new List<HookVerificationItem>();
				hookItemsByFullName[metadata.HookFullName] = sameName;
			}

			sameName.Add(item);
		}

		VerifyPatchItems(patchItems, hookItemsByFullName, failures, stats);
	}

	private static void VerifyPatchItems(
		List<HookVerificationItem> hookItems,
		Dictionary<string, List<HookVerificationItem>> hookItemsByFullName,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		foreach (HookVerificationItem item in hookItems)
		{
			List<HookVerificationItem> installOrder = new();
			HashSet<HookVerificationItem> visited = new();
			HashSet<HookVerificationItem> visiting = new();

			try
			{
				AddPatchInstallOrder(item, hookItemsByFullName, installOrder, visited, visiting);
				foreach (HookVerificationItem patch in installOrder)
				{
					PatchHarmony.Patch(
						patch.TargetMethod,
						patch.Prefix == null ? null : new HarmonyMethod(patch.Prefix, Priority.VeryHigh),
						patch.Postfix == null ? null : new HarmonyMethod(patch.Postfix, Priority.VeryHigh),
						patch.Transpiler == null ? null : new HarmonyMethod(patch.Transpiler, Priority.VeryHigh));
				}

				stats.PatchedHooks++;
			}
			catch (Exception ex)
			{
				failures.Add(new VerificationFailure(item.Metadata.DisplayName, "patch", Unwrap(ex).Message));
			}
			finally
			{
				try
				{
					PatchHarmony.UnpatchAll(HarmonyId);
				}
				catch
				{
					// Continue collecting failures from other hooks.
				}
			}
		}
	}

	private static void AddPatchInstallOrder(
		HookVerificationItem item,
		Dictionary<string, List<HookVerificationItem>> patchItemsByFullName,
		List<HookVerificationItem> installOrder,
		HashSet<HookVerificationItem> visited,
		HashSet<HookVerificationItem> visiting)
	{
		if (visited.Contains(item))
		{
			return;
		}

		if (!visiting.Add(item))
		{
			throw new InvalidOperationException($"Circular hook dependency detected at '{item.Metadata.HookFullName}'.");
		}

		foreach (string dependency in item.Metadata.Dependencies)
		{
			if (!patchItemsByFullName.TryGetValue(dependency, out List<HookVerificationItem>? dependencyItems) || dependencyItems.Count == 0)
			{
				throw new InvalidOperationException($"Declared dependency '{dependency}' was not found.");
			}

			foreach (HookVerificationItem dependencyItem in dependencyItems)
			{
				AddPatchInstallOrder(dependencyItem, patchItemsByFullName, installOrder, visited, visiting);
			}
		}

		visiting.Remove(item);
		visited.Add(item);
		installOrder.Add(item);
	}

	private static void VerifyStagingCompatibilityManifest(HashSet<string> seenHookFullNames, List<VerificationFailure> failures, VerificationStats stats)
	{
		foreach (StagingHookCompatEntry entry in StagingHookCompatManifest.All)
		{
			if (string.IsNullOrWhiteSpace(entry.HookFullName))
			{
				failures.Add(new VerificationFailure("<staging manifest>", "manifest", "A compatibility entry has an empty hook full name."));
			}

			if (string.IsNullOrWhiteSpace(entry.Reason))
			{
				failures.Add(new VerificationFailure(entry.HookFullName, "manifest", "Compatibility entry is missing a reason."));
			}

			if (!seenHookFullNames.Contains(entry.HookFullName))
			{
				stats.ManifestEntriesNotPresent++;
			}
		}

		VerifyStagingRustIlCompat(failures, stats);
	}

	private static bool DumpHookTarget(List<HookMetadata> hooks, string hookFullName)
	{
		HookMetadata metadata = hooks.FirstOrDefault(hook => string.Equals(hook.HookFullName, hookFullName, StringComparison.Ordinal));
		if (metadata.Type == null)
		{
			Console.Error.WriteLine($"Hook '{hookFullName}' was not found.");
			return false;
		}

		Type? targetType = FindType(metadata.Target);
		if (targetType == null)
		{
			Console.Error.WriteLine($"Target type '{metadata.Target}' was not found.");
			return false;
		}

		MethodBase? targetMethod = ResolveTargetMethod(targetType, metadata);
		if (targetMethod == null)
		{
			Console.Error.WriteLine($"Signature for '{metadata.Target}.{metadata.Method}({string.Join(", ", metadata.MethodArgs)})' was not found.");
			return false;
		}

		Console.WriteLine($"hook: {metadata.HookFullName}");
		Console.WriteLine($"target: {targetMethod.DeclaringType?.FullName}.{targetMethod.Name}");
		Console.WriteLine($"signature: {metadata.Target}.{metadata.Method}({string.Join(", ", metadata.MethodArgs)})");

		MethodBody? body = targetMethod.GetMethodBody();
		if (body == null)
		{
			Console.WriteLine("locals: <none>");
		}
		else
		{
			Console.WriteLine($"locals: {body.LocalVariables.Count}");
			foreach (LocalVariableInfo local in body.LocalVariables)
			{
				Console.WriteLine($"  L_{local.LocalIndex}: {local.LocalType.FullName}");
			}
		}

		List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(targetMethod, out _);
		Console.WriteLine($"instructions: {instructions.Count}");
		for (int i = 0; i < instructions.Count; i++)
		{
			CodeInstruction instruction = instructions[i];
			string operand = instruction.operand switch
			{
				null => string.Empty,
				MethodBase method => $"{method.DeclaringType?.FullName}.{method.Name}",
				FieldInfo field => $"{field.DeclaringType?.FullName}.{field.Name}",
				Label label => $"label:{label.GetHashCode()}",
				Label[] labels => string.Join(", ", labels.Select(label => $"label:{label.GetHashCode()}")),
				_ => instruction.operand.ToString() ?? string.Empty
			};

			Console.WriteLine($"{i:0000}: {instruction.opcode,-12} {operand}");
		}

		return true;
	}

	private static void VerifyStagingRustIlCompat(List<VerificationFailure> failures, VerificationStats stats)
	{
		Type? compatType = FindType("Carbon.Hooks.StagingRustIlCompat");
		if (compatType == null)
		{
			failures.Add(new VerificationFailure("StagingRustIlCompat", "shim", "Carbon.Hooks.StagingRustIlCompat was not found in Carbon.dll."));
			return;
		}

		ShimTranspilerSpec[] transpilers =
		{
			new("BaseFishingRod.Server_RequestCast", "BaseFishingRod", "Server_RequestCast", new[] { "BaseEntity+RPCMessage" }, "ServerRequestCastTranspiler", new[] { "ShouldCancelFishingCast" }),
			new("BaseFishingRod.CatchProcessBudgeted", "BaseFishingRod", "CatchProcessBudgeted", Array.Empty<string>(), "CatchProcessBudgetedTranspiler", new[] { "ShouldCancelFishCatch", "OnFishCatchCompat" }),
			new("ResourceDispenser.GiveResourceFromItem", "ResourceDispenser", "GiveResourceFromItem", new[] { "BasePlayer", "ItemAmount", "System.Single", "System.Single", "AttackEntity" }, "GiveResourceFromItemTranspiler", new[] { "ShouldSkipDispenserGather", "OnDispenserGatheredCompat" }),
			new("ResourceDispenser.AssignFinishBonus", "ResourceDispenser", "AssignFinishBonus", new[] { "BasePlayer", "System.Single", "AttackEntity" }, "AssignFinishBonusTranspiler", new[] { "OnDispenserBonusCompat", "OnDispenserBonusReceivedCompat" }),
			new("ItemCrafter.CraftItem", "ItemCrafter", "CraftItem", new[] { "ItemBlueprint", "BasePlayer", "ProtoBuf.Item+InstanceData", "System.Int32", "System.Int32", "Item", "System.Boolean", "System.Int32" }, "CraftItemTranspiler", new[] { "OnItemCraftCompat" })
		};

		foreach (ShimTranspilerSpec spec in transpilers)
		{
			VerifyTranspilerShim(compatType, spec, failures, stats);
		}

		ShimPatchSpec[] patches =
		{
			new("NPCPlayer.CreateCorpse", "NPCPlayer", "CreateCorpse", new[] { "BasePlayer+PlayerFlags", "UnityEngine.Vector3", "UnityEngine.Quaternion", "System.Collections.Generic.List<TriggerBase>", "System.Boolean" }, "CreateCorpsePostfix", ShimPatchKind.Postfix),
			new("BasePlayer.OnReceivedVoice", "BasePlayer", "OnReceivedVoice", new[] { "System.ReadOnlySpan<System.Byte>" }, "OnReceivedVoicePrefix", ShimPatchKind.Prefix)
		};

		foreach (ShimPatchSpec spec in patches)
		{
			VerifyPatchShim(compatType, spec, failures, stats);
		}
	}

	private static void VerifyTranspilerShim(Type compatType, ShimTranspilerSpec spec, List<VerificationFailure> failures, VerificationStats stats)
	{
		if (!TryResolveShimTarget(spec.Description, spec.TargetType, spec.TargetMethod, spec.TargetArgs, failures, out MethodInfo? target))
		{
			return;
		}

		MethodInfo? transpiler = AccessTools.Method(compatType, spec.TranspilerMethod);
		if (transpiler == null)
		{
			failures.Add(new VerificationFailure(spec.Description, "shim", $"Compatibility transpiler '{spec.TranspilerMethod}' was not found."));
			return;
		}

		try
		{
			List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
			object? result = transpiler.Invoke(null, new object[] { original, generator });
			if (result is not IEnumerable<CodeInstruction> rewritten)
			{
				failures.Add(new VerificationFailure(spec.Description, "shim", $"Compatibility transpiler '{spec.TranspilerMethod}' did not return IL instructions."));
				return;
			}

			List<CodeInstruction> instructions = rewritten.ToList();
			foreach (string expectedCall in spec.ExpectedHelperCalls)
			{
				if (!CallsCompatHelper(instructions, compatType, expectedCall))
				{
					failures.Add(new VerificationFailure(spec.Description, "shim", $"Compatibility transpiler '{spec.TranspilerMethod}' did not insert '{expectedCall}'."));
				}
			}

			PatchHarmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
			stats.ShimTranspilerChecks++;
		}
		catch (Exception ex)
		{
			failures.Add(new VerificationFailure(spec.Description, "shim", Unwrap(ex).Message));
		}
		finally
		{
			try
			{
				PatchHarmony.UnpatchAll(HarmonyId);
			}
			catch
			{
				// Continue validating the remaining compatibility shims.
			}
		}
	}

	private static void VerifyPatchShim(Type compatType, ShimPatchSpec spec, List<VerificationFailure> failures, VerificationStats stats)
	{
		if (!TryResolveShimTarget(spec.Description, spec.TargetType, spec.TargetMethod, spec.TargetArgs, failures, out MethodInfo? target))
		{
			return;
		}

		MethodInfo? patchMethod = AccessTools.Method(compatType, spec.PatchMethod);
		if (patchMethod == null)
		{
			failures.Add(new VerificationFailure(spec.Description, "shim", $"Compatibility patch method '{spec.PatchMethod}' was not found."));
			return;
		}

		try
		{
			if (spec.Kind == ShimPatchKind.Prefix)
			{
				PatchHarmony.Patch(target, prefix: new HarmonyMethod(patchMethod));
			}
			else
			{
				PatchHarmony.Patch(target, postfix: new HarmonyMethod(patchMethod));
			}

			stats.ShimPatchChecks++;
		}
		catch (Exception ex)
		{
			failures.Add(new VerificationFailure(spec.Description, "shim", Unwrap(ex).Message));
		}
		finally
		{
			try
			{
				PatchHarmony.UnpatchAll(HarmonyId);
			}
			catch
			{
				// Continue validating the remaining compatibility shims.
			}
		}
	}

	private static bool TryResolveShimTarget(
		string description,
		string targetTypeName,
		string targetMethodName,
		string[] targetArgNames,
		List<VerificationFailure> failures,
		out MethodInfo? target)
	{
		target = null;

		Type? targetType = FindType(targetTypeName);
		if (targetType == null)
		{
			failures.Add(new VerificationFailure(description, "shim", $"Compatibility target type '{targetTypeName}' was not found."));
			return false;
		}

		Type[]? targetArgTypes = ResolveTypeReferences(targetArgNames);
		if (targetArgTypes == null)
		{
			failures.Add(new VerificationFailure(description, "shim", $"One or more compatibility target argument types could not be resolved: {string.Join(", ", targetArgNames)}."));
			return false;
		}

		target = AccessTools.Method(targetType, targetMethodName, targetArgTypes);
		if (target == null)
		{
			failures.Add(new VerificationFailure(description, "shim", $"Compatibility target signature '{targetTypeName}.{targetMethodName}({string.Join(", ", targetArgNames)})' was not found."));
			return false;
		}

		return true;
	}

	private static bool CallsCompatHelper(List<CodeInstruction> instructions, Type compatType, string methodName)
	{
		return instructions.Any(instruction =>
			instruction.operand is MethodBase method &&
			method.DeclaringType == compatType &&
			method.Name == methodName);
	}

	private static MethodBase? ResolveTargetMethod(Type targetType, HookMetadata metadata)
	{
		return metadata.MethodType switch
		{
			"Getter" => AccessTools.PropertyGetter(targetType, metadata.Method),
			"Setter" => AccessTools.PropertySetter(targetType, metadata.Method),
			_ => ResolveNormalTargetMethod(targetType, metadata)
		};
	}

	private static MethodBase? ResolveNormalTargetMethod(Type targetType, HookMetadata metadata)
	{
		Type[]? argumentTypes = ResolveArgumentTypes(metadata.MethodArgs);

		if (argumentTypes != null)
		{
			MethodInfo? exact = AccessTools.Method(targetType, metadata.Method, argumentTypes);
			if (exact != null)
			{
				return exact;
			}
		}

		MethodInfo? localFunction = ResolveCompilerGeneratedLocalFunction(targetType, metadata, argumentTypes);
		if (localFunction != null)
		{
			return localFunction;
		}

		List<MethodInfo> candidates = AccessTools.GetDeclaredMethods(targetType)
			.Where(method => method.Name == metadata.Method)
			.ToList();

		if (argumentTypes == null && metadata.MethodArgs.Length == 0 && candidates.Count == 1)
		{
			return candidates[0];
		}

		if (argumentTypes == null)
		{
			return null;
		}

		return candidates.SingleOrDefault(method => ParametersMatch(method.GetParameters(), argumentTypes, metadata.MethodArgs));
	}

	private static MethodInfo? ResolveCompilerGeneratedLocalFunction(Type targetType, HookMetadata metadata, Type[]? argumentTypes)
	{
		if (argumentTypes == null)
		{
			return null;
		}

		int suffixIndex = metadata.Method.LastIndexOf('|');
		if (suffixIndex < 0 || !metadata.Method.StartsWith('<') || !metadata.Method.Contains(">g__", StringComparison.Ordinal))
		{
			return null;
		}

		string stablePrefix = metadata.Method[..(suffixIndex + 1)];
		MethodInfo? result = null;

		foreach (MethodInfo method in AccessTools.GetDeclaredMethods(targetType))
		{
			if (!method.Name.StartsWith(stablePrefix, StringComparison.Ordinal))
			{
				continue;
			}

			if (!ParametersMatch(method.GetParameters(), argumentTypes, metadata.MethodArgs))
			{
				continue;
			}

			if (result != null)
			{
				return null;
			}

			result = method;
		}

		return result;
	}

	private static bool ParametersMatch(ParameterInfo[] parameters, Type[] argumentTypes, string[] argumentTypeNames)
	{
		if (parameters.Length != argumentTypes.Length)
		{
			return false;
		}

		for (int i = 0; i < parameters.Length; i++)
		{
			Type actual = UnwrapByRef(parameters[i].ParameterType);
			Type expected = UnwrapByRef(argumentTypes[i]);

			if (actual == expected)
			{
				continue;
			}

			string expectedName = argumentTypeNames[i];
			if (string.Equals(actual.FullName, expectedName, StringComparison.Ordinal) ||
			    string.Equals(actual.Name, expectedName, StringComparison.Ordinal))
			{
				continue;
			}

			return false;
		}

		return true;
	}

	private static Type UnwrapByRef(Type type)
	{
		return type.IsByRef ? type.GetElementType() ?? type : type;
	}

	private static Type[]? ResolveArgumentTypes(string[] argumentNames)
	{
		return ResolveTypeReferences(argumentNames);
	}

	private static Type[]? ResolveTypeReferences(string[] typeNames)
	{
		Type[] types = new Type[typeNames.Length];

		for (int i = 0; i < typeNames.Length; i++)
		{
			Type? type = ResolveTypeReference(typeNames[i]);
			if (type == null)
			{
				return null;
			}

			types[i] = type;
		}

		return types;
	}

	private static Type? ResolveTypeReference(string typeName)
	{
		if (string.IsNullOrWhiteSpace(typeName))
		{
			return null;
		}

		int genericStart = typeName.IndexOf('<');
		if (genericStart > 0 && typeName.EndsWith('>'))
		{
			string genericName = typeName[..genericStart];
			string genericArgsText = typeName[(genericStart + 1)..^1];
			string[] genericArgNames = SplitGenericArguments(genericArgsText);
			Type[]? genericArgs = ResolveTypeReferences(genericArgNames);
			if (genericArgs == null)
			{
				return null;
			}

			string genericDefinitionName = genericName.Contains('`', StringComparison.Ordinal)
				? genericName
				: $"{genericName}`{genericArgs.Length}";
			Type? genericType = genericName switch
			{
				"System.Collections.Generic.List" => typeof(List<>),
				"System.ReadOnlySpan" => typeof(ReadOnlySpan<>),
				_ => FindType(genericDefinitionName)
			};

			return genericType == null ? null : genericType.MakeGenericType(genericArgs);
		}

		return FindType(typeName);
	}

	private static string[] SplitGenericArguments(string value)
	{
		List<string> result = new();
		int depth = 0;
		int start = 0;

		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			if (c == '<')
			{
				depth++;
			}
			else if (c == '>')
			{
				depth--;
			}
			else if (c == ',' && depth == 0)
			{
				result.Add(value[start..i].Trim());
				start = i + 1;
			}
		}

		result.Add(value[start..].Trim());
		return result.ToArray();
	}

	private static bool TryReadHookMetadata(TypeInfo type, out HookMetadata metadata)
	{
		metadata = default!;

		if (!IsLoadableHookType(type.AsType()))
		{
			return false;
		}

		object? patchAttribute = type.GetCustomAttributes(false).FirstOrDefault(IsPatchAttribute);
		if (patchAttribute == null)
		{
			return false;
		}

		string hookName = GetProperty<string>(patchAttribute, "Name") ?? string.Empty;
		string hookFullName = GetProperty<string>(patchAttribute, "FullName") ?? string.Empty;
		string target = GetProperty<string>(patchAttribute, "Target") ?? string.Empty;
		string method = GetProperty<string>(patchAttribute, "Method") ?? string.Empty;
		string[] methodArgs = GetProperty<string[]>(patchAttribute, "MethodArgs") ?? Array.Empty<string>();
		string methodType = GetProperty<object>(patchAttribute, "MethodType")?.ToString() ?? "Normal";
		string? identifier = type.GetCustomAttributes(false)
			.FirstOrDefault(IsIdentifierAttribute)
			?.GetType()
			.GetProperty("Value")
			?.GetValue(type.GetCustomAttributes(false).FirstOrDefault(IsIdentifierAttribute)) as string;
		string[] dependencies = GetProperty<string[]>(
			type.GetCustomAttributes(false).FirstOrDefault(IsDependenciesAttribute) ?? new object(),
			"Value") ?? Array.Empty<string>();
		int options = ReadOptions(type);

		metadata = new HookMetadata(
			type,
			hookName,
			hookFullName,
			target,
			method,
			methodArgs,
			methodType,
			identifier ?? type.FullName ?? type.Name,
			dependencies,
			(options & HookFlagsPatch) != 0,
			(options & HookFlagsStatic) != 0,
			(options & HookFlagsMetadataOnly) != 0);

		return !string.IsNullOrEmpty(hookName) && !string.IsNullOrEmpty(hookFullName);
	}

	private static bool IsLoadableHookType(Type type)
	{
		for (Type? current = type; current != null; current = current.BaseType)
		{
			if (current.FullName == "API.Hooks.Patch")
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsPatchAttribute(object attribute)
	{
		return attribute.GetType().FullName == "API.Hooks.HookAttribute+Patch"
		       || attribute.GetType().FullName == "API.Hooks.HookAttribute.Patch";
	}

	private static bool IsIdentifierAttribute(object attribute)
	{
		return attribute.GetType().FullName == "API.Hooks.HookAttribute+Identifier"
		       || attribute.GetType().FullName == "API.Hooks.HookAttribute.Identifier";
	}

	private static bool IsOptionsAttribute(object attribute)
	{
		return attribute.GetType().FullName == "API.Hooks.HookAttribute+Options"
		       || attribute.GetType().FullName == "API.Hooks.HookAttribute.Options";
	}

	private static bool IsDependenciesAttribute(object attribute)
	{
		return attribute.GetType().FullName == "API.Hooks.HookAttribute+Dependencies"
		       || attribute.GetType().FullName == "API.Hooks.HookAttribute.Dependencies";
	}

	private static T? GetProperty<T>(object instance, string name)
	{
		object? value = instance.GetType().GetProperty(name)?.GetValue(instance);
		return value is T typed ? typed : default;
	}

	private static int ReadOptions(TypeInfo type)
	{
		object? optionsAttribute = type.GetCustomAttributes(false).FirstOrDefault(IsOptionsAttribute);
		object? value = optionsAttribute?.GetType().GetProperty("Value")?.GetValue(optionsAttribute);
		return value == null ? 0 : Convert.ToInt32(value);
	}

	private static Type? FindType(string typeName)
	{
		if (string.IsNullOrWhiteSpace(typeName))
		{
			return null;
		}

		foreach (string candidateName in TypeNameCandidates(typeName))
		{
			Type? type = Type.GetType(candidateName, false);
			if (type != null)
			{
				return type;
			}

			type = AccessTools.TypeByName(candidateName);
			if (type != null)
			{
				return type;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = assembly.GetType(candidateName, false);
				if (type != null)
				{
					return type;
				}
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					type = assembly.GetTypes().FirstOrDefault(candidate =>
						string.Equals(candidate.FullName, candidateName, StringComparison.Ordinal) ||
						string.Equals(candidate.Name, candidateName, StringComparison.Ordinal));
				}
				catch (ReflectionTypeLoadException ex)
				{
					type = ex.Types.Where(candidate => candidate != null).FirstOrDefault(candidate =>
						string.Equals(candidate!.FullName, candidateName, StringComparison.Ordinal) ||
						string.Equals(candidate.Name, candidateName, StringComparison.Ordinal));
				}

				if (type != null)
				{
					return type;
				}
			}
		}

		return null;
	}

	private static IEnumerable<string> TypeNameCandidates(string typeName)
	{
		HashSet<string> candidates = new(StringComparer.Ordinal) { typeName };

		if (typeName.Contains('/'))
		{
			candidates.Add(typeName.Replace('/', '+'));
		}

		int lastDot = typeName.LastIndexOf('.');
		if (lastDot > 0)
		{
			candidates.Add($"{typeName[..lastDot]}+{typeName[(lastDot + 1)..]}");
		}

		foreach (string candidate in candidates)
		{
			yield return candidate;
		}
	}

	private static void ConfigureAssemblyResolution(Options options)
	{
		AddSearchPath(options.HooksDir);
		AddSearchPath(options.CarbonManaged);
		AddSearchPath(Path.Combine(options.CarbonManaged, "lib"));
		AddSearchPath(Path.Combine(options.ServerRoot, "RustDedicated_Data", "Managed"));
		AddSearchPath(AppContext.BaseDirectory);

		AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ResolveAssembly(new AssemblyName(args.Name).Name);
	}

	private static void LoadBootstrapAssemblies(Options options)
	{
		LoadDirectory(Path.Combine(options.ServerRoot, "RustDedicated_Data", "Managed"));
		LoadDirectory(options.CarbonManaged);
		LoadDirectory(Path.Combine(options.CarbonManaged, "lib"));
	}

	private static void LoadDirectory(string directory)
	{
		if (!Directory.Exists(directory))
		{
			return;
		}

		foreach (string file in Directory.EnumerateFiles(directory, "*.dll").OrderBy(Path.GetFileName))
		{
			try
			{
				LoadAssembly(file);
			}
			catch
			{
				// Some Unity/runtime assemblies are not needed for metadata or patch verification.
			}
		}
	}

	private static Assembly LoadAssembly(string file)
	{
		string name = AssemblyName.GetAssemblyName(file).Name ?? Path.GetFileNameWithoutExtension(file);

		if (AssemblyCache.TryGetValue(name, out Assembly? cached))
		{
			return cached;
		}

		Assembly assembly = Assembly.LoadFrom(file);
		AssemblyCache[name] = assembly;
		return assembly;
	}

	private static Assembly? ResolveAssembly(string? name)
	{
		if (string.IsNullOrEmpty(name) || ResolveMisses.Contains(name))
		{
			return null;
		}

		if (AssemblyCache.TryGetValue(name, out Assembly? cached))
		{
			return cached;
		}

		foreach (string searchPath in AssemblySearchPaths)
		{
			string candidate = Path.Combine(searchPath, $"{name}.dll");
			if (!File.Exists(candidate))
			{
				continue;
			}

			try
			{
				return LoadAssembly(candidate);
			}
			catch
			{
				// Try the next search path.
			}
		}

		ResolveMisses.Add(name);
		return null;
	}

	private static void AddSearchPath(string path)
	{
		if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !AssemblySearchPaths.Contains(path))
		{
			AssemblySearchPaths.Add(path);
		}
	}

	private static Exception Unwrap(Exception exception)
	{
		while (exception is TargetInvocationException or TypeInitializationException && exception.InnerException != null)
		{
			exception = exception.InnerException;
		}

		return exception;
	}

	private static void PrintSummary(VerificationStats stats, List<VerificationFailure> failures)
	{
		Console.WriteLine($"hooks: total={stats.TotalHooks} watched={stats.WatchedHooks} unwatched={stats.UnwatchedHooks} resolved={stats.ResolvedHooks} patched={stats.PatchedHooks} metadataOnly={stats.MetadataOnlyHooks} nonPatch={stats.NonPatchHooks}");
		Console.WriteLine($"staging-compat: suppressed={stats.SuppressedHooks} shims={stats.ShimHooks} disabled={stats.DisabledHooks} manifestMissingFromHooks={stats.ManifestEntriesNotPresent}");
		Console.WriteLine($"shim-validation: transpilers={stats.ShimTranspilerChecks} prefixPostfix={stats.ShimPatchChecks}");

		if (failures.Count == 0)
		{
			Console.WriteLine("verification: ok");
			return;
		}

		Console.Error.WriteLine($"verification: failed ({failures.Count})");
		foreach (VerificationFailure failure in failures)
		{
			Console.Error.WriteLine($"[{failure.Kind}] {failure.Hook}: {failure.Message}");
		}
	}

	private sealed record Options(
		string ServerRoot,
		string CarbonManaged,
		string HooksDir,
		bool AllGeneratedHooks,
		bool StrictNoSuppression,
		bool VerifyCompatibilityShims,
		IReadOnlySet<string> AllowedSuppressions,
		string? DumpHookFullName)
	{
		public static Options Parse(string[] args)
		{
			string serverRoot = "/home/johnm/rust-staging-autoupdate/server";
			string? carbonManaged = null;
			string? hooksDir = null;
			bool allGeneratedHooks = true;
			bool strictNoSuppression = true;
			bool verifyCompatibilityShims = false;
			string? allowSuppressionFile = null;
			string? dumpHookFullName = null;

			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
					case "-h":
					case "--help":
						PrintUsage();
						Environment.Exit(0);
						break;

					case "--server-root":
						serverRoot = RequireValue(args, ref i, "--server-root");
						break;

					case "--carbon-managed":
						carbonManaged = RequireValue(args, ref i, "--carbon-managed");
						break;

					case "--hooks-dir":
						hooksDir = RequireValue(args, ref i, "--hooks-dir");
						break;

					case "--all-generated":
						allGeneratedHooks = true;
						break;

					case "--focused-compat":
						allGeneratedHooks = false;
						strictNoSuppression = false;
						verifyCompatibilityShims = true;
						break;

					case "--strict-no-suppression":
						strictNoSuppression = true;
						break;

					case "--allow-suppression-file":
						allowSuppressionFile = RequireValue(args, ref i, "--allow-suppression-file");
						strictNoSuppression = false;
						break;

					case "--verify-compat-shims":
						verifyCompatibilityShims = true;
						break;

					case "--dump-hook":
						dumpHookFullName = RequireValue(args, ref i, "--dump-hook");
						break;

					default:
						throw new ArgumentException($"Unknown argument: {args[i]}");
				}
			}

			carbonManaged ??= Path.Combine(serverRoot, "carbon", "managed");
			hooksDir ??= Path.Combine(carbonManaged, "hooks");

			return new Options(
				Path.GetFullPath(serverRoot),
				Path.GetFullPath(carbonManaged),
				Path.GetFullPath(hooksDir),
				allGeneratedHooks,
				strictNoSuppression,
				verifyCompatibilityShims,
				LoadAllowedSuppressions(allowSuppressionFile),
				dumpHookFullName);
		}

		public void Validate()
		{
			RequireDirectory(ServerRoot, "server root");
			RequireDirectory(Path.Combine(ServerRoot, "RustDedicated_Data", "Managed"), "Rust managed directory");
			RequireFile(Path.Combine(ServerRoot, "RustDedicated_Data", "Managed", "Assembly-CSharp.dll"), "Assembly-CSharp.dll");
			RequireDirectory(CarbonManaged, "Carbon managed directory");
			RequireFile(Path.Combine(CarbonManaged, "Carbon.dll"), "Carbon.dll");
			RequireFile(Path.Combine(CarbonManaged, "Carbon.SDK.dll"), "Carbon.SDK.dll");
			RequireDirectory(HooksDir, "hooks directory");
		}

		private static string RequireValue(string[] args, ref int index, string option)
		{
			if (index + 1 >= args.Length)
			{
				throw new ArgumentException($"{option} requires a value.");
			}

			index++;
			return args[index];
		}

		private static void RequireDirectory(string path, string label)
		{
			if (!Directory.Exists(path))
			{
				throw new DirectoryNotFoundException($"Missing {label}: {path}");
			}
		}

		private static void RequireFile(string path, string label)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"Missing {label}: {path}", path);
			}
		}

		public static void PrintUsage()
		{
			Console.WriteLine("Usage: Carbon.StagingHookVerifier --server-root <path> [--carbon-managed <path>] [--hooks-dir <path>] [--all-generated] [--strict-no-suppression] [--allow-suppression-file <path>] [--dump-hook <hook-full-name>]");
		}

		private static IReadOnlySet<string> LoadAllowedSuppressions(string? path)
		{
			HashSet<string> hooks = new(StringComparer.Ordinal);
			if (string.IsNullOrWhiteSpace(path))
			{
				return hooks;
			}

			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement root = document.RootElement;
			JsonElement array = root.ValueKind == JsonValueKind.Array
				? root
				: root.TryGetProperty("hooks", out JsonElement hooksElement) ? hooksElement : default;

			if (array.ValueKind != JsonValueKind.Array)
			{
				throw new ArgumentException($"Suppression allow file must be an array or contain a 'hooks' array: {path}");
			}

			foreach (JsonElement item in array.EnumerateArray())
			{
				string? hook = item.ValueKind switch
				{
					JsonValueKind.String => item.GetString(),
					JsonValueKind.Object when item.TryGetProperty("hookFullName", out JsonElement hookFullName) => hookFullName.GetString(),
					JsonValueKind.Object when item.TryGetProperty("hook", out JsonElement hookName) => hookName.GetString(),
					_ => null
				};

				if (!string.IsNullOrWhiteSpace(hook))
				{
					hooks.Add(hook);
				}
			}

			return hooks;
		}
	}

	private readonly record struct HookMetadata(
		TypeInfo Type,
		string HookName,
		string HookFullName,
		string Target,
		string Method,
		string[] MethodArgs,
		string MethodType,
		string Identifier,
		string[] Dependencies,
		bool IsPatch,
		bool IsStatic,
		bool IsMetadataOnly)
	{
		public string DisplayName => $"{HookFullName} ({Type.FullName})";
	}

	private sealed record HookVerificationItem(
		HookMetadata Metadata,
		MethodBase TargetMethod,
		MethodInfo? Prefix,
		MethodInfo? Postfix,
		MethodInfo? Transpiler);

	private sealed class VerificationStats
	{
		public int TotalHooks;
		public int WatchedHooks;
		public int UnwatchedHooks;
		public int ResolvedHooks;
		public int PatchedHooks;
		public int MetadataOnlyHooks;
		public int NonPatchHooks;
		public int SuppressedHooks;
		public int ShimHooks;
		public int DisabledHooks;
		public int ManifestEntriesNotPresent;
		public int ShimTranspilerChecks;
		public int ShimPatchChecks;
	}

	private enum ShimPatchKind
	{
		Prefix,
		Postfix
	}

	private readonly record struct ShimTranspilerSpec(
		string Description,
		string TargetType,
		string TargetMethod,
		string[] TargetArgs,
		string TranspilerMethod,
		string[] ExpectedHelperCalls);

	private readonly record struct ShimPatchSpec(
		string Description,
		string TargetType,
		string TargetMethod,
		string[] TargetArgs,
		string PatchMethod,
		ShimPatchKind Kind);

	private readonly record struct VerificationFailure(string Hook, string Kind, string Message);
}

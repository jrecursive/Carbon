using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text.Json;
using HarmonyLib;
using Carbon.Hooks;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
	private const int HookFlagsStatic = 1;
	private const int HookFlagsPatch = 2;
	private const int HookFlagsMetadataOnly = 16;
	private const int OnPlayerDisconnectedHookId = 72085565;
	private const string OnPlayerDisconnectedHookName = "OnPlayerDisconnected";
	private const int OnMarketplaceTerminalPurchaseHookId = unchecked((int)2145652880u);
	private const string OnMarketplaceTerminalPurchaseHookName = "OnMarketplaceTerminalPurchase";
	private const int OnLoseConditionHookId = unchecked((int)2025192851u);
	private const string OnLoseConditionHookName = "OnLoseCondition";
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

			if (!string.IsNullOrWhiteSpace(options.ChildInstallHookFullName))
			{
				options = options with
				{
					InstallHookRequests = ExpandHookDependencyRequests(hooks, options.ChildInstallHookFullName)
				};
			}

			HookVerificationIndex index = BuildHookIndex(hooks, options, failures, stats);

			if (!string.IsNullOrWhiteSpace(options.ChildInstallHookFullName))
			{
				VerifyChildInstallHook(index, options.ChildInstallHookFullName, failures, stats);
			}
			else
			{
				VerifyRequiredGeneratedHookSemantics(hooks, failures, stats);
				VerifyPatchItems(index.PatchItems, index.ByFullName, failures, stats);
				VerifyRequestedInstallHooks(index, options, failures, stats);
			}

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

	private static HookVerificationIndex BuildHookIndex(
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

			if (!options.AllGeneratedHooks
			    && !StagingHookCompatManifest.TryGet(metadata.HookFullName, out _)
			    && !MatchesInstallRequest(metadata, options.InstallHookRequests))
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

		return new HookVerificationIndex(hookItems, patchItems, hookItemsByFullName);
	}

	private static IReadOnlyList<string> ExpandHookDependencyRequests(
		IReadOnlyList<HookMetadata> hooks,
		string requestedHookFullName)
	{
		HashSet<string> requested = new(StringComparer.Ordinal);
		Queue<string> pending = new();
		pending.Enqueue(requestedHookFullName);

		while (pending.Count > 0)
		{
			string request = pending.Dequeue();
			if (!requested.Add(request))
			{
				continue;
			}

			foreach (HookMetadata metadata in hooks.Where(metadata =>
				         string.Equals(metadata.HookFullName, request, StringComparison.Ordinal) ||
				         string.Equals(metadata.HookName, request, StringComparison.Ordinal)))
			{
				requested.Add(metadata.HookFullName);
				foreach (string dependency in metadata.Dependencies)
				{
					if (!string.IsNullOrWhiteSpace(dependency) && !requested.Contains(dependency))
					{
						pending.Enqueue(dependency);
					}
				}
			}
		}

		return requested.OrderBy(value => value, StringComparer.Ordinal).ToArray();
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
		Dictionary<string, List<HookVerificationItem>> hookItemsByFullName,
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
			if (!hookItemsByFullName.TryGetValue(dependency, out List<HookVerificationItem>? dependencyItems) || dependencyItems.Count == 0)
			{
				throw new InvalidOperationException($"Declared dependency '{dependency}' was not found.");
			}

			foreach (HookVerificationItem dependencyItem in dependencyItems)
			{
				AddPatchInstallOrder(dependencyItem, hookItemsByFullName, installOrder, visited, visiting);
			}
		}

		visiting.Remove(item);
		visited.Add(item);
		installOrder.Add(item);
	}

	private static void VerifyRequestedInstallHooks(
		HookVerificationIndex index,
		Options options,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		List<HookVerificationItem> requested = new();

		if (options.InstallCompatibilityHooks)
		{
			foreach (StagingHookCompatEntry entry in StagingHookCompatManifest.All)
			{
				if (index.ByFullName.TryGetValue(entry.HookFullName, out List<HookVerificationItem>? items))
				{
					requested.AddRange(items);
				}
			}
		}

		foreach (string request in options.InstallHookRequests)
		{
			if (TryResolveInstallRequest(index, request, out List<HookVerificationItem> items, out string error))
			{
				requested.AddRange(items);
			}
			else
			{
				failures.Add(new VerificationFailure(request, "install-request", error));
			}
		}

		if (options.InstallAllDynamicHooks)
		{
			requested.AddRange(index.Items.Where(item => !item.Metadata.IsPatch && !item.Metadata.IsStatic));
		}

		List<HookVerificationItem> distinct = requested
			.GroupBy(item => item.Metadata.HookFullName, StringComparer.Ordinal)
			.Select(group => group.First())
			.OrderBy(item => item.Metadata.HookFullName, StringComparer.Ordinal)
			.ToList();

		foreach (InstallCheckResult check in RunChildInstallHooks(distinct, options))
		{
			stats.InstallTestedHooks++;
			if (check.Result.Success)
			{
				stats.InstallPassedHooks++;
				continue;
			}

			stats.InstallFailedHooks++;
			failures.Add(new VerificationFailure(check.Item.Metadata.DisplayName, "install", check.Result.Message));
		}
	}

	private static bool MatchesInstallRequest(HookMetadata metadata, IReadOnlyList<string> requests)
	{
		foreach (string request in requests)
		{
			if (string.IsNullOrWhiteSpace(request))
			{
				continue;
			}

			string normalized = request.Trim();
			Match hashMatch = HookRequestWithHashRegex().Match(normalized);
			if (hashMatch.Success)
			{
				string hookName = hashMatch.Groups["name"].Value.Trim();
				if (string.Equals(metadata.HookName, hookName, StringComparison.Ordinal)
				    || string.Equals(metadata.HookFullName, hookName, StringComparison.Ordinal))
				{
					return true;
				}

				continue;
			}

			if (string.Equals(metadata.HookFullName, normalized, StringComparison.Ordinal)
			    || string.Equals(metadata.HookName, normalized, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static List<InstallCheckResult> RunChildInstallHooks(IReadOnlyList<HookVerificationItem> items, Options options)
	{
		InstallCheckResult[] results = new InstallCheckResult[items.Count];
		Parallel.For(
			0,
			items.Count,
			new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.MaxParallelInstallChecks) },
			index =>
			{
				HookVerificationItem item = items[index];
				try
				{
					results[index] = new InstallCheckResult(item, RunChildInstallHook(item.Metadata.HookFullName, options));
				}
				catch (Exception ex)
				{
					results[index] = new InstallCheckResult(item, new ChildInstallResult(false, ex.Message));
				}
			});

		return results.ToList();
	}

	private static bool TryResolveInstallRequest(
		HookVerificationIndex index,
		string request,
		out List<HookVerificationItem> items,
		out string error)
	{
		items = new List<HookVerificationItem>();
		error = string.Empty;

		if (string.IsNullOrWhiteSpace(request))
		{
			error = "Empty hook install request.";
			return false;
		}

		string normalized = request.Trim();
		Match hashMatch = HookRequestWithHashRegex().Match(normalized);
		if (hashMatch.Success)
		{
			string hookName = hashMatch.Groups["name"].Value.Trim();
			string hash = hashMatch.Groups["hash"].Value;
			items = index.Items
				.Where(item =>
					(string.Equals(item.Metadata.HookName, hookName, StringComparison.Ordinal)
					 || string.Equals(item.Metadata.HookFullName, hookName, StringComparison.Ordinal))
					&& item.Metadata.Identifier.EndsWith(hash, StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (items.Count == 0)
			{
				items = index.Items
					.Where(item =>
						string.Equals(item.Metadata.HookName, hookName, StringComparison.Ordinal)
						|| string.Equals(item.Metadata.HookFullName, hookName, StringComparison.Ordinal))
					.ToList();
			}
		}
		else if (index.ByFullName.TryGetValue(normalized, out List<HookVerificationItem>? byFullName))
		{
			items = byFullName.ToList();
		}
		else
		{
			items = index.Items
				.Where(item => string.Equals(item.Metadata.HookName, normalized, StringComparison.Ordinal))
				.ToList();
		}

		if (items.Count == 0)
		{
			error = $"No generated hook matched install request '{request}'.";
			return false;
		}

		return true;
	}

	private static ChildInstallResult RunChildInstallHook(string hookFullName, Options options)
	{
		string verifierAssembly = Assembly.GetExecutingAssembly().Location;
		ProcessStartInfo startInfo = new("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};

		startInfo.ArgumentList.Add(verifierAssembly);
		startInfo.ArgumentList.Add("--server-root");
		startInfo.ArgumentList.Add(options.ServerRoot);
		startInfo.ArgumentList.Add("--carbon-managed");
		startInfo.ArgumentList.Add(options.CarbonManaged);
		startInfo.ArgumentList.Add("--hooks-dir");
		startInfo.ArgumentList.Add(options.HooksDir);
		startInfo.ArgumentList.Add("--all-generated");
		if (options.StrictNoSuppression)
		{
			startInfo.ArgumentList.Add("--strict-no-suppression");
		}

		startInfo.ArgumentList.Add("--child-install-hook");
		startInfo.ArgumentList.Add(hookFullName);

		using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start hook install child process.");
		Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
		Task<string> stderrTask = process.StandardError.ReadToEndAsync();
		if (!process.WaitForExit(60000))
		{
			try
			{
				process.Kill(entireProcessTree: true);
			}
			catch
			{
				// The process may already have exited between timeout and kill.
			}

			return new ChildInstallResult(false, "Child install test timed out after 60s.");
		}

		string stdout = stdoutTask.GetAwaiter().GetResult();
		string stderr = stderrTask.GetAwaiter().GetResult();

		if (process.ExitCode == 0)
		{
			return new ChildInstallResult(true, stdout.Trim());
		}

		string output = string.Join(Environment.NewLine, new[] { stdout.Trim(), stderr.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
		if (string.IsNullOrWhiteSpace(output))
		{
			output = $"Child install test exited with code {process.ExitCode}.";
		}

		return new ChildInstallResult(false, output);
	}

	private static void VerifyChildInstallHook(
		HookVerificationIndex index,
		string hookFullName,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		if (!index.ByFullName.TryGetValue(hookFullName, out List<HookVerificationItem>? items) || items.Count == 0)
		{
			failures.Add(new VerificationFailure(hookFullName, "install", "Generated hook was not found."));
			return;
		}

		foreach (HookVerificationItem item in items)
		{
			stats.InstallTestedHooks++;
			try
			{
				InstallHookWithDependencies(item, index.ByFullName);
				stats.InstallPassedHooks++;
				Console.WriteLine($"install-test: ok {item.Metadata.HookFullName} ({item.Metadata.Type.FullName})");
			}
			catch (Exception ex)
			{
				stats.InstallFailedHooks++;
				failures.Add(new VerificationFailure(item.Metadata.DisplayName, "install", Unwrap(ex).Message));
			}
			finally
			{
				try
				{
					PatchHarmony.UnpatchAll(HarmonyId);
				}
				catch
				{
					// Continue reporting the actual install failure.
				}
			}
		}
	}

	private static void InstallHookWithDependencies(
		HookVerificationItem item,
		Dictionary<string, List<HookVerificationItem>> hookItemsByFullName)
	{
		List<HookVerificationItem> installOrder = new();
		HashSet<HookVerificationItem> visited = new();
		HashSet<HookVerificationItem> visiting = new();
		AddPatchInstallOrder(item, hookItemsByFullName, installOrder, visited, visiting);

		foreach (HookVerificationItem patch in installOrder)
		{
			PatchHarmony.Patch(
				patch.TargetMethod,
				patch.Prefix == null ? null : new HarmonyMethod(patch.Prefix, Priority.VeryHigh),
				patch.Postfix == null ? null : new HarmonyMethod(patch.Postfix, Priority.VeryHigh),
				patch.Transpiler == null ? null : new HarmonyMethod(patch.Transpiler, Priority.VeryHigh));
		}
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

	private static void VerifyRequiredGeneratedHookSemantics(
		IReadOnlyList<HookMetadata> hooks,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		VerifyOnPlayerDisconnectedSemantics(hooks, failures, stats);
		VerifyMarketplaceTerminalPurchaseSemantics(hooks, failures, stats);
		VerifyOnLoseConditionSemantics(failures, stats);
	}

	private static void VerifyOnPlayerDisconnectedSemantics(
		IReadOnlyList<HookMetadata> hooks,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		List<HookMetadata> candidates = hooks
			.Where(metadata =>
				string.Equals(metadata.HookFullName, OnPlayerDisconnectedHookName, StringComparison.Ordinal)
				&& string.Equals(metadata.Target, "ServerMgr", StringComparison.Ordinal)
				&& string.Equals(metadata.Method, "OnDisconnected", StringComparison.Ordinal))
			.ToList();

		if (candidates.Count != 1)
		{
			failures.Add(new VerificationFailure(
				OnPlayerDisconnectedHookName,
				"semantic",
				$"Expected exactly one generated ServerMgr.OnDisconnected hook, but found {candidates.Count}. " +
				"The non-null BasePlayer disconnect invariant was not verified."));
			return;
		}

		HookMetadata metadata = candidates[0];
		Type? targetType = FindType(metadata.Target);
		if (targetType == null)
		{
			failures.Add(new VerificationFailure(metadata.DisplayName, "semantic", $"Target type '{metadata.Target}' was not found."));
			return;
		}

		MethodBase? targetMethod = ResolveTargetMethod(targetType, metadata);
		if (targetMethod == null)
		{
			failures.Add(new VerificationFailure(
				metadata.DisplayName,
				"semantic",
				$"Target signature '{metadata.Target}.{metadata.Method}({string.Join(", ", metadata.MethodArgs)})' was not found."));
			return;
		}

		MethodInfo? transpiler = AccessTools.Method(metadata.Type.AsType(), "Transpiler");
		if (transpiler == null)
		{
			failures.Add(new VerificationFailure(metadata.DisplayName, "semantic", "Generated Transpiler method was not found."));
			return;
		}

		try
		{
			List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(targetMethod, out ILGenerator generator);
			ParameterInfo[] parameters = transpiler.GetParameters();
			if (parameters.Length != 3)
			{
				throw new InvalidOperationException($"Expected a three-parameter generated transpiler, but found {parameters.Length} parameters.");
			}

			object? result = transpiler.Invoke(null, new object?[] { original, generator, targetMethod });
			if (result is not IEnumerable<CodeInstruction> rewritten)
			{
				throw new InvalidOperationException("Generated transpiler did not return IL instructions.");
			}

			VerifyOnPlayerDisconnectedControlFlow(rewritten.ToList());
			stats.SemanticChecks++;
		}
		catch (Exception ex)
		{
			failures.Add(new VerificationFailure(metadata.DisplayName, "semantic", Unwrap(ex).Message));
		}
	}

	private static void VerifyMarketplaceTerminalPurchaseSemantics(
		IReadOnlyList<HookMetadata> hooks,
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		List<HookMetadata> candidates = hooks
			.Where(metadata =>
				string.Equals(metadata.HookFullName, OnMarketplaceTerminalPurchaseHookName, StringComparison.Ordinal)
				&& string.Equals(metadata.Target, "MarketTerminal", StringComparison.Ordinal)
				&& string.Equals(metadata.Method, "Server_Purchase", StringComparison.Ordinal))
			.ToList();

		if (candidates.Count != 1)
		{
			failures.Add(new VerificationFailure(
				OnMarketplaceTerminalPurchaseHookName,
				"semantic",
				$"Expected exactly one MarketTerminal.Server_Purchase hook, but found {candidates.Count}."));
			return;
		}

		HookMetadata metadata = candidates[0];
		Type? targetType = FindType(metadata.Target);
		MethodBase? targetMethod = targetType == null ? null : ResolveTargetMethod(targetType, metadata);
		MethodInfo? transpiler = AccessTools.Method(metadata.Type.AsType(), "Transpiler");
		if (targetMethod == null || transpiler == null)
		{
			failures.Add(new VerificationFailure(
				metadata.DisplayName,
				"semantic",
				"Marketplace target or transpiler could not be resolved."));
			return;
		}

		try
		{
			List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(targetMethod, out ILGenerator generator);
			object? result = transpiler.Invoke(null, new object?[] { original, generator, targetMethod });
			if (result is not IEnumerable<CodeInstruction> rewritten)
			{
				throw new InvalidOperationException("Marketplace transpiler did not return IL instructions.");
			}

			VerifyMarketplaceTerminalPurchaseControlFlow(rewritten.ToList(), targetMethod);
			stats.SemanticChecks++;
		}
		catch (Exception ex)
		{
			failures.Add(new VerificationFailure(metadata.DisplayName, "semantic", Unwrap(ex).Message));
		}
	}

	private static void VerifyMarketplaceTerminalPurchaseControlFlow(
		IReadOnlyList<CodeInstruction> instructions,
		MethodBase targetMethod)
	{
		List<int> hookCalls = FindHookDispatches(instructions, OnMarketplaceTerminalPurchaseHookId);
		if (hookCalls.Count != 1)
		{
			throw new InvalidOperationException(
				$"Expected one marketplace hook dispatch for ID {unchecked((uint)OnMarketplaceTerminalPurchaseHookId)}, found {hookCalls.Count}.");
		}

		int hookCallIndex = hookCalls[0];
		if (hookCallIndex < 9 || instructions[hookCallIndex].operand is not MethodInfo hookMethod)
		{
			throw new InvalidOperationException("Marketplace hook dispatch shape is incomplete.");
		}

		ParameterInfo[] hookParameters = hookMethod.GetParameters();
		if (hookMethod.ReturnType != typeof(object)
		    || hookParameters.Length != 6
		    || hookParameters[0].ParameterType != typeof(uint)
		    || hookParameters.Skip(1).Any(parameter => parameter.ParameterType != typeof(object)))
		{
			throw new InvalidOperationException(
				"Marketplace hook must call HookCaller.CallStaticHook(uint, object, object, object, object, object).");
		}

		MethodBody body = targetMethod.GetMethodBody()
			?? throw new InvalidOperationException("Marketplace target has no method body.");
		int vendingLocal = GetLoadedLocalIndex(instructions[hookCallIndex - 7]);
		int sellOrderLocal = GetLoadedLocalIndex(instructions[hookCallIndex - 4]);
		int amountLocal = GetLoadedLocalIndex(instructions[hookCallIndex - 2]);
		if (vendingLocal < 0 || sellOrderLocal < 0 || amountLocal < 0
		    || body.LocalVariables[vendingLocal].LocalType.FullName != "VendingMachine"
		    || body.LocalVariables[sellOrderLocal].LocalType != typeof(int)
		    || body.LocalVariables[amountLocal].LocalType != typeof(int)
		    || instructions[hookCallIndex - 8].opcode != OpCodes.Ldarg_0
		    || instructions[hookCallIndex - 6].opcode != OpCodes.Ldarg_1
		    || instructions[hookCallIndex - 5].operand is not FieldInfo playerField
		    || playerField.Name != "player"
		    || instructions[hookCallIndex - 3].opcode != OpCodes.Box
		    || instructions[hookCallIndex - 1].opcode != OpCodes.Box)
		{
			throw new InvalidOperationException(
				"Marketplace hook arguments are not terminal, VendingMachine, RPC player, sell-order index, and amount.");
		}

		int eligibilityIndex = FindNextCall(instructions, hookCallIndex, "MarketTerminal", "GetDeliveryEligibleVendingMachines");
		int powerIndex = FindNextCall(instructions, hookCallIndex, "Marketplace", "Server_CanAcceptOrder");
		int takeIndex = FindNextCall(instructions, hookCallIndex, "PlayerInventory", "Take");
		int transactionIndex = FindNextCall(instructions, hookCallIndex, "VendingMachine", "DoTransaction");
		int activeStoreIndex = FindNextFieldStore(instructions, hookCallIndex, "MarketTerminal", "_transactionActive");
		if (eligibilityIndex < 0 || powerIndex < 0 || takeIndex < 0 || transactionIndex < 0 || activeStoreIndex < 0)
		{
			throw new InvalidOperationException("Marketplace side-effect and guard anchors were not all found after the hook.");
		}

		int vendingStoreIndex = FindPreviousLocalStore(instructions, hookCallIndex, vendingLocal);
		int sellOrderStoreIndex = FindPreviousLocalStore(instructions, hookCallIndex, sellOrderLocal);
		int amountStoreIndex = FindPreviousLocalStore(instructions, hookCallIndex, amountLocal);
		int validationBranchIndex = FindPreviousConditionalBranch(instructions, hookCallIndex, vendingStoreIndex);
		if (vendingStoreIndex < 0 || sellOrderStoreIndex < 0 || amountStoreIndex < 0 || validationBranchIndex < 0)
		{
			throw new InvalidOperationException(
				"Marketplace hook does not follow the captured RPC locals and native validation branch.");
		}

		int branchIndex = NextMeaningfulInstruction(instructions, hookCallIndex);
		int returnIndex = NextMeaningfulInstruction(instructions, branchIndex);
		if (branchIndex < 0 || !IsBranchFalse(instructions[branchIndex])
		    || returnIndex < 0 || instructions[returnIndex].opcode != OpCodes.Ret
		    || instructions[branchIndex].operand is not Label continueLabel)
		{
			throw new InvalidOperationException("Marketplace non-null cancellation is not a direct empty-stack return.");
		}

		int continueIndex = FindLabelTarget(instructions, continueLabel);
		if (continueIndex <= returnIndex || continueIndex > eligibilityIndex)
		{
			throw new InvalidOperationException(
				"Marketplace null-result continuation does not resume at the delivery-eligibility prelude.");
		}
	}

	private static void VerifyOnLoseConditionSemantics(
		List<VerificationFailure> failures,
		VerificationStats stats)
	{
		Type? corePlugin = FindType("Carbon.Core.CorePlugin");
		Type? itemType = FindType("Item");
		MethodInfo? target = corePlugin == null || itemType == null
			? null
			: AccessTools.Method(corePlugin, "IOnLoseCondition", new[] { itemType, typeof(float) });
		if (target == null)
		{
			failures.Add(new VerificationFailure(
				OnLoseConditionHookName,
				"semantic",
				"Carbon.Core.CorePlugin.IOnLoseCondition(Item, float) was not found."));
			return;
		}

		try
		{
			List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(target, out _);
			List<int> hookCalls = FindHookDispatches(instructions, OnLoseConditionHookId);
			if (hookCalls.Count != 1)
			{
				throw new InvalidOperationException(
					$"Expected one OnLoseCondition dispatch for ID {OnLoseConditionHookId}, found {hookCalls.Count}.");
			}

			int hookCallIndex = hookCalls[0];
			int copyBackIndex = FindNextArgsFloatRead(instructions, hookCallIndex);
			int conditionStoreIndex = FindNextCall(instructions, hookCallIndex, "Item", "set_condition");
			if (copyBackIndex < 0 || conditionStoreIndex < 0 || copyBackIndex >= conditionStoreIndex)
			{
				throw new InvalidOperationException(
					"OnLoseCondition does not copy args[1] back into the amount argument before condition subtraction.");
			}

			stats.SemanticChecks++;
		}
		catch (Exception ex)
		{
			failures.Add(new VerificationFailure(OnLoseConditionHookName, "semantic", Unwrap(ex).Message));
		}
	}

	private static void VerifyOnPlayerDisconnectedControlFlow(IReadOnlyList<CodeInstruction> instructions)
	{
		List<(int CallIndex, int IdIndex)> dispatches = new();
		for (int i = 0; i < instructions.Count; i++)
		{
			if (instructions[i].opcode != OpCodes.Call
			    || !CallsMethod(instructions[i], "Carbon.HookCaller", "CallStaticHook")
			    || i < 3
			    || instructions[i - 3].opcode != OpCodes.Ldc_I4
			    || instructions[i - 3].operand is not int hookId
			    || hookId != OnPlayerDisconnectedHookId)
			{
				continue;
			}

			dispatches.Add((i, i - 3));
		}

		if (dispatches.Count != 1)
		{
			throw new InvalidOperationException(
				$"Expected exactly one CallStaticHook dispatch for hook ID {OnPlayerDisconnectedHookId}, but found {dispatches.Count}.");
		}

		(int hookCallIndex, int hookIdIndex) = dispatches[0];
		if (instructions[hookCallIndex].operand is not MethodInfo hookDispatchMethod)
		{
			throw new InvalidOperationException("OnPlayerDisconnected hook dispatch operand is not a method.");
		}

		ParameterInfo[] hookDispatchParameters = hookDispatchMethod.GetParameters();
		if (hookDispatchMethod.ReturnType != typeof(object)
		    || hookDispatchParameters.Length != 3
		    || hookDispatchParameters[0].ParameterType != typeof(uint)
		    || hookDispatchParameters[1].ParameterType != typeof(object)
		    || hookDispatchParameters[2].ParameterType != typeof(object))
		{
			throw new InvalidOperationException(
				"OnPlayerDisconnected does not call HookCaller.CallStaticHook(uint, object, object).");
		}

		List<int> playerDisconnectCalls = Enumerable.Range(0, instructions.Count)
			.Where(index => CallsMethod(instructions[index], "BasePlayer", "OnDisconnected"))
			.ToList();
		if (playerDisconnectCalls.Count != 1)
		{
			throw new InvalidOperationException(
				$"Expected exactly one BasePlayer.OnDisconnected call in rewritten IL, but found {playerDisconnectCalls.Count}.");
		}

		int playerDisconnectIndex = playerDisconnectCalls[0];
		if (hookCallIndex >= playerDisconnectIndex)
		{
			throw new InvalidOperationException(
				$"Hook dispatch at IL index {hookCallIndex} must execute before BasePlayer.OnDisconnected at index {playerDisconnectIndex}.");
		}

		int comparisonIndex = FindPreviousCall(instructions, hookIdIndex, "UnityEngine.Object", "op_Inequality");
		if (comparisonIndex < 0)
		{
			int laterComparisonIndex = FindNextCall(instructions, hookCallIndex, "UnityEngine.Object", "op_Inequality");
			if (laterComparisonIndex >= 0)
			{
				throw new InvalidOperationException(
					$"Hook dispatch at IL index {hookCallIndex} executes before the BasePlayer null comparison at index {laterComparisonIndex}; " +
					"the null-player path can invoke OnPlayerDisconnected.");
			}

			throw new InvalidOperationException(
				$"No UnityEngine.Object.op_Inequality BasePlayer null comparison was found before hook dispatch at IL index {hookCallIndex}.");
		}

		int nullIndex = PreviousMeaningfulInstruction(instructions, comparisonIndex);
		int localIndex = PreviousMeaningfulInstruction(instructions, nullIndex);
		if (nullIndex < 0 || instructions[nullIndex].opcode != OpCodes.Ldnull
		    || localIndex < 0 || instructions[localIndex].opcode != OpCodes.Ldloc_0)
		{
			throw new InvalidOperationException(
				$"The null comparison at IL index {comparisonIndex} is not fed by BasePlayer local 0 followed by ldnull.");
		}

		int branchIndex = NextMeaningfulInstruction(instructions, comparisonIndex);
		if (branchIndex < 0 || !IsBranchFalse(instructions[branchIndex]))
		{
			string actual = branchIndex < 0 ? "<end of method>" : $"{instructions[branchIndex].opcode} at index {branchIndex}";
			throw new InvalidOperationException(
				$"Expected brfalse immediately after the BasePlayer null comparison at IL index {comparisonIndex}, but found {actual}.");
		}

		int fallThroughIndex = NextMeaningfulInstruction(instructions, branchIndex);
		if (fallThroughIndex != hookIdIndex)
		{
			throw new InvalidOperationException(
				$"Non-null BasePlayer branch begins at IL index {fallThroughIndex}, but hook ID {OnPlayerDisconnectedHookId} is loaded at index {hookIdIndex}. " +
				"The hook must be the first operation inside the guarded branch.");
		}

		int hookPlayerLoadIndex = NextMeaningfulInstruction(instructions, hookIdIndex);
		if (hookPlayerLoadIndex != hookCallIndex - 2 || instructions[hookPlayerLoadIndex].opcode != OpCodes.Ldloc_0)
		{
			throw new InvalidOperationException(
				"Hook dispatch does not load BasePlayer local 0 checked by the native null guard.");
		}

		int reasonLoadIndex = PreviousMeaningfulInstruction(instructions, hookCallIndex);
		if (reasonLoadIndex != hookCallIndex - 1 || instructions[reasonLoadIndex].opcode != OpCodes.Ldarg_1)
		{
			throw new InvalidOperationException(
				"Hook dispatch does not pass ServerMgr.OnDisconnected argument 1 as the disconnect reason.");
		}

		int playerLoadIndex = PreviousMeaningfulInstruction(instructions, playerDisconnectIndex);
		if (playerLoadIndex < 0 || instructions[playerLoadIndex].opcode != OpCodes.Ldloc_0)
		{
			throw new InvalidOperationException(
				"BasePlayer.OnDisconnected does not consume BasePlayer local 0 checked by the native null guard.");
		}

		if (instructions[branchIndex].operand is not Label nullTarget)
		{
			throw new InvalidOperationException($"Null branch at IL index {branchIndex} has no label target.");
		}

		int nullTargetIndex = FindLabelTarget(instructions, nullTarget);
		if (nullTargetIndex < 0)
		{
			throw new InvalidOperationException($"Null branch target from IL index {branchIndex} was not found in rewritten instructions.");
		}

		if (nullTargetIndex <= hookCallIndex || nullTargetIndex <= playerDisconnectIndex)
		{
			throw new InvalidOperationException(
				$"Null BasePlayer branch from IL index {branchIndex} lands at index {nullTargetIndex}; it must bypass both " +
				$"CallStaticHook at index {hookCallIndex} and BasePlayer.OnDisconnected at index {playerDisconnectIndex}.");
		}

		for (int i = fallThroughIndex; i < playerDisconnectIndex; i++)
		{
			FlowControl flow = instructions[i].opcode.FlowControl;
			if (flow is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return or FlowControl.Throw)
			{
				throw new InvalidOperationException(
					$"Non-null BasePlayer path is not straight-line from hook dispatch to BasePlayer.OnDisconnected; " +
					$"unexpected {instructions[i].opcode} at IL index {i}.");
			}
		}
	}

	private static int FindPreviousCall(IReadOnlyList<CodeInstruction> instructions, int beforeIndex, string declaringTypeFullName, string methodName)
	{
		for (int i = beforeIndex - 1; i >= 0; i--)
		{
			if (CallsMethod(instructions[i], declaringTypeFullName, methodName))
			{
				return i;
			}
		}

		return -1;
	}

	private static int FindNextCall(IReadOnlyList<CodeInstruction> instructions, int afterIndex, string declaringTypeFullName, string methodName)
	{
		for (int i = afterIndex + 1; i < instructions.Count; i++)
		{
			if (CallsMethod(instructions[i], declaringTypeFullName, methodName))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool CallsMethod(CodeInstruction instruction, string declaringTypeFullName, string methodName)
	{
		return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
		       && instruction.operand is MethodBase method
		       && string.Equals(method.DeclaringType?.FullName, declaringTypeFullName, StringComparison.Ordinal)
		       && string.Equals(method.Name, methodName, StringComparison.Ordinal);
	}

	private static List<int> FindHookDispatches(IReadOnlyList<CodeInstruction> instructions, int hookId)
	{
		List<int> result = new();
		for (int i = 0; i < instructions.Count; i++)
		{
			if (instructions[i].opcode != OpCodes.Ldc_I4
			    || instructions[i].operand is not int candidate
			    || candidate != hookId)
			{
				continue;
			}

			for (int j = i + 1; j < instructions.Count && j <= i + 16; j++)
			{
				if (CallsMethod(instructions[j], "Carbon.HookCaller", "CallStaticHook"))
				{
					result.Add(j);
					break;
				}

				if (instructions[j].opcode.FlowControl is FlowControl.Return or FlowControl.Throw)
				{
					break;
				}
			}
		}

		return result;
	}

	private static int GetLoadedLocalIndex(CodeInstruction instruction)
	{
		if (instruction.opcode == OpCodes.Ldloc_0) return 0;
		if (instruction.opcode == OpCodes.Ldloc_1) return 1;
		if (instruction.opcode == OpCodes.Ldloc_2) return 2;
		if (instruction.opcode == OpCodes.Ldloc_3) return 3;
		if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S) return -1;

		return instruction.operand switch
		{
			LocalBuilder local => local.LocalIndex,
			LocalVariableInfo local => local.LocalIndex,
			int index => index,
			byte index => index,
			_ => -1
		};
	}

	private static int FindNextFieldStore(
		IReadOnlyList<CodeInstruction> instructions,
		int afterIndex,
		string declaringTypeFullName,
		string fieldName)
	{
		for (int i = afterIndex + 1; i < instructions.Count; i++)
		{
			if (instructions[i].opcode == OpCodes.Stfld
			    && instructions[i].operand is FieldInfo field
			    && string.Equals(field.DeclaringType?.FullName, declaringTypeFullName, StringComparison.Ordinal)
			    && string.Equals(field.Name, fieldName, StringComparison.Ordinal))
			{
				return i;
			}
		}

		return -1;
	}

	private static int FindPreviousLocalStore(
		IReadOnlyList<CodeInstruction> instructions,
		int beforeIndex,
		int localIndex)
	{
		for (int i = beforeIndex - 1; i >= 0; i--)
		{
			if (GetStoredLocalIndex(instructions[i]) == localIndex)
			{
				return i;
			}
		}

		return -1;
	}

	private static int GetStoredLocalIndex(CodeInstruction instruction)
	{
		if (instruction.opcode == OpCodes.Stloc_0) return 0;
		if (instruction.opcode == OpCodes.Stloc_1) return 1;
		if (instruction.opcode == OpCodes.Stloc_2) return 2;
		if (instruction.opcode == OpCodes.Stloc_3) return 3;
		if (instruction.opcode != OpCodes.Stloc && instruction.opcode != OpCodes.Stloc_S) return -1;

		return instruction.operand switch
		{
			LocalBuilder local => local.LocalIndex,
			LocalVariableInfo local => local.LocalIndex,
			int index => index,
			byte index => index,
			_ => -1
		};
	}

	private static int FindPreviousConditionalBranch(
		IReadOnlyList<CodeInstruction> instructions,
		int beforeIndex,
		int afterIndex)
	{
		for (int i = beforeIndex - 1; i > afterIndex; i--)
		{
			if (instructions[i].opcode.FlowControl == FlowControl.Cond_Branch)
			{
				return i;
			}
		}

		return -1;
	}

	private static int FindNextArgsFloatRead(
		IReadOnlyList<CodeInstruction> instructions,
		int afterIndex)
	{
		for (int i = afterIndex + 1; i < instructions.Count; i++)
		{
			if (instructions[i].opcode != OpCodes.Ldelem_Ref)
			{
				continue;
			}

			int indexLoad = PreviousMeaningfulInstruction(instructions, i);
			int unboxIndex = NextMeaningfulInstruction(instructions, i);
			if (indexLoad >= 0 && IsLoadIntOne(instructions[indexLoad])
			    && unboxIndex >= 0
			    && instructions[unboxIndex].opcode == OpCodes.Unbox_Any
			    && Equals(instructions[unboxIndex].operand, typeof(float)))
			{
				return unboxIndex;
			}
		}

		return -1;
	}

	private static bool IsLoadIntOne(CodeInstruction instruction)
	{
		return instruction.opcode == OpCodes.Ldc_I4_1
		       || instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == 1
		       || instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte shortValue && shortValue == 1;
	}

	private static int PreviousMeaningfulInstruction(IReadOnlyList<CodeInstruction> instructions, int beforeIndex)
	{
		for (int i = beforeIndex - 1; i >= 0; i--)
		{
			if (instructions[i].opcode != OpCodes.Nop)
			{
				return i;
			}
		}

		return -1;
	}

	private static int NextMeaningfulInstruction(IReadOnlyList<CodeInstruction> instructions, int afterIndex)
	{
		for (int i = afterIndex + 1; i < instructions.Count; i++)
		{
			if (instructions[i].opcode != OpCodes.Nop)
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsBranchFalse(CodeInstruction instruction)
	{
		return instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S;
	}

	private static int FindLabelTarget(IReadOnlyList<CodeInstruction> instructions, Label target)
	{
		for (int i = 0; i < instructions.Count; i++)
		{
			if (instructions[i].labels.Contains(target))
			{
				return i;
			}
		}

		return -1;
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
		if (targetMethod == null
		    && string.Equals(metadata.HookFullName, OnLoseConditionHookName, StringComparison.Ordinal))
		{
			Type? itemType = FindType("Item");
			targetMethod = itemType == null
				? null
				: AccessTools.Method(targetType, metadata.Method, new[] { itemType, typeof(float) });
		}
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
		Console.WriteLine($"semantic-validation: requiredGeneratedHooks={stats.SemanticChecks}");
		Console.WriteLine($"shim-validation: transpilers={stats.ShimTranspilerChecks} prefixPostfix={stats.ShimPatchChecks}");
		Console.WriteLine($"install-tests: tested={stats.InstallTestedHooks} passed={stats.InstallPassedHooks} failed={stats.InstallFailedHooks}");

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
		bool InstallCompatibilityHooks,
		bool InstallAllDynamicHooks,
		IReadOnlySet<string> AllowedSuppressions,
		string? DumpHookFullName,
		IReadOnlyList<string> InstallHookRequests,
		int MaxParallelInstallChecks,
		string? ChildInstallHookFullName)
	{
		public static Options Parse(string[] args)
		{
			string serverRoot = "/home/johnm/rust-staging-autoupdate/server";
			string? carbonManaged = null;
			string? hooksDir = null;
			bool allGeneratedHooks = true;
			bool strictNoSuppression = true;
			bool verifyCompatibilityShims = false;
			bool installCompatibilityHooks = true;
			bool installAllDynamicHooks = false;
			string? allowSuppressionFile = null;
			string? dumpHookFullName = null;
			string? childInstallHookFullName = null;
			int maxParallelInstallChecks = 8;
			List<string> installHookRequests = new();

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

					case "--requested-only":
						allGeneratedHooks = false;
						installCompatibilityHooks = false;
						verifyCompatibilityShims = false;
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

					case "--install-compat-hooks":
						installCompatibilityHooks = true;
						break;

					case "--no-install-compat-hooks":
						installCompatibilityHooks = false;
						break;

					case "--install-all-dynamic":
						installAllDynamicHooks = true;
						break;

					case "--install-hook":
						installHookRequests.Add(RequireValue(args, ref i, "--install-hook"));
						break;

					case "--install-hooks-from-log":
						installHookRequests.AddRange(ReadHookRequestsFromLog(RequireValue(args, ref i, "--install-hooks-from-log")));
						break;

					case "--max-parallel-install-checks":
						maxParallelInstallChecks = int.Parse(RequireValue(args, ref i, "--max-parallel-install-checks"));
						break;

					case "--dump-hook":
						dumpHookFullName = RequireValue(args, ref i, "--dump-hook");
						break;

					case "--child-install-hook":
						childInstallHookFullName = RequireValue(args, ref i, "--child-install-hook");
						allGeneratedHooks = false;
						installCompatibilityHooks = false;
						installHookRequests.Add(childInstallHookFullName);
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
				installCompatibilityHooks,
				installAllDynamicHooks,
				LoadAllowedSuppressions(allowSuppressionFile),
				dumpHookFullName,
				installHookRequests.Distinct(StringComparer.Ordinal).ToArray(),
				Math.Max(1, maxParallelInstallChecks),
				childInstallHookFullName);
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
			Console.WriteLine("Usage: Carbon.StagingHookVerifier --server-root <path> [--carbon-managed <path>] [--hooks-dir <path>] [--all-generated] [--requested-only] [--strict-no-suppression] [--allow-suppression-file <path>] [--install-hook <hook>] [--install-hooks-from-log <path>] [--install-all-dynamic] [--max-parallel-install-checks <n>] [--dump-hook <hook-full-name>]");
		}

		private static IReadOnlyList<string> ReadHookRequestsFromLog(string path)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"Missing hook request log: {path}", path);
			}

			List<string> hooks = new();
			foreach (string line in File.ReadLines(path))
			{
				foreach (Match match in QuotedHookRequestRegex().Matches(line))
				{
					hooks.Add(match.Groups["hook"].Value);
				}
			}

			return hooks.Distinct(StringComparer.Ordinal).ToArray();
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

	private sealed record HookVerificationIndex(
		List<HookVerificationItem> Items,
		List<HookVerificationItem> PatchItems,
		Dictionary<string, List<HookVerificationItem>> ByFullName);

	private readonly record struct ChildInstallResult(bool Success, string Message);

	private readonly record struct InstallCheckResult(HookVerificationItem Item, ChildInstallResult Result);

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
		public int SemanticChecks;
		public int ShimTranspilerChecks;
		public int ShimPatchChecks;
		public int InstallTestedHooks;
		public int InstallPassedHooks;
		public int InstallFailedHooks;
	}

	[GeneratedRegex(@"(?<name>.+)\[(?<hash>[0-9a-fA-F]{6})\]$", RegexOptions.CultureInvariant)]
	private static partial Regex HookRequestWithHashRegex();

	[GeneratedRegex(@"(?:hook|for) '?(?<hook>[A-Za-z_][^']*?\[[0-9a-fA-F]{6}\])'?", RegexOptions.CultureInvariant)]
	private static partial Regex QuotedHookRequestRegex();

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

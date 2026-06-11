using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using API.Hooks;
using Carbon.Extensions;
using Carbon.Pooling;
using Facepunch;
using HarmonyLib;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

namespace Carbon.Hooks;

public class HookEx : IDisposable, IHook
{
	private HookRuntime _runtime;
	private readonly TypeInfo _patchMethod;


	public string HookName { get; }
	public string HookFullName { get; }
	public HookFlags Options { get; set; }
	public Type TargetType { get; }
	public MethodType MethodType { get; }
	public string TargetMethod { get; }
	public List<MethodBase> TargetMethods { get; }
	public string[] TargetMethodArgNames { get; }
	public Type[] TargetMethodArgs { get; }
	public string Identifier { get; }
	public string ShortIdentifier => Identifier[^6..];
	public string[] Dependencies { get; }
	public bool IsPatch => Options.HasFlag(HookFlags.Patch);
	public bool IsStaticHook => Options.HasFlag(HookFlags.Static);
	public bool IsDynamicHook => !Options.HasFlag(HookFlags.Static) && !Options.HasFlag(HookFlags.Patch);
	public bool IsHidden => Options.HasFlag(HookFlags.Hidden);
	public bool IsChecksumIgnored => Options.HasFlag(HookFlags.IgnoreChecksum);
	public bool IsLoaded => _runtime.Status is HookState.Success or HookState.Warning or HookState.Failure or HookState.Inactive;
	public bool IsInstalled => _runtime.Status is HookState.Success or HookState.Warning;
	public bool IsFailed => _runtime.Status is HookState.Failure;
	public HookState Status { get => _runtime.Status; set => _runtime.Status = value; }

	public string PatchMethodName => _patchMethod.Name;
	public string LastError => _runtime.LastError;

	public override string ToString() => $"{HookName}[{ShortIdentifier}]";

	public HookEx(TypeInfo type)
	{
		try
		{
			Harmony.DEBUG = false;

			if (type == null || !Attribute.IsDefined(type, typeof(HookAttribute.Patch), false))
				throw new Exception($"Type is null or metadata not defined");

			HookAttribute.Patch metadata = type.GetCustomAttribute<HookAttribute.Patch>() ?? null;

			if (metadata == default)
				throw new Exception($"Metadata information is invalid or was not found");

			Dependencies = [];
			HookFullName = metadata.FullName;
			HookName = metadata.Name;
			TargetMethod = metadata.Method;
			TargetMethodArgNames = metadata.MethodArgs ?? [];
			TargetMethodArgs = [.. TargetMethodArgNames.Select(AccessToolsEx.TypeByName)];
			TargetMethods = [];
			TargetType = string.IsNullOrEmpty(metadata.Target) ? null : AccessToolsEx.TypeByName(metadata.Target);
			MethodType = metadata.MethodType;

			Identifier = type.GetCustomAttribute<HookAttribute.Identifier>()?.Value ?? $"{Guid.NewGuid():N}";
			Options = type.GetCustomAttribute<HookAttribute.Options>()?.Value ?? HookFlags.None;

			if (Attribute.IsDefined(type, typeof(HookAttribute.Dependencies), false))
				Dependencies = type.GetCustomAttribute<HookAttribute.Dependencies>()?.Value ?? default;

			if (Options.HasFlag(HookFlags.MetadataOnly))
			{
				SetStatus(HookState.Inactive);
				return;
			}

			_patchMethod = type;
			_runtime.Status = HookState.Inactive;
			_runtime.HarmonyHandler = new Harmony(Identifier);

			// cache the additional metadata about the hook
			_runtime.Prefix = AccessTools.Method(type, "Prefix") ?? null;
			_runtime.Postfix = AccessTools.Method(type, "Postfix") ?? null;
			_runtime.Transpiler = AccessTools.Method(type, "Transpiler") ?? null;

			if (TargetType == null)
			{
				throw new Exception($"Target type '{metadata.Target}' not found");
			}

			// Type generics need to handled differently from a standard type.
			// Harmony/Mono.Cecil will not allow the patching of the generic type
			// which means we need to find each type matching the constrain and
			// then patch each one of them idividually.
			if (TargetType.IsGenericType)
			{
				Type generic = TargetType;
				IEnumerable<Type> constrains = AccessToolsEx.GetConstraints(generic);

				foreach (Type item in AccessToolsEx.MatchConstrains(constrains))
				{
					Type constructed = generic.MakeGenericType(new Type[] { item });
					MethodInfo method = AccessTools.Method(constructed, TargetMethod) ?? null;
					if (method != null) TargetMethods.Add(method);
				}

				if (TargetMethods.Count == 0)
					throw new Exception($"Signature for '{TargetType}.{TargetMethod}' not found");
			}
			else
			{
				MethodBase method = GetTargetMethodInfo()
					?? throw new Exception($"Signature for '{TargetType}.{TargetMethod}' not found");
				TargetMethods.Add(method);
			}
		}
		catch (System.Exception e)
		{
			Logger.Error($"Error while parsing '{type.Name}'", e);
			SetStatus(HookState.Failure, e.Message);
		}
		finally
		{
			Harmony.DEBUG = true;
		}
	}

	public bool ApplyPatch()
	{
		if (IsInstalled) return true;

		HarmonyMethod
			prefix = null, postfix = null, transpiler = null;

		try
		{
			if (_runtime.Prefix != null)
				prefix = new HarmonyMethod(_runtime.Prefix, Priority.VeryHigh);

			if (_runtime.Postfix != null)
				postfix = new HarmonyMethod(_runtime.Postfix, Priority.VeryHigh);

			if (_runtime.Transpiler != null)
				transpiler = new HarmonyMethod(_runtime.Transpiler, Priority.VeryHigh);

			if (prefix is null && postfix is null && transpiler is null)
				throw new Exception($"(prefix, postfix, transpiler not found");

			if (TargetMethod is null || TargetMethod.Length == 0)
				throw new Exception($"target method not found");
		}
		catch (System.Exception e)
		{
			Logger.Error($"Error while patching hook '{this}'", e);
			_runtime.Status = HookState.Failure;
			_runtime.LastError = e.Message;
			return false;
		}

		try
		{
			foreach (MethodBase method in TargetMethods)
			{
				MethodInfo current = (_runtime.HarmonyHandler.Patch(method,
					prefix: prefix, postfix: postfix, transpiler: transpiler
				) ?? null) ?? throw new Exception($"HarmonyLib failed to patch '{method}'");

				_runtime.Status = HookState.Success;
			}
		}
#if DEBUG
		catch (HarmonyException e)
		{
			var sb = Pool.Get<StringBuilder>();
			Logger.Error($"Error while patching hook '{this}' index:{e.GetErrorIndex()} offset:{e.GetErrorOffset()}", e);
			sb.AppendLine($"{e.InnerException?.Message.Trim() ?? string.Empty}");

			int x = 0;
			foreach (var instruction in e.GetInstructionsWithOffsets())
				sb.AppendLine($"\t{x++:000} {instruction.Key:X4}: {instruction.Value}");

			Logger.Error(sb.ToString());
			Pool.FreeUnmanaged(ref sb);
			return false;
		}
#endif
		catch (System.Exception e)
		{
			Logger.Error($"Error while patching hook '{this}'", e.InnerException ?? e);
			_runtime.Status = HookState.Failure;
			_runtime.LastError = e.Message;
			return false;
		}

		return true;
	}
	public bool RemovePatch()
	{
		try
		{
			if (!IsInstalled)
			{
				return true;
			}
			_runtime.HarmonyHandler.UnpatchAll(Identifier);
			_runtime.Status = HookState.Inactive;
			return true;
		}
		catch (System.Exception e)
		{
#if DEBUG
			Logger.Error($"Error while unpatching hook '{HookFullName}[{Identifier}]'", e);
#endif
			_runtime.LastError = e.Message;
			return false;
		}
	}
	public MethodInfo GetTargetMethodInfo()
	{
		return MethodType switch
		{
			MethodType.Getter => AccessTools.PropertyGetter(TargetType, TargetMethod),
			MethodType.Setter => AccessTools.PropertySetter(TargetType, TargetMethod),
			_ => GetStandardTargetMethodInfo() ?? GetCompilerGeneratedLocalFunctionTarget() ?? GetUniqueTargetMethodByName()
		};
	}

	private MethodInfo GetStandardTargetMethodInfo()
	{
		if (TargetMethodArgs.Any(argument => argument == null))
		{
			return null;
		}

		return AccessTools.Method(TargetType, TargetMethod, TargetMethodArgs);
	}

	private MethodInfo GetCompilerGeneratedLocalFunctionTarget()
	{
		if (TargetType == null || string.IsNullOrEmpty(TargetMethod) || TargetMethodArgs == null)
		{
			return null;
		}

		int suffixIndex = TargetMethod.LastIndexOf('|');
		if (suffixIndex < 0 || !TargetMethod.StartsWith("<") || !TargetMethod.Contains(">g__"))
		{
			return null;
		}

		string stablePrefix = TargetMethod[..(suffixIndex + 1)];
		MethodInfo result = null;

		foreach (MethodInfo method in AccessTools.GetDeclaredMethods(TargetType))
		{
			if (!method.Name.StartsWith(stablePrefix))
			{
				continue;
			}

			if (!MatchesTargetParameters(method.GetParameters()))
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

	private MethodInfo GetUniqueTargetMethodByName()
	{
		if (TargetType == null || string.IsNullOrEmpty(TargetMethod) || TargetMethodArgNames.Length != 0)
		{
			return null;
		}

		MethodInfo result = null;
		foreach (MethodInfo method in AccessTools.GetDeclaredMethods(TargetType))
		{
			if (!string.Equals(method.Name, TargetMethod, StringComparison.Ordinal))
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

	private bool MatchesTargetParameters(ParameterInfo[] parameters)
	{
		if (parameters.Length != TargetMethodArgs.Length)
		{
			return false;
		}

		for (int i = 0; i < parameters.Length; i++)
		{
			if (!MatchesTargetParameter(parameters[i].ParameterType, TargetMethodArgs[i], TargetMethodArgNames.ElementAtOrDefault(i)))
			{
				return false;
			}
		}

		return true;
	}

	private static bool MatchesTargetParameter(Type actual, Type expected, string expectedName)
	{
		actual = UnwrapByRef(actual);
		expected = UnwrapByRef(expected);

		if (expected != null)
		{
			return actual == expected;
		}

		if (string.IsNullOrEmpty(expectedName))
		{
			return false;
		}

		return string.Equals(actual.FullName, expectedName, StringComparison.Ordinal) ||
			   string.Equals(actual.Name, expectedName, StringComparison.Ordinal);
	}

	private static Type UnwrapByRef(Type type)
	{
		return type?.IsByRef == true ? type.GetElementType() : type;
	}

	public void SetStatus(HookState Status, string error = null)
	{
		_runtime.Status = Status;
		_runtime.LastError = error;
	}

	public bool HasDependencies() => Dependencies is { Length: > 0 };

	private bool hasDisposed;

	protected virtual void Dispose(bool disposing)
	{
		if (hasDisposed)
		{
			return;
		}

		if (disposing)
		{
			RemovePatch();
		}

		_runtime.HarmonyHandler = null;
		hasDisposed = true;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

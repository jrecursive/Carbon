using System;
using System.Collections.Generic;

namespace Carbon.Hooks;

#if RUST_STAGING || STAGING_HOOK_VERIFIER
internal enum StagingHookCompatAction
{
	Shim,
	GeneratorPolicyFix,
	DisabledObsolete
}

internal readonly struct StagingHookCompatEntry
{
	public StagingHookCompatEntry(string hookFullName, StagingHookCompatAction action, bool suppressGeneratedHook, string reason)
	{
		HookFullName = hookFullName;
		Action = action;
		SuppressGeneratedHook = suppressGeneratedHook;
		Reason = reason;
	}

	public string HookFullName { get; }
	public StagingHookCompatAction Action { get; }
	public bool SuppressGeneratedHook { get; }
	public string Reason { get; }
}

internal static class StagingHookCompatManifest
{
	private static readonly StagingHookCompatEntry[] Entries =
	{
		new("CanCastFishingRod", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against BaseFishingRod.Server_RequestCast."),
		new("CanCatchFish", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against BaseFishingRod.CatchProcessBudgeted."),
		new("OnFishCatch", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against BaseFishingRod.CatchProcessBudgeted."),
		new("OnItemCraft", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against ItemCrafter.CraftItem."),
		new("OnCorpsePopulate", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against NPCPlayer.CreateCorpse."),
		new("OnPlayerVoice", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against BasePlayer.OnReceivedVoice(ReadOnlySpan<byte>)."),
		new("OnDispenserGather", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against ResourceDispenser.GiveResourceFromItem."),
		new("OnDispenserGathered", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against ResourceDispenser.GiveResourceFromItem."),
		new("OnDispenserBonus", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against ResourceDispenser.AssignFinishBonus."),
		new("OnDispenserBonusReceived", StagingHookCompatAction.Shim, true, "Restored by StagingRustIlCompat against ResourceDispenser.AssignFinishBonus."),
		new("OnPlayerAttack [Projectile]", StagingHookCompatAction.DisabledObsolete, true, "Current downloaded hook uses invalid cancellation IL; keep suppressed until the local generator policy fix is wired into the release hook build."),
		new("FixItemKeyId [patch]", StagingHookCompatAction.DisabledObsolete, true, "Current staging ItemCrafter.CraftItem carries attachmentID natively."),
		new("NoLimboGroupForPlayers [patch]", StagingHookCompatAction.DisabledObsolete, true, "Generated patch is not compatible with the installed staging IL and has no first-party consumer.")
	};

	private static readonly Dictionary<string, StagingHookCompatEntry> EntryByHookFullName = BuildIndex();

	public static IReadOnlyList<StagingHookCompatEntry> All => Entries;

	public static bool TryGet(string hookFullName, out StagingHookCompatEntry entry)
	{
		if (!string.IsNullOrEmpty(hookFullName) && EntryByHookFullName.TryGetValue(hookFullName, out entry))
		{
			return true;
		}

		entry = default;
		return false;
	}

	public static bool ShouldSuppressGeneratedHook(string hookFullName, out StagingHookCompatEntry entry)
	{
		return TryGet(hookFullName, out entry) && entry.SuppressGeneratedHook;
	}

	private static Dictionary<string, StagingHookCompatEntry> BuildIndex()
	{
		Dictionary<string, StagingHookCompatEntry> index = new(StringComparer.Ordinal);

		foreach (StagingHookCompatEntry entry in Entries)
		{
			index.Add(entry.HookFullName, entry);
		}

		return index;
	}
}
#endif

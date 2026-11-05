namespace Carbon.Compatibility;

/// <summary>Preserves legacy inventory truthiness after Rust moved inventories out of Unity components.</summary>
public static class LegacyPlayerInventory
{
	public static bool Exists(PlayerInventory inventory)
	{
		// The proxy is a managed object; Unity lifetime checks belong to its owner.
		if (inventory == null)
			return false;
		BaseEntity owner = inventory.GetBaseEntity();
		return !object.ReferenceEquals(owner, null) && owner != null;
	}
}

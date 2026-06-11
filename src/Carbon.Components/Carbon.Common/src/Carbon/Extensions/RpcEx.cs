using Network;

namespace Carbon.Extensions;

public static class RpcEx
{
	public static void SendClientRpc(this BaseEntity entity, BasePlayer player, string funcName)
	{
		if (!TryStart(entity, player, funcName, out var writer)) return;

		using (writer)
		{
			writer.Send(new SendInfo(player.Connection));
		}
	}

	public static void SendClientRpc(this BaseEntity entity, BasePlayer player, string funcName, string arg1)
	{
		if (!TryStart(entity, player, funcName, out var writer)) return;

		using (writer)
		{
			writer.String(arg1);
			writer.Send(new SendInfo(player.Connection));
		}
	}

	public static void SendClientRpc(this BaseEntity entity, BasePlayer player, string funcName, float arg1)
	{
		if (!TryStart(entity, player, funcName, out var writer)) return;

		using (writer)
		{
			writer.Float(arg1);
			writer.Send(new SendInfo(player.Connection));
		}
	}

	public static void SendClientRpc(this BaseEntity entity, BasePlayer player, string funcName, Vector3 arg1)
	{
		if (!TryStart(entity, player, funcName, out var writer)) return;

		using (writer)
		{
			writer.Float(arg1.x);
			writer.Float(arg1.y);
			writer.Float(arg1.z);
			writer.Send(new SendInfo(player.Connection));
		}
	}

	public static void WriteBytesWithSizeCompat(this NetWrite writer, byte[] data, bool variableLength = false)
	{
		if (writer == null) return;

		if (data == null || data.Length == 0)
		{
			writer.WriteUInt32(0, variableLength);
			return;
		}

		writer.WriteUInt32((uint)data.Length, variableLength);
		writer.Write(data, 0, data.Length);
	}

	public static void SendClientPng(this BaseEntity entity, BasePlayer player, uint crc, byte[] data, uint entityId = 0)
	{
		if (data == null || !TryStart(entity, player, "CL_ReceiveFilePng", out var writer)) return;

		using (writer)
		{
			writer.UInt32(crc);
			writer.UInt32((uint)data.Length);
			writer.WriteBytesWithSizeCompat(data);
			writer.UInt32(entityId);
			writer.UInt8((byte)FileStorage.Type.png);
			writer.Send(new SendInfo(player.Connection));
		}
	}

	private static bool TryStart(BaseEntity entity, BasePlayer player, string funcName, out NetWrite writer)
	{
		writer = null;

		if (entity?.net == null || player?.Connection == null || string.IsNullOrEmpty(funcName))
		{
			return false;
		}

		writer = Net.sv.StartWrite();
		if (writer == null)
		{
			return false;
		}

		writer.PacketID(Message.Type.RPCMessage);
		writer.EntityID(entity.net.ID);
		writer.UInt32(StringPool.Get(funcName));
		writer.UInt64(0UL);
		return true;
	}
}

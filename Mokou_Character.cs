using System.IO;
using CharacterRely;
using CharacterRely.Content.ModCharacter;
using Mokou_Character.Character;
using Mokou_Character.Skill;
using Terraria.ModLoader;

namespace Mokou_Character
{
	/// <summary>妹红角色模组入口，负责初始化角色库并转交联机数据包。</summary>
	public class Mokou_Character : Mod
	{
		/// <summary>加载角色选择、动画绘制、物品适配和技能系统。</summary>
		public override void Load()
		{
			CharacterRelyLibrary.Initialize(this);
			// 注册后自动出现在新建玩家时的角色选择界面。
			CharacterRegistry.Register<Mokou>(this, "mokou", "藤原妹红", "不老不死的蓬莱人");
		}

		/// <summary>读取外层消息编号，将角色库的数据包交回库处理。</summary>
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			byte messageType = reader.ReadByte();
			if (messageType == CharacterRelyLibrary.PacketId)
			{
				CharacterRelyLibrary.HandlePacket(reader, whoAmI);
				return;
			}

			if (messageType is ResurrectionTeleport.RequestPacket or ResurrectionTeleport.ResultPacket)
				ResurrectionTeleport.Handle(this, messageType, reader, whoAmI);
		}
	}
}

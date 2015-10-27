/*
 * Credit to MarioE for the original plugin.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace IceWand
{
	[ApiVersion(1, 22)]
	public class IceWand : TerrariaPlugin
	{
		List<IceWandAction> Actions = new List<IceWandAction>();
		int[] ActionData = new int[256];
		byte[] ActionTypes = new byte[256];

		public override string Author
		{
			get { return "Zaicon"; }
		}
		public override string Description
		{
			get { return "Lets the ice rod do virtually anything."; }
		}
		public override string Name
		{
			get { return "IceWand"; }
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public IceWand(Main game)
			: base(game)
		{
			Order = 10;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		void OnGetData(GetDataEventArgs e)
		{
			if (!e.Handled && e.MsgID == PacketTypes.Tile)
			{
				using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
				{
					byte action = reader.ReadByte();
					int x = reader.ReadInt16();
					int y = reader.ReadInt16();
					ushort type = reader.ReadUInt16();

					if (type == 127 && ActionTypes[e.Msg.whoAmI] != 0)
					{
						if (WorldGen.genRand == null)
							WorldGen.genRand = new Random();

						Actions[ActionTypes[e.Msg.whoAmI]].callback.Invoke(null,
							new IceWandEventArgs(x, y, TShock.Players[e.Msg.whoAmI], ActionData[e.Msg.whoAmI]));
						TSPlayer.All.SendTileSquare(x, y, 1);
						e.Handled = true;
					}
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			Commands.ChatCommands.Add(new Command("iw.use", IceWandCmd, "icewand", "iw"));

			Actions.Add(null);
			Actions.Add(new IceWandAction(Bomb, "bomb"));
			Actions.Add(new IceWandAction(Explode, "explode"));
			Actions.Add(new IceWandAction(Honey, "honey"));
			Actions.Add(new IceWandAction(Item, "item"));
			Actions.Add(new IceWandAction(Lava, "lava"));
			Actions.Add(new IceWandAction(Position, "position"));
			Actions.Add(new IceWandAction(SpawnMob, "spawnmob"));
			Actions.Add(new IceWandAction(Tile, "tile"));
			Actions.Add(new IceWandAction(Wall, "wall"));
			Actions.Add(new IceWandAction(Water, "water"));
		}
		void OnLeave(LeaveEventArgs e)
		{
			ActionData[e.Who] = 0;
			ActionTypes[e.Who] = 0;
		}

		void IceWandCmd(CommandArgs e)
		{
			if (e.Parameters.Count != 1 && e.Parameters.Count != 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /icewand <action> [data]");
				return;
			}

			string actionName = e.Parameters[0].ToLower();
			if (actionName == "help" || actionName == "list")
			{
				var actions = new StringBuilder();
				for (int i = 1; i < Actions.Count; i++)
				{
					if (e.Player.Group.HasPermission("iw." + Actions[i].name))
						actions.Append(Actions[i].name).Append(", ");
				}
				e.Player.SendInfoMessage("Available actions: " + actions.ToString().Substring(0, actions.Length - 2));
			}
			else if (e.Parameters[0].ToLower() == "off")
			{
				ActionTypes[e.Player.Index] = 0;
				e.Player.SendSuccessMessage("Turned off ice wand.");
			}
			else
			{
				for (int i = 1; i < Actions.Count; i++)
				{
					if (Actions[i].name == actionName)
					{
						if (e.Player.Group.HasPermission("iw." + Actions[i].name))
						{
							if (e.Parameters.Count != 1)
							{
								if (!int.TryParse(e.Parameters[1], out ActionData[e.Player.Index]))
								{
									e.Player.SendErrorMessage("Invalid data.");
									return;
								}
							}
							ActionTypes[e.Player.Index] = (byte)i;
							e.Player.SendSuccessMessage("Ice wand action is now {0}.", Actions[i].name);
							return;
						}
						else
						{
							e.Player.SendErrorMessage("You do not have access to this action.");
							return;
						}
					}
				}
				e.Player.SendErrorMessage("Invalid ice wand action.");
			}
		}

		void Bomb(object sender, IceWandEventArgs e)
		{
			int ID = Projectile.NewProjectile(e.X * 16 + 8, e.Y * 16 + 8, 0, 0, 28, 250, 10);
			Main.projectile[ID].timeLeft = 1;
			TSPlayer.All.SendData(PacketTypes.ProjectileNew, "", ID);
		}
		void Explode(object sender, IceWandEventArgs e)
		{
			int ID = Projectile.NewProjectile(e.X * 16 + 8, e.Y * 16 + 8, 0, 0, 108, 250, 10);
			TSPlayer.All.SendData(PacketTypes.ProjectileNew, "", ID);
		}
		void Honey(object sender, IceWandEventArgs e)
		{
			Main.tile[e.X, e.Y].liquidType(2);
			Main.tile[e.X, e.Y].liquid = 255;
			WorldGen.SquareTileFrame(e.X, e.Y);
			TSPlayer.All.SendTileSquare(e.X, e.Y, 1);
		}
		void Item(object sender, IceWandEventArgs e)
		{
			int ID = Terraria.Item.NewItem(e.X * 16, e.Y * 16, 0, 0, e.Data, 1);
		}
		void Lava(object sender, IceWandEventArgs e)
		{
			Main.tile[e.X, e.Y].liquidType(1);
			Main.tile[e.X, e.Y].liquid = 255;
			WorldGen.SquareTileFrame(e.X, e.Y);
			TSPlayer.All.SendTileSquare(e.X, e.Y, 1);
		}
		void Position(object sender, IceWandEventArgs e)
		{
			e.Player.SendInfoMessage("Position: {0}, {1}", e.X, e.Y);
		}
		void SpawnMob(object sender, IceWandEventArgs e)
		{
			int ID = NPC.NewNPC(e.X * 16, e.Y * 16, e.Data);
			TSPlayer.All.SendData(PacketTypes.NpcUpdate, "", ID);
		}
		void Tile(object sender, IceWandEventArgs e)
		{
			if (e.Data >= 0 && e.Data < Main.maxTileSets)
			{
				WorldGen.PlaceTile(e.X, e.Y, e.Data, true, true);
				TSPlayer.All.SendTileSquare(e.X, e.Y, 4);
			}
		}
		void Wall(object sender, IceWandEventArgs e)
		{
			if (e.Data > 0 && e.Data < Main.maxWallTypes)
			{
				WorldGen.PlaceWall(e.X, e.Y, e.Data, true);
				TSPlayer.All.SendTileSquare(e.X, e.Y, 1);
			}
		}
		void Water(object sender, IceWandEventArgs e)
		{
			Main.tile[e.X, e.Y].liquidType(0);
			Main.tile[e.X, e.Y].liquid = 255;
			WorldGen.SquareTileFrame(e.X, e.Y);
			TSPlayer.All.SendTileSquare(e.X, e.Y, 1);
		}
	}
}
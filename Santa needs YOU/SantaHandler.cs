using Archipelago.Gifting.Net.Gifts;
using Archipelago.Gifting.Net.Gifts.Versions.Current;
using Archipelago.Gifting.Net.Service;
using Archipelago.Gifting.Net.Service.TraitAcceptance;
using Archipelago.Gifting.Net.Traits;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json;

namespace Santa_needs_YOU;

public class SantaHandler
{
	private readonly GiftingService GiftingService;
	private readonly ArchipelagoSession Session;
	private readonly long NumChildren;
	private int Satisfiedchildren;
	private Dictionary<int, GiftTrait> Whishlist = null!;

	public SantaHandler(ArchipelagoSession session)
	{
		Session = session;
		NumChildren = (long)session.DataStorage.GetSlotData()["locations"];
		session.Items.ItemReceived += OnItemReceived;
		session.MessageLog.OnMessageReceived += CheckMessage;

		GiftingService = new GiftingService(session);
		GiftingService.OpenGiftBox();
		GiftingService.SubscribeToNewGifts(CheckGifts);

		Load();

		CheckAllGifts();
	}

	private void Load()
	{
		string saveName = "AP_Santa_" + Session.RoomState.Seed;
		if (File.Exists(saveName))
		{
			Whishlist = JsonConvert.DeserializeObject<Dictionary<int, GiftTrait>>(File.ReadAllText(saveName))!;
		}

		if (Whishlist == null!)
		{
			Whishlist = new Dictionary<int, GiftTrait>
			{
				[-1] = new("Nothing", 0, 0)
			};
			Satisfiedchildren = 0;
		}
		else
		{
			Satisfiedchildren = (int)Whishlist[-1].Quality;
		}
	}

	private void Save()
	{
		string saveName = "AP_Santa_" + Session.RoomState.Seed;
		Whishlist[-1].Quality = Satisfiedchildren;
		File.WriteAllText(saveName, JsonConvert.SerializeObject(Whishlist));
	}

	private void OnItemReceived(ReceivedItemsHelper helper)
	{
		foreach ((int _, AcceptedTraits? acceptedTraits) in GiftingService.GetAcceptedTraitsByPlayer(
			         Session.ConnectionInfo.Team,
			         new[]
			         {
				         "Armor", "Flower", "Speed", "Monster", "Drink", "Fiber", "Egg", "Food", "Fruit", "Heal",
				         "Vegetable", "Consumable", "Resource", "Mana", "Fish", "Meat", "Cure"
			         }))
		{
			if (acceptedTraits.Player != Session.ConnectionInfo.Slot)
			{
				int traitsLength = acceptedTraits.Traits.Length;
				if (traitsLength > 0)
				{
					string mainTrait = acceptedTraits.Traits[Random.Shared.Next(traitsLength)];
					double duration = Random.Shared.NextDouble() * 1.5 + 0.5;
					double quality = Random.Shared.NextDouble() * 1.5 + 0.5;
					GiftTrait[] traits = { new(mainTrait, duration, quality) };
					foreach (string? trait in acceptedTraits.Traits)
					{
						if (trait != mainTrait && Random.Shared.NextDouble() < 0.3)
						{
							duration = Random.Shared.NextDouble() * 1 + 0.25;
							quality = Random.Shared.NextDouble() * 1 + 0.25;
							traits[traits.Length] = new GiftTrait(trait, duration, quality);
						}
					}

					GiftItem gift = new("Love", Random.Shared.Next(10),
						Random.Shared.NextInt64(1000000000L, 100000000000));
					GiftingService.SendGift(gift, traits, Session.Players.GetPlayerName(acceptedTraits.Player));
				}
			}
		}
	}

	private void CheckMessage(LogMessage message)
	{
		if (message is ChatLogMessage chatLogMessage)
		{
			string text = chatLogMessage.Message;
			if (text.StartsWith('¤'))
			{
				string[] commands = text.Split(' ');
				switch (commands[0])
				{
					case "¤info":
						SayPacket response = new();
						switch (commands.Length)
						{
							case 1:
								long childrenLeft = NumChildren - Satisfiedchildren;
								if (childrenLeft == 0)
									response.Text = "All the gifts have been delivered";
								else
									response.Text = $"There are still {childrenLeft} gifts to deliver";
								break;
							case 2:
								int id = Convert.ToInt32(commands[1]);
								if (id < 0 || id >= NumChildren)
									response.Text =
										$"Child {id} isn't on my list, it only goes from 0 to {NumChildren - 1}";
								else
								{
									if (!Whishlist.ContainsKey(id))
									{
										GenerateWish(id);
									}

									response.Text = $"Child {id} would like {Whishlist[id].Trait}";
								}
								break;
							default:
								response.Text = "Command not recognized";
								break;
						}

						Session.Socket.SendPacket(response);
						break;
				}
			}
		}
	}

	private void GenerateWish(int id)
	{
		HashSet<string> acceptedTraits = new();
		int team = Session.ConnectionInfo.Team;
		foreach (PlayerInfo player in Session.Players.AllPlayers)
		{
			if (player.Team == team && GiftData.SenderByGames.ContainsKey(player.Game))
			{
				acceptedTraits.UnionWith(GiftData.SenderByGames[player.Game]);
			}
		}

		string trait;
		if (acceptedTraits.Count == 0)
		{
			trait = "Nothing";
			Satisfiedchildren += 1;
			Session.Locations.CompleteLocationChecks(4573924180576428 + id);
		}
		else
			trait = acceptedTraits.ElementAt(Random.Shared.Next(acceptedTraits.Count));

		Whishlist[id] = new GiftTrait(trait, 1, 1);
		Save();
	}

	private void CheckAllGifts()
	{
		CheckGifts(GiftingService.GetAllGiftsAndEmptyGiftbox());
	}

	private void CheckGifts(Dictionary<string, Gift> gifts)
	{
		if (gifts.Count > 0)
		{
			bool anyGood = false;
			foreach ((string? _, Gift? gift) in gifts)
			{
				int foundKey = -1;
				foreach ((int key, GiftTrait? wish) in Whishlist)
				{
					if (wish.Trait == "Nothing") continue;
					if (gift.Traits.Any(giftTrait => giftTrait.Trait == wish.Trait))
					{
						foundKey = key;
						break;
					}
				}

				if (foundKey == -1)
				{
					GiftingService.RefundGift(gift);
				}
				else
				{
					anyGood = true;
					Satisfiedchildren += 1;
					Session.Locations.CompleteLocationChecks(4573924180576428 + foundKey);
					Whishlist[foundKey].Trait = "Nothing";
				}
			}
			
			if (anyGood)
				Save();

			GiftingService.RemoveGiftsFromGiftBox(gifts.Keys);
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Error;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Trading;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.DB;
using NLog;

namespace AAEmu.Game.Core.Managers.World
{
    public class SpecialtyManager : Singleton<SpecialtyManager>
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        public static int MAX_SPECIALTY_RATIO = 130;

        private Dictionary<uint, Specialty> _specialties;
        private Dictionary<uint, SpecialtyBundleItem> _specialtyBundleItems;
        private Dictionary<uint, SpecialtyNpc> _specialtyNpc;
        
        //                 itemId           bundleId
        private Dictionary<uint, Dictionary<uint, SpecialtyBundleItem>> _specialtyBundleItemsMapped;
        //                 itemId           zoneId
        private Dictionary<uint, Dictionary<uint, int>> _priceRatios;
        
        public void Load()
        {
            _specialties = new Dictionary<uint, Specialty>();
            _specialtyBundleItems = new Dictionary<uint, SpecialtyBundleItem>();
            _specialtyNpc = new Dictionary<uint, SpecialtyNpc>();
            
            _specialtyBundleItemsMapped = new Dictionary<uint, Dictionary<uint, SpecialtyBundleItem>>();
            _priceRatios = new Dictionary<uint, Dictionary<uint, int>>();
            
            _log.Info("SpecialtyManager is loading...");

            ItemManager.Instance.OnItemsLoaded += OnItemsLoaded;

            using (var connection = SQLite.CreateConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM specialties";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var template = new Specialty();
                            template.Id = reader.GetUInt32("id");
                            template.RowZoneGroupId = reader.GetUInt32("row_zone_group_id");
                            template.ColZoneGroupId = reader.GetUInt32("col_zone_group_id");
                            template.Ratio = reader.GetUInt32("ratio");
                            template.Profit = reader.GetUInt32("profit");
                            template.VendorExist = reader.GetBoolean("id", true);
                            _specialties.Add(template.Id, template);
                        }
                    }
                }
                
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM specialty_bundle_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var template = new SpecialtyBundleItem();
                            template.Id = reader.GetUInt32("id");
                            template.ItemId = reader.GetUInt32("item_id");
                            template.SpecialtyBundleId = reader.GetUInt32("specialty_bundle_id");
                            template.Profit = reader.GetUInt32("profit");
                            template.Ratio = reader.GetUInt32("ratio");
                            _specialtyBundleItems.Add(template.Id, template);
                            
                            if (!_specialtyBundleItemsMapped.ContainsKey(template.ItemId))
                                _specialtyBundleItemsMapped.Add(template.ItemId, new Dictionary<uint, SpecialtyBundleItem>());

                            _specialtyBundleItemsMapped[template.ItemId].Add(template.SpecialtyBundleId, template);
                        }
                    }
                }
                
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM specialty_npcs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var template = new SpecialtyNpc();
                            template.Id = reader.GetUInt32("id");
                            template.Name = reader.GetString("name");
                            template.NpcId = reader.GetUInt32("npc_id");
                            template.SpecialtyBundleId = reader.GetUInt32("specialty_bundle_id");
                            
                            _specialtyNpc.Add(template.NpcId, template);
                        }
                    }
                }
            }
            
            _log.Info("SpecialtyManager loaded");
        }

        public void OnItemsLoaded(object sender, EventArgs e)
        {
            foreach (var specialtyBundleItem in _specialtyBundleItems.Values)
            {
                specialtyBundleItem.Item = ItemManager.Instance.GetTemplate(specialtyBundleItem.ItemId);
            }
        }

        public int GetRatioForSpecialty(Character player)
        {
            var backpack = player.Inventory.Equipment.GetItemBySlot((int)EquipmentItemSlotType.Reserved);
            if (backpack == null) 
                return 0;

            var zoneId = player.Position.ZoneId;
            
            if (!_priceRatios.ContainsKey(backpack.TemplateId)) 
                _priceRatios.Add(backpack.TemplateId, new Dictionary<uint, int>());
            
            if (!_priceRatios[backpack.TemplateId].ContainsKey(zoneId))
                _priceRatios[backpack.TemplateId].Add(zoneId, MAX_SPECIALTY_RATIO);
           
            return _priceRatios[backpack.TemplateId][zoneId];
        }

        public int GetBasePriceForSpecialty(Character player, uint npcId)
        {
            // Sanity checks
            var backpack = player.Inventory.Equipment.GetItemBySlot((int)EquipmentItemSlotType.Reserved);
            if (backpack == null)
            {
                player.SendErrorMessage(ErrorMessageType.StoreBackpackNogoods);
                return 0;
            }
            
            var npc = WorldManager.Instance.GetNpc(npcId);
            if (npc == null)
            {
                player.SendErrorMessage(ErrorMessageType.InvalidTarget);
                return 0;
            }

            if (MathUtil.CalculateDistance(player.Position, npc.Position) > 2.5)
            {
                player.SendErrorMessage(ErrorMessageType.TooFarAway);
                return 0;   
            }

            var bundleIdAtNPC = _specialtyNpc[npc.TemplateId].SpecialtyBundleId;

            if (!_specialtyBundleItemsMapped.ContainsKey(backpack.TemplateId))
            {
                player.SendErrorMessage(ErrorMessageType.Invalid);
                return 0;
            }
            
            if (!_specialtyBundleItemsMapped[backpack.TemplateId].ContainsKey(bundleIdAtNPC))
            {
                player.SendErrorMessage(ErrorMessageType.Invalid);
                return 0;
            }
            
            var bundleItem = _specialtyBundleItemsMapped[backpack.TemplateId][bundleIdAtNPC];
            if (bundleItem == null)
            {
                player.SendErrorMessage(ErrorMessageType.Invalid);
                return 0;
            }
            
            return (int) (Math.Floor(bundleItem.Profit * (bundleItem.Ratio / 1000f)) + bundleItem.Item.Refund);
        }

        public void SellSpecialty(Character player, uint npcObjId)
        {
            var basePrice = GetBasePriceForSpecialty(player, npcObjId);

            if (basePrice == 0) // We had an error, no need to keep going
                return;

            var priceRatio = GetRatioForSpecialty(player);

            var backpack = player.Inventory.Equipment.GetItemBySlot((int)EquipmentItemSlotType.Reserved);
            if (backpack == null)
            {
                player.SendErrorMessage(ErrorMessageType.StoreBackpackNogoods);
                return;
            }

            var npc = WorldManager.Instance.GetNpc(npcObjId);
            if (npc == null)
                return;

            // Our backpack isn't null, we have the NPC 
            var finalPrice = (basePrice * (priceRatio / 100f));

            // TODO : Send the mail properly
            // As a placeholder, we instantly add the pack's price to the player this time
            if (npc.Template.SpecialtyCoinId != 0)
            {
                var amountOfItems = (int) Math.Round(finalPrice / 10000);
                if (!player.Inventory.Bag.AcquireDefaultItem(ItemTaskType.SellBackpack, npc.Template.SpecialtyCoinId, amountOfItems))
                {
                    player.SendErrorMessage(ErrorMessageType.BagFull);
                    return;
                }
            }
            else
            {
                player.AddMoney(SlotType.Inventory, (int)finalPrice);
            }
            
            // Delete the backpack
            player.Inventory.Equipment.ConsumeItem(ItemTaskType.SellBackpack, backpack.TemplateId, 1, backpack);
            player.ChangeLabor(-60, (int) ActabilityType.Commerce);

            // Lastly, we reduce the ratio by 1%
            _priceRatios[backpack.TemplateId][player.Position.ZoneId] = priceRatio > 70 ? priceRatio - 1 : 70;
        }
    }
}

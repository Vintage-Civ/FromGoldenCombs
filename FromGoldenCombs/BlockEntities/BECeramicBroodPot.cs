﻿using FromGoldenCombs.config;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FromGoldenCombs.BlockEntities
{
    class BECeramicBroodPot : BlockEntityDisplay
    {
        double harvestableAtTotalHours;
        double cooldownUntilTotalHours;
        int quantityNearbyFlowers;
        int quantityNearbyHives;
        float actvitiyLevel;
        RoomRegistry roomreg;
        float roomness;
        public static SimpleParticleProperties Bees;
        int scanQuantityNearbyFlowers;
        int scanQuantityNearbyHives;
        int scanIteration;
        public bool isActiveHive { get; set; }
        EnumHivePopSize hivePopSize;
        int harvestBase = FromGoldenCombsConfig.Current.clayPotHiveHoursToHarvest;

        public readonly InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "ceramicbroodpot";

        public BECeramicBroodPot()
        {
            inv = new InventoryGeneric(1, "hivepot-slot", null, null);
            meshes = new MeshData[1];

        }

        static BECeramicBroodPot()
        {
            Bees = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 215, 156, 65),
                new Vec3d(), new Vec3d(),
                new Vec3f(0, 0, 0),
                new Vec3f(0, 0, 0),
                1f,
                0f,
                0.5f, 0.5f,
                EnumParticleModel.Cube
            );
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(TestHarvestable, 6000);
            RegisterGameTickListener(OnScanForFlowers, api.World.Rand.Next(5000) + 30000);

            roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                Shape shape = capi.Assets.TryGet(new AssetLocation("fromgoldencombs", "shapes/block/hive/ceramic/ceramicbroodpot-notop.json")).ToObject<Shape>();

                if (api.Side == EnumAppSide.Client)
                {
                    RegisterGameTickListener(SpawnBeeParticles, 300);
                }
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            Block hive = Api.World.BlockAccessor.GetBlock(Pos);
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot.Empty)
            {
                if (TryTake(byPlayer))
                {
                    MarkDirty(true);
                    return true;
                }
            } else if (slot.Itemstack.Collectible.WildCardMatch(new AssetLocation("game", "skep-populated-*")) && !isActiveHive)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOutWhole();

                isActiveHive = true;
                return true;
            }
            else if (TryPut(slot)) {
                {
                    Api.World.BlockAccessor.ExchangeBlock(Api.World.BlockAccessor.GetBlock(hive.CodeWithVariant("top", "withtop")).BlockId, Pos);
                    MarkDirty(true);
                }
                return true; //This prevents TryPlaceBlock from passing if TryPut fails.
            }
            return false;

        }

        private bool TryTake(IPlayer player)
        {
            int index = 0;
            if (!inv[index].Empty)
            {
                Api.World.BlockAccessor.ExchangeBlock(Api.World.BlockAccessor.GetBlock(this.Block.CodeWithVariant("top", "notop")).BlockId, Pos);
                player.InventoryManager.TryGiveItemstack(inv[0].TakeOutWhole());
                return true;
            }
            else
            {
                ItemStack stack = this.Block.OnPickBlock(Api.World, Pos);
                stack.Attributes.SetBool("populated", isActiveHive);
                if (player.InventoryManager.TryGiveItemstack(stack))
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                    return true;
                }
            }
            return false;
        }

        public void TryPutDirect(ItemStack stack)
        {
            int index = 0;
            if (inv[index].Empty
               && stack.Block.FirstCodePart() == "hivetop" && stack.Block.Variant["type"] != "raw")
            {
                inv[index].Itemstack = stack;
            }

        }

        private bool TryPut(ItemSlot slot)
        {

            int index = 0;
            if (inv[index].Empty
               && slot.Itemstack.Block.FirstCodePart() == "hivetop" && slot.Itemstack.Block.Variant["type"] != "raw")
            {
                slot.TryPutInto(Api.World, inv[index]);
                if(inv[index].Itemstack.Block.Variant["type"] != "raw" && isActiveHive)
                {
                    cooldownUntilTotalHours = 0;
                }
                return true;
            }
            return false;
        }

        //Rendering Processes
        readonly Matrixf mat = new();

        protected override void updateMeshes()
        {
            mat.Identity();
            mat.RotateYDeg(this.Block.Shape.rotateY);

            base.updateMeshes();
        }

        protected override MeshData genMesh(ItemStack stack, int index)
        {
            MeshData mesh;

            ICoreClientAPI capi = Api as ICoreClientAPI;
            mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
            nowTesselatingItem = stack.Item;
            nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Block.Shape.Base);
            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);

            float x = 0;
            float y = -.985f;
            float z = 0;
            Vec4f offset = mat.TransformVector(new Vec4f(x, y, z, 0));
            //This seems to work for rotating the actual appearance of the blocks in the itemslots.
            mesh.Rotate(new Vec3f(0.5f, 1f, 0.5f), 3.14f, this.Block != null ? this.Block.Shape.rotateY * GameMath.DEG2RAD : 0f, 0f);
            mesh.Translate(offset.XYZ);

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mat.Identity();
            return base.OnTesselation(mesher, tessThreadTesselator);

        }

        private void TestHarvestable(float dt)
        {
            double worldTime = Api.World.Calendar.TotalHours;
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            if (conds == null) return;

            float temp = conds.Temperature + (roomness > 0 ? 5 : 0);
            actvitiyLevel = GameMath.Clamp(temp / 5f, 0f, 1f);

            bool hasEmptyHivetop = !inv[0].Empty && inv[0].Itemstack.Block.Variant["type"] == "empty";
            // Reset timers during winter
            if (temp <= -10)
            {
                //TODO: Readdress harvestAtTotalHours math to ensure it works for all ranges of growth time.
                harvestableAtTotalHours = worldTime + HarvestableTime(harvestBase);
                cooldownUntilTotalHours = worldTime + 4 / 2 * 24;
            }

            //If not cooling down
            if (cooldownUntilTotalHours <= 0 && hasEmptyHivetop)
            {
                //If harvestableAtHours is not currently set, but the hivesize is greater than Poor
                if (harvestableAtTotalHours == 0 && hivePopSize > EnumHivePopSize.Poor)
                {

                    harvestableAtTotalHours = worldTime + HarvestableTime(harvestBase);
                }

                //If harvestableAthours is reached, update inv[0] to harvestable honey pot,
                //then reset harvestableAtHours, and begin cooldown stage
                else if (worldTime > harvestableAtTotalHours && hivePopSize > EnumHivePopSize.Poor)
                {
                    inv[0].Itemstack = new ItemStack(Api.World.GetBlock(inv[0].Itemstack.Collectible.CodeWithVariant("type", "harvestable")), 1);
                    harvestableAtTotalHours = 0;
                    cooldownUntilTotalHours = worldTime + 4 / 2 * 24;
                    updateMeshes();
                    MarkDirty(true);
                }
                //If not cooling down, but also does not have a valid honey pot, restart cooldown stage
                else if (cooldownUntilTotalHours <= 0 && !hasEmptyHivetop)
                {
                    harvestableAtTotalHours = 0;
                    cooldownUntilTotalHours = worldTime + 4 / 2 * 24;
                }
            }
            if (cooldownUntilTotalHours != 0 && harvestableAtTotalHours > 0)
            {
            }
        }

        private double HarvestableTime(int i)
        {
            Random rand = new();
            return (i * .75) + ((i * .5) * rand.NextDouble());
        }

        readonly Vec3d startPos = new();
        readonly Vec3d endPos = new();
        Vec3f minVelo = new();
                             
        private void SpawnBeeParticles(float dt)
        {
            if (isActiveHive)
            {
                float dayLightStrength = Api.World.Calendar.GetDayLightStrength(Pos.X, Pos.Z);
                if (Api.World.Rand.NextDouble() > 2 * dayLightStrength - 0.5) return;

                Random rand = Api.World.Rand;

                Bees.MinQuantity = actvitiyLevel;

                // Leave hive
                if (Api.World.Rand.NextDouble() > 0.5)
                {
                    startPos.Set(Pos.X + 0.5f, Pos.Y + 0.5f, Pos.Z + 0.5f);
                    minVelo.Set((float)rand.NextDouble() * 3 - 1.5f, (float)rand.NextDouble() * 1 - 0.5f, (float)rand.NextDouble() * 3 - 1.5f);

                    Bees.MinPos = startPos;
                    Bees.MinVelocity = minVelo;
                    Bees.LifeLength = 1f;
                    Bees.WithTerrainCollision = false;
                }

                // Go back to hive
                else
                {
                    startPos.Set(Pos.X + rand.NextDouble() * 5 - 2.5, Pos.Y + rand.NextDouble() * 2 - 1f, Pos.Z + rand.NextDouble() * 5 - 2.5f);
                    endPos.Set(Pos.X + 0.5f, Pos.Y + 0.5f, Pos.Z + 0.5f);

                    minVelo.Set((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
                    minVelo /= 2;

                    Bees.MinPos = startPos;
                    Bees.MinVelocity = minVelo;
                    Bees.WithTerrainCollision = true;
                    Api.World.SpawnParticles(Bees);
                }
            }
        }

        private void OnScanForFlowers(float dt)
        {
            double worldTime = Api.World.Calendar.TotalHours;

            if (isActiveHive)
            {

                Room room = roomreg?.GetRoomForPosition(Pos);
                roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;

                if (actvitiyLevel < 1) return;
                if (Api.Side == EnumAppSide.Client) return;
                if (Api.World.Calendar.TotalHours < cooldownUntilTotalHours) return;
                if (scanIteration == 0)
                {
                    scanQuantityNearbyFlowers = 0;
                    scanQuantityNearbyHives = 0;
                }

                int minX = -8 + 8 * (scanIteration / 2);
                int minZ = -8 + 8 * (scanIteration % 2);
                int size = 8;

                Block fullSkepN = Api.World.GetBlock(new AssetLocation("skep-populated-north"));
                Block fullSkepE = Api.World.GetBlock(new AssetLocation("skep-populated-east"));
                Block fullSkepS = Api.World.GetBlock(new AssetLocation("skep-populated-south"));
                Block fullSkepW = Api.World.GetBlock(new AssetLocation("skep-populated-west"));

                Block wildhive1 = Api.World.GetBlock(new AssetLocation("wildbeehive-medium"));
                Block wildhive2 = Api.World.GetBlock(new AssetLocation("wildbeehive-large"));

                Block claypothive = Api.World.GetBlock(new AssetLocation("claypothive-populated-empty-withtop"));
                Block claypothive2 = Api.World.GetBlock(new AssetLocation("claypothive-populated-empty-notop"));
                Block claypothive3 = Api.World.GetBlock(new AssetLocation("claypothive-populated-harvestable-notop"));
                Block claypothive4 = Api.World.GetBlock(new AssetLocation("claypothive-populated-harvestable-withtop"));

                Block langstrothstacke = Api.World.GetBlock(new AssetLocation("langstrothstack-one-east"));
                Block langstrothstackn = Api.World.GetBlock(new AssetLocation("langstrothstack-one-north"));
                Block langstrothstacks = Api.World.GetBlock(new AssetLocation("langstrothstack-one-south"));
                Block langstrothstackw = Api.World.GetBlock(new AssetLocation("langstrothstack-one-west"));

                Block langstrothstack2e = Api.World.GetBlock(new AssetLocation("langstrothstack-two-east"));
                Block langstrothstack2n = Api.World.GetBlock(new AssetLocation("langstrothstack-two-north"));
                Block langstrothstack2s = Api.World.GetBlock(new AssetLocation("langstrothstack-two-south"));
                Block langstrothstack2w = Api.World.GetBlock(new AssetLocation("langstrothstack-two-west"));

                Block langstrothstack3e = Api.World.GetBlock(new AssetLocation("langstrothstack-three-east"));
                Block langstrothstack3n = Api.World.GetBlock(new AssetLocation("langstrothstack-three-north"));
                Block langstrothstack3s = Api.World.GetBlock(new AssetLocation("langstrothstack-three-south"));
                Block langstrothstack3w = Api.World.GetBlock(new AssetLocation("langstrothstack-three-west"));

                Api.World.BlockAccessor.WalkBlocks(Pos.AddCopy(minX, -5, minZ), Pos.AddCopy(minX + size - 1, 5, minZ + size - 1), (block, pos) =>
                {
                    if (block.Id == 0) return;

                    if (block.Attributes != null && block.Attributes.IsTrue("beeFeed"))
                    {
                        scanQuantityNearbyFlowers++;
                    };

                    if (block == fullSkepN || block == fullSkepE || block == fullSkepS || block == fullSkepW
                    || block == wildhive1 || block == wildhive2
                    || block == claypothive || block == claypothive2 || block == claypothive3 || block == claypothive4
                    || block == langstrothstacke || block == langstrothstackn || block == langstrothstacks || block == langstrothstackw
                    || block == langstrothstack2e || block == langstrothstack2n || block == langstrothstack2s || block == langstrothstack2w
                    || block == langstrothstack3e || block == langstrothstack3n || block == langstrothstack3s || block == langstrothstack3w)
                    {
                        scanQuantityNearbyHives++;
                    }
                });
                scanIteration++;
                if (scanIteration == 4)
                {
                    scanIteration = 0;
                    OnScanComplete();
                }

            }
        }

        private void OnScanComplete()
        {

            quantityNearbyFlowers = scanQuantityNearbyFlowers;
            quantityNearbyHives = scanQuantityNearbyHives;
            hivePopSize = (EnumHivePopSize)GameMath.Clamp(quantityNearbyFlowers - 3 * quantityNearbyHives, 0, 2);
            MarkDirty();
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (Api.World.EntityDebugMode && forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                dsc.AppendLine(
                    Lang.Get("Nearby flowers: {0}, Nearby Hives: {1}, Empty Hives: {2}, Pop after hours: {3}. harvest in {4}, repop cooldown: {5}",quantityNearbyFlowers, quantityNearbyHives, (harvestableAtTotalHours - Api.World.Calendar.TotalHours).ToString("#.##"), (cooldownUntilTotalHours - Api.World.Calendar.TotalHours).ToString("#.##"))
                    + "\n" + Lang.Get("Population Size: " + hivePopSize));
            }
            if (isActiveHive)
            {
                double worldTime = Api.World.Calendar.TotalHours;
                int daysTillHarvest = (int)Math.Round((harvestableAtTotalHours - worldTime) / 24);
                daysTillHarvest = daysTillHarvest <= 0 ? 0 : daysTillHarvest;
                string hiveState = Lang.Get("Nearby flowers: {0}\nPopulation Size: {1}", quantityNearbyFlowers, hivePopSize);
         
                dsc.AppendLine(hiveState);
                if ((harvestableAtTotalHours - worldTime / 24 > 0) && this.Block.Variant["top"] == "withtop")
                {
                    string combPopTime;
                    if (FromGoldenCombsConfig.Current.showcombpoptime) {
                        combPopTime = "Your bees will produce comb in " + (daysTillHarvest < 1 ? "less than one day" : daysTillHarvest + " days");
                        dsc.AppendLine(combPopTime);
                    } 
                }
                else if (isActiveHive && (this.Block.Variant["top"] == "notop" || inv[0].Itemstack.Collectible.Variant["type"]=="harvestable"))
                {
                    dsc.AppendLine("Hive lacks a usable honey pot, will not produce comb.");
                }
                else if (quantityNearbyFlowers>0)
                {
                    dsc.AppendLine("The bees are out gathering.");
                } else
                {
                    dsc.AppendLine("The bees are still settling in.");
                }

            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);


            tree.SetInt("scanIteration", scanIteration);

            tree.SetInt("quantityNearbyFlowers", quantityNearbyFlowers);
            tree.SetInt("quantityNearbyHives", quantityNearbyHives);


            tree.SetInt("scanQuantityNearbyFlowers", scanQuantityNearbyFlowers);
            tree.SetInt("scanQuantityNearbyHives", scanQuantityNearbyHives);

            tree.SetBool("isactivehive", isActiveHive);
            tree.SetDouble("cooldownUntilTotalHours", cooldownUntilTotalHours);
            tree.SetDouble("harvestableAtTotalHours", harvestableAtTotalHours);
            tree.SetInt("hiveHealth", (int)hivePopSize);
            tree.SetFloat("roomness", roomness);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            scanIteration = tree.GetInt("scanIteration");

            quantityNearbyFlowers = tree.GetInt("quantityNearbyFlowers");
            quantityNearbyHives = tree.GetInt("quantityNearbyHives");

            scanQuantityNearbyFlowers = tree.GetInt("scanQuantityNearbyFlowers");
            scanQuantityNearbyHives = tree.GetInt("scanQuantityNearbyHives");

            isActiveHive = tree.GetBool("isactivehive");
            cooldownUntilTotalHours = tree.GetDouble("cooldownUntilTotalHours");
            harvestableAtTotalHours = tree.GetDouble("harvestableAtTotalHours");
            hivePopSize = (EnumHivePopSize)tree.GetInt("hiveHealth");
            roomness = tree.GetFloat("roomness");

            MarkDirty(true);

        }

    }
}

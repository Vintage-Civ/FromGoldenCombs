﻿using FromGoldenCombs.BlockEntities;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FromGoldenCombs.Blocks.Langstroth
{
    class LangstrothCore : BlockContainer
    {
        //Enable selectionbox interaction
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (!slot.Empty &&
                IsValidLangstroth(block) && !(slot.Itemstack?.Block is FrameRack))
            {
                ItemStack super = this.OnPickBlock(api.World, blockSel.Position);
                api.World.BlockAccessor.SetBlock(api.World.GetBlock(
                new AssetLocation("fromgoldencombs", "langstrothstack-two-" + block.LastCodePart())).BlockId, blockSel.Position);
                BELangstrothStack lStack = (BELangstrothStack)api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                lStack.InitializePut(super, slot);
            }
            return true;
        }
        public bool IsValidLangstroth(Block block)
        {
            if (block is LangstrothCore && !(block is LangstrothBrood) && !(block is FrameRack))
            {
                return true;
            }
            return false;
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if (this is LangstrothStack)
            return base.GetPlacedBlockName(world, pos);
 
            StringBuilder sb = new();
            return base.GetPlacedBlockName(world, pos) + sb.AppendLine() + Lang.Get("fromgoldencombs:getmaterials", this.Variant["primary"].ToString().UcFirst(), this.Variant["accent"].ToString().UcFirst());
        }
    }
 }

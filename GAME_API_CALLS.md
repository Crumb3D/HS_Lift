# HS_Lift — game calls (from Source)

Not extracted from the game DLL. This is `HS_Lift/Source`. Unity / System / Harmony / our types skipped.

3.2 vs 3.3: `7DaysRef/HS_Lift_3.2_vs_3.3.md`

## Inherits
- `Block`
- `BlockPowered`
- `ConsoleCmdAbstract`
- `IModApi`
- `ItemAction`
- `NetPackage`

## HarmonyPatch / typeof game types
- `BlockCompositeTileEntity`
- `BlockValue`
- `Entity`
- `EntityAlive`
- `EntityPlayerLocal`
- `NetPackageManager`
- `PlayerMoveController`
- `string`
- `TextureAtlasBlocks`
- `TextureFullArray`
- `TileEntityPowered`
- `Vector3i`
- `WorldBase`
- `XUiC_InteractionPrompt`

## Type.Method (static / nested)
- `Block.GetBlockName`
- `Block.GetBlockValue`
- `Block.GetLightValue`
- `Block.GetLocalizedBlockName`
- `Car.InverseTransformPoint`
- `Cells.Add`
- `ExcludedColumns.Add`
- `ExcludedColumns.Clear`
- `ExcludedColumns.RemoveAll`
- `FieldType.GetGenericArguments`
- `Floors.Add`
- `Floors.Clear`
- `Floors.Find`
- `Floors.FindIndex`
- `Floors.Remove`
- `Floors.RemoveAll`
- `Floors.Sort`
- `GameIO.GetSaveGameDir`
- `GameIO.GetUserGameDataDir`
- `GameManager.ShowTooltip`
- `GameManager.ShowTooltipMP`
- `GameStartDone.RegisterHandler`
- `Instance.GetPowerItemByWorldPos`
- `Instance.Output`
- `ItemClass.GetForId`
- `Lifts.Add`
- `Lifts.AddRange`
- `Lifts.Clear`
- `Lifts.Contains`
- `Lifts.Find`
- `Lifts.FindIndex`
- `Localization.Get`
- `Log.Error`
- `Log.Out`
- `Log.Warning`
- `Manager.BroadcastPlay`
- `Manager.BroadcastPlayByLocalPlayer`
- `Manager.PlayInsidePlayerHead`
- `Manager.StopLoopInsidePlayerHead`
- `Name.Trim`
- `Panels.Add`
- `Panels.Clear`
- `Panels.Exists`
- `Panels.Find`
- `Panels.FindAll`
- `Panels.RemoveAll`
- `PassThroughBlocks.Add`
- `PassThroughBlocks.Exists`
- `PassThroughBlocks.RemoveAll`
- `PlayerSpawnedInWorld.RegisterHandler`
- `Text.IndexOf`
- `Text.StringBuilder`
- `Versioning.TargetFrameworkAttribute`
- `World.GetPrimaryPlayer`
- `WorldShuttingDown.RegisterHandler`
- `XUiC_InteractionPrompt.SetText`
- `XUiC_Radial.RadialContextHoldingSlotIndex`

## new
- `new BlockActivationCommand`
- `new BlockChangeInfo`
- `new BlockValue`
- `new string`
- `new Vector3i`

## override (must still exist on 3.3 base)
- `CanInteract`
- `CreateTileEntity`
- `Execute`
- `ExecuteAction`
- `GetActivationText`
- `GetBlockActivationCommands`
- `getCommands`
- `getDescription`
- `getHelp`
- `GetLength`
- `HasBlockActivationCommands`
- `HasRadial`
- `OnBlockActivated`
- `OnBlockEntityTransformAfterActivated`
- `ProcessPackage`
- `read`
- `SetupRadial`
- `write`

## By file (line)
### HSGameVersion.cs
- `20: Log.Error`
- `25: Log.Error`
- `31: Log.Out`

### HSLiftAtlas.cs
- `3: HarmonyPatch TextureAtlasBlocks`

### HSLiftCabinLights.cs
- `92: new Vector3i`

### HSLiftCar.cs
- `106: ExcludedColumns.RemoveAll`
- `127: ExcludedColumns.Add`
- `203: new Vector3i`
- `208: new BlockChangeInfo`
- `224: new Vector3i`
- `238: new Vector3i`
- `239: new Vector3i`
- `243: new Vector3i`
- `244: new Vector3i`
- `260: Block.GetBlockName`
- `266: Block.GetBlockValue`
- `267: Block.GetBlockValue`
- `268: Block.GetBlockValue`
- `278: new Vector3i`
- `282: new BlockChangeInfo`
- `295: new Vector3i`
- `333: new BlockChangeInfo`
- `333: new Vector3i`
- `339: new Vector3i`
- `353: new BlockChangeInfo`
- `365: new Vector3i`
- `441: new Vector3i`
- `488: new Vector3i`
- `551: new Vector3i`
- `553: new Vector3i`
- `581: new Vector3i`
- `592: Block.GetBlockName`
- `606: Block.GetLocalizedBlockName`
- `610: Block.GetBlockName`
- `645: new BlockChangeInfo`
- `657: Block.GetBlockName`
- `665: Block.GetLocalizedBlockName`
- `694: Block.GetBlockName`
- `702: Block.GetLocalizedBlockName`
- `728: new string`
- `760: new BlockChangeInfo`
- `761: new BlockChangeInfo`
- `785: new Vector3i`
- `815: new BlockChangeInfo`
- `845: new Vector3i`
- `851: new Vector3i`
- `909: new Vector3i`
- `914: Block.GetBlockName`
- `921: new Vector3i`
- `992: ItemClass.GetForId`
- `1007: new Vector3i`
- `1029: Block.GetBlockName`
- `1034: Block.GetLightValue`
- `1144: Cells.Add`
- `1202: new Vector3i`
- `1204: new BlockValue`
- `1209: Block.GetBlockName`
- `1239: new BlockChangeInfo`
- `1266: new Vector3i`
- `1275: new BlockChangeInfo`
- `1276: Block.GetBlockName`
- `1293: Block.GetBlockName`
- `1308: new Vector3i`
- `1310: new BlockValue`

### HSLiftCommands.cs
- `4: inherits ConsoleCmdAbstract`
- `6: override getCommands`
- `11: override getDescription`
- `19: override getHelp`
- `52: override Execute`
- `81: Instance.Output`

### HSLiftConfiguration.cs
- `12: new Vector3i`
- `146: GameIO.GetSaveGameDir`
- `234: Lifts.Add`
- `257: Lifts.Add`
- `275: Lifts.AddRange`
- `286: Lifts.Add`
- `295: Lifts.Clear`
- `297: Lifts.Add`
- `315: PassThroughBlocks.RemoveAll`
- `317: PassThroughBlocks.Exists`
- `318: PassThroughBlocks.Add`
- `319: PassThroughBlocks.Exists`
- `320: PassThroughBlocks.Add`
- `321: PassThroughBlocks.Exists`
- `322: PassThroughBlocks.Add`
- `323: PassThroughBlocks.Exists`
- `324: PassThroughBlocks.Add`
- `325: PassThroughBlocks.Exists`
- `326: PassThroughBlocks.Add`
- `327: PassThroughBlocks.Exists`
- `328: PassThroughBlocks.Add`
- `329: PassThroughBlocks.Exists`
- `330: PassThroughBlocks.Add`
- `331: PassThroughBlocks.Exists`
- `332: PassThroughBlocks.Add`
- `333: PassThroughBlocks.Exists`
- `334: PassThroughBlocks.Add`
- `335: PassThroughBlocks.Exists`
- `336: PassThroughBlocks.Add`
- `341: Floors.Add`
- `342: Floors.Add`
- `360: Lifts.FindIndex`
- `362: Lifts.Contains`
- `362: Lifts.Add`
- `380: Lifts.Find`
- `402: Lifts.Add`
- `423: Panels.Exists`
- `512: new Vector3i`
- `531: Floors.RemoveAll`
- `532: Floors.Sort`
- `548: Floors.Find`
- `553: Floors.Find`
- `558: Floors.FindIndex`

### HSLiftController.cs
- `217: Panels.Find`
- `219: Floors.Find`
- `295: Localization.Get`
- `303: new Vector3i`
- `415: Localization.Get`
- `679: new Vector3i`
- `681: new Vector3i`
- `698: new Vector3i`
- `756: World.GetPrimaryPlayer`
- `758: Manager.PlayInsidePlayerHead`
- `780: World.GetPrimaryPlayer`
- `781: Manager.StopLoopInsidePlayerHead`
- `796: Manager.BroadcastPlay`

### HSLiftDebug.cs
- `11: Log.Out`
- `16: Log.Out`
- `21: Log.Warning`
- `26: Log.Error`

### HSLiftDoors.cs
- `106: new Vector3i`
- `168: new Vector3i`
- `196: new Vector3i`
- `197: new Vector3i`
- `213: new Vector3i`
- `394: new Vector3i`
- `401: new Vector3i`
- `421: Block.GetBlockName`

### HSLiftFloorMenu.cs
- `27: new BlockActivationCommand`
- `43: Localization.Get`

### HSLiftFloorSigns.cs
- `88: new Vector3i`
- `93: Block.GetBlockName`
- `98: Block.GetBlockValue`
- `105: new BlockChangeInfo`
- `114: Name.Trim`

### HSLiftMod.cs
- `6: inherits IModApi`
- `18: GameIO.GetUserGameDataDir`
- `28: GameStartDone.RegisterHandler`
- `29: WorldShuttingDown.RegisterHandler`
- `30: PlayerSpawnedInWorld.RegisterHandler`

### HSLiftNet.cs
- `60: FieldType.GetGenericArguments`
- `167: new Vector3i`
- `173: new Vector3i`
- `196: Localization.Get`
- `210: GameManager.ShowTooltip`
- `225: GameManager.ShowTooltip`
- `228: GameManager.ShowTooltipMP`
- `246: inherits NetPackage`
- `280: override read`
- `290: new Vector3i`
- `306: new BlockValue`
- `319: override write`
- `354: override ProcessPackage`
- `427: override GetLength`
- `435: HarmonyPatch NetPackageManager`

### HSLiftPaint.cs
- `87: Text.StringBuilder`
- `248: new Vector3i`

### HSLiftPower.cs
- `17: Instance.GetPowerItemByWorldPos`

### HSLiftSettings.cs
- `38: GameIO.GetSaveGameDir`

### HSLiftSetup.cs
- `13: GameManager.ShowTooltip`
- `14: Instance.Output`
- `147: Panels.FindAll`
- `182: ExcludedColumns.Clear`
- `221: ExcludedColumns.RemoveAll`
- `222: ExcludedColumns.Add`
- `225: Block.GetBlockName`
- `227: Block.GetBlockName`
- `252: Floors.Add`
- `276: Floors.Add`
- `298: Floors.Remove`
- `320: Floors.RemoveAll`
- `321: Floors.Sort`
- `349: Floors.Sort`
- `416: Floors.Clear`
- `417: Panels.Clear`
- `418: ExcludedColumns.Clear`
- `431: Floors.Add`
- `450: Panels.Clear`
- `472: Block.GetBlockName`
- `476: Panels.RemoveAll`
- `477: Panels.Add`
- `492: new Vector3i`

### HSLiftSounds.cs
- `237: Car.InverseTransformPoint`

### ItemActionHSLiftTool.cs
- `7: inherits ItemAction`
- `48: override HasRadial`
- `53: override ExecuteAction`
- `59: GameManager.ShowTooltip`
- `59: Localization.Get`
- `67: override CanInteract`
- `69: Localization.Get`
- `72: override SetupRadial`
- `85: Localization.Get`
- `86: Localization.Get`
- `87: Localization.Get`
- `88: Localization.Get`
- `89: Localization.Get`
- `90: Localization.Get`
- `91: Localization.Get`
- `92: Localization.Get`
- `93: Localization.Get`
- `97: XUiC_Radial.RadialContextHoldingSlotIndex`

### Blocks\BlockHSLiftInsidePanel.cs
- `4: inherits Block`
- `6: override HasBlockActivationCommands`
- `11: override GetBlockActivationCommands`
- `26: override GetActivationText`
- `33: Localization.Get`
- `35: Localization.Get`
- `47: override OnBlockEntityTransformAfterActivated`
- `54: override OnBlockActivated`
- `59: override OnBlockActivated`
- `70: GameManager.ShowTooltip`
- `70: Localization.Get`
- `78: GameManager.ShowTooltip`
- `78: Localization.Get`

### Blocks\BlockHSLiftOutsidePanel.cs
- `4: inherits BlockPowered`
- `6: override CreateTileEntity`
- `11: override HasBlockActivationCommands`
- `16: override GetBlockActivationCommands`
- `24: new BlockActivationCommand`
- `33: override GetActivationText`
- `40: Localization.Get`
- `42: Localization.Get`
- `45: Localization.Get`
- `58: Localization.Get`
- `62: override OnBlockEntityTransformAfterActivated`
- `68: override OnBlockActivated`
- `79: GameManager.ShowTooltip`
- `79: Localization.Get`

### obj\Release\.NETFramework,Version=v4.8.AssemblyAttributes.cs
- `4: Versioning.TargetFrameworkAttribute`

### Patches\HSLiftDoorPatch.cs
- `7: HarmonyPatch BlockCompositeTileEntity`
- `32: GameManager.ShowTooltip`
- `32: Localization.Get`
- `69: GameManager.ShowTooltip`
- `69: Localization.Get`
- `70: Manager.BroadcastPlayByLocalPlayer`
- `83: Localization.Get`
- `87: Localization.Get`
- `87: Localization.Get`
- `89: GameManager.ShowTooltip`
- `91: Manager.BroadcastPlayByLocalPlayer`
- `133: HarmonyPatch BlockCompositeTileEntity`
- `173: HarmonyPatch BlockCompositeTileEntity`
- `186: Localization.Get`
- `186: Localization.Get`

### Patches\HSLiftLightPatch.cs
- `3: HarmonyPatch TileEntityPowered`

### Patches\HSLiftToolPatch.cs
- `6: HarmonyPatch PlayerMoveController`
- `25: XUiC_InteractionPrompt.SetText`
- `25: Localization.Get`
- `55: XUiC_InteractionPrompt.SetText`
- `64: Localization.Get`
- `81: HarmonyPatch XUiC_InteractionPrompt`
- `88: Localization.Get`
- `90: Text.IndexOf`
- `96: XUiC_InteractionPrompt.SetText`



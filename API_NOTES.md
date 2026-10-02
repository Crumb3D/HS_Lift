# HSLift - 3.2 API notes

Verified against `7DaysToDie_Data/Managed/Assembly-CSharp.dll` (3.2) with `Mods/_tools/ApiProbe`
(`dotnet run -- <Type> <memberRegex>`, `il <Type> <Method>`, `callers <Type::Method>`).

## No elevator API
- No Elevator/Lift/Mover class. `liftHydraulic*` is static deco. `elevatorDoor*` are plain doors.
- `elevator*Panel*` blocks are `CreativeMode=Dev`, no Class, no recipe (deco only).
- No elevator recipes in vanilla or any installed mod.

## Riding
- `vp_FPController.UpdateCollisions`: `if (ground.collider.gameObject.layer == 28) m_Platform = ground.transform` (28 = `vp_Layer.MovableObject`).
- `FixedMove` carries the player by `m_Platform.TransformPoint(m_PositionOnPlatform)`.
- `EntityPlayerLocal.vp_FPController`, `vp_FPController.GroundTransform`.

## Blocks
- `WorldBase.GetBlock(Vector3i)`, `World.GetTileEntity(Vector3i)`, `World.IsRemote()`, `World.GetPrimaryPlayer()`.
- `WorldBase.SetBlocksRPC(List<BlockChangeInfo>)`.
- `new BlockChangeInfo(BlockValueRef, BlockValue, sbyte density, TextureFullArray tex)` (BlockValueRef has implicit from Vector3i).
- `Chunk.GetTextureFullArray(x,y,z,bool applyIgnore=true)`, `Chunk.GetDensity(x,y,z)` (local coords via `World.toBlockXZ`/`toBlockY`).
- `BlockValue`: `rawData` (uint), `damage`, `ischild`, `rotation`, `Block`, `isair`, `BlockValue.Air`, ctor `(uint, int damage)`.
- `Block`: `HasTileEntity`, `isMultiBlock`, `multiBlockPos` (`Get(idx, type, rot)`, `Length`, `GetParentPos(pos, bv)`), `shape.GetRotation(bv)`, `shape.IsTerrain()`.

## Drawing blocks off-grid (as EntityFallingBlock.CreateMesh)
- `ItemClass.GetForId(bv.ToItemType())`, `ItemClass.CloneModel(World, ItemValue, Vector3 pos, Transform parent, MeshPurpose=World, TextureFullArray tex)`.
- then `transform.rotation = bv.Block.shape.GetRotation(bv)`, disable clone colliders, `Utils.SetColliderLayerRecursively(go, layer)`.
- Unity positions are `worldPos - Origin.position`.

## Power
- `TileEntityPowered.IsPowered`, `PowerManager.Instance.GetPowerItemByWorldPos(Vector3i).IsPowered`.
- `BlockPowered` virtuals: `Init`, `CreateTileEntity(Chunk)`, `GetBlockActivationCommands`, `GetActivationText`, `OnBlockActivated(string, WorldBase, Vector3i, BlockValue, EntityPlayerLocal)`.
- XML `Class="Name, HSLift"` -> C# class `BlockName` (SCore pattern).

## Misc
- `ConsoleCmdAbstract`: `getCommands`, `getDescription`, `getHelp`, `Execute(List<string>, CommandSenderInfo)`; output via `SdtdConsole.Instance.Output`.
- `EntityPlayerLocal.HitInfo` (`WorldRayHitInfo.bHitValid`, `.hit.blockPos`).
- `ModEvents.GameStartDone`, `WorldShuttingDown`; `GameManager.IsDedicatedServer`, `ConnectionManager.Instance.IsServer`.

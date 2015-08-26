using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Components;
using VRage.ObjectBuilders;
using VRageMath;

//Credit for the scripts in this mod goes to Jimmacle
#warning Proper mod functionality will require GitHub PR #407 to be merged

namespace DBMassDriver
{
    #region DamageSystem
    public static class Data
    {
        public static List<MDInfo> Drivers;
    }

    public class MDInfo
    {
        public long Id;
        public int DamageMultiplier;

        public MDInfo(long EntID, int damageMultiplier)
        {
            Id = EntID;
            DamageMultiplier = damageMultiplier;
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class MassDriverCore : MySessionComponentBase
    {
        private bool init = false;
        public void DamageHandler(object entity, ref MyDamageInformation info)
        {
            try
            {
                long attacker = info.AttackerId;
                MyAPIGateway.Utilities.ShowMessage("", info.AttackerId.ToString());

                
                MDInfo sourceDriver = Data.Drivers.Find(d => d.Id == attacker);
                if (sourceDriver != null)
                {
                    info.Amount = info.Amount * sourceDriver.DamageMultiplier;
                }
            }
            catch
            {

            }
        }

        public void Init()
        {
            //MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(1, DamageHandler);
#warning Damage modifier disabled until damage system properly shows attackerId
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            Init();
            init = true;
        }
    }
    #endregion

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallGatlingGun), new string[] { "MassDriverBody" })]
    public class MassDriverBody : MyGameLogicComponent
    {
        MyObjectBuilder_EntityBase ObjectBuilder; //object builder for block instance
        IMySmallGatlingGun MDBody;
        List</*Sandbox.ModAPI.*/IMyBatteryBlock> compulsators;
        private bool m_enableOnce = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            MDBody = Entity as IMySmallGatlingGun;
            ObjectBuilder = objectBuilder;
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy && ObjectBuilder != null ? ObjectBuilder.Clone() as MyObjectBuilder_EntityBase : ObjectBuilder;
        }

        public override void Close()
        {
            base.Close();
        }

        public override void UpdateBeforeSimulation()
        {
#warning TODO: Find source of NullReferenceExceptions and fix
            try
            {
                GetMultiblockConfig();
            }
            catch { }

            if (EnoughPower())
            {
                MDBody.SetCustomName(MDBody.CustomName + ": READY");
                if (!m_enableOnce)
                {
                    MDBody.ApplyAction("OnOff_On");
                    m_enableOnce = true;
                }
            }
            else
            {
                //force block to be off to prevent firing
                MDBody.ApplyAction("OnOff_Off");
                MDBody.SetCustomName(MDBody.CustomName + ": CHARGE");
                m_enableOnce = false;
            }

            if (MDBody.IsShooting)
            {
                /* Needs #407
                foreach (Sandbox.ModAPI.IMyBatteryBlock batt in compulsators)
                {
                    batt.SetCurrentStoredPower(batt.CurrentStoredPower - (1210f / compulsators.Count));
                }*/
            }

            base.UpdateBeforeSimulation();
        }

        public bool EnoughPower() //return true if connected stored power is > 1.21 GW
        {
            float charge = 0;
            foreach (/*Sandbox.ModAPI.*/IMyBatteryBlock batt in compulsators)
            {
                charge += batt.CurrentStoredPower;
            }
            return charge > 1210f;
        }

        public void GetMultiblockConfig()
        {

#warning TODO: Restrict barrel placement to directly in front of the main body

            List<Sandbox.ModAPI.IMySlimBlock> connectedBlocks = new List<Sandbox.ModAPI.IMySlimBlock>();

            Sandbox.ModAPI.IMyCubeGrid grid = (Sandbox.ModAPI.IMyCubeGrid)MDBody.CubeGrid;
            Matrix localMatrix = MDBody.LocalMatrix;
            List<Vector3I> searchQueue = new List<Vector3I>()
            {
                FindOffset(localMatrix, new Vector3I(2, 0, 1)),  //right port
                FindOffset(localMatrix, new Vector3I(-2, 0, 1)), //left port
                FindOffset(localMatrix, new Vector3I(0, 0, 3)),  //front port
            };

            while (searchQueue.Count > 0)
            {
                Vector3I searchLocation = searchQueue[0];

                Sandbox.ModAPI.IMySlimBlock block = grid.GetCubeBlock(searchLocation);

                if (block != null)
                {
                    if (!connectedBlocks.Contains(block))
                    {
                        if (block.GetObjectBuilder().SubtypeId.ToString().Contains("MassDriver"))
                        {
                            connectedBlocks.Add(block);
                        }

                        switch (block.GetObjectBuilder().SubtypeId.ToString())
                        {
                            case "MassDriverCord":
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, 1)));   //front port
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, -1)));  //back port
                                break;
                            case "MassDriverCordSupport":
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, 1)));   //front port
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, -1)));  //back port
                                break;
                            case "MassDriverCordTurn":
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(1, 0, 0)));   //right port
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, -1, 0)));  //bottom port
                                break;
                            case "MassDriverCapacitor":
                                //need to change the translation matrix a little because of the block's even-block-count depth and height
                                Vector3 fixedCapTranslation = block.FatBlock.LocalMatrix.Translation + block.FatBlock.LocalMatrix.Forward + block.FatBlock.LocalMatrix.Down;
                                Matrix fixedCapMatrix = block.FatBlock.LocalMatrix;
                                fixedCapMatrix.Translation = fixedCapTranslation;

                                searchQueue.Add(FindOffset(fixedCapMatrix, new Vector3I(0, 0, 2)));  //front port
                                searchQueue.Add(FindOffset(fixedCapMatrix, new Vector3I(0, 0, -3))); //back port
                                searchQueue.Add(FindOffset(fixedCapMatrix, new Vector3I(0, -1, 0))); //bottom port
                                break;
                            case "MassDriverBarrelSector":
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, 2)));  //front port
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, -2))); //back port
                                break;
                            case "MassDriverBarrelTip":
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, -2))); //back port
                                break;
                            default:

                                break;
                        }
                    }
                }

                searchQueue.RemoveAt(0);
            }

            //count attached parts
            //
            int barrelCount = connectedBlocks.FindAll(b => b.GetObjectBuilder().SubtypeId.ToString().Contains("MassDriverBarrel")).Count;
            int compulsatorCount = connectedBlocks.FindAll(b => b.GetObjectBuilder().SubtypeId.ToString() == "MassDriverCapacitor").Count;

            //build list of connected batteries
            //
            compulsators = new List</*Sandbox.ModAPI.*/IMyBatteryBlock>();
            foreach (var batt in connectedBlocks.FindAll(b => b.GetObjectBuilder().SubtypeId.ToString() == "MassDriverCapacitor"))
            {
                compulsators.Add(batt.FatBlock as /*Sandbox.ModAPI.*/IMyBatteryBlock);
            }

            //update custom name and info instance
            //
            MDBody.SetCustomName("Mass Driver (" + barrelCount.ToString() + "B|" + compulsatorCount.ToString() + "C)");

            if (Data.Drivers.Find(d => d.Id == MDBody.EntityId) == null) //add entry for damage handler
            {
                Data.Drivers.Add(new MDInfo(MDBody.EntityId, compulsatorCount));
            }
            else
            {
                MDInfo existingEntry = Data.Drivers.Find(d => d.Id == MDBody.EntityId);
                existingEntry.DamageMultiplier = compulsatorCount;
            }

            MyAPIGateway.Utilities.ShowMessage("", Data.Drivers.Count().ToString());
        }

        public Vector3I FindOffset(Matrix localMatrix, Vector3I offset)
        {
            Vector3I output = new Vector3I();
            output += offset.X * localMatrix.Right.ToVector3I();
            output += offset.Y * localMatrix.Up.ToVector3I();
            output += offset.Z * localMatrix.Forward.ToVector3I();
            return localMatrix.Translation.ToVector3IPos() + output;
        }
    }

    static class Extensions
    {
        public static Vector3I ToVector3IPos (this Vector3 input)
        {
            Vector3I output = new Vector3I();
            output.X = (int)Math.Round(input.X / 2.5f);
            output.Y = (int)Math.Round(input.Y / 2.5f);
            output.Z = (int)Math.Round(input.Z / 2.5f);

            return output;
        }

        public static Vector3I ToVector3I (this Vector3 input)
        {
            return new Vector3I((int)input.X, (int)input.Y, (int)input.Z);
        }
    }
}
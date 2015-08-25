using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox;
using Sandbox.Common;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Components;
using Sandbox.Common.Components;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRageMath;

//Credit for the scripts in this mod goes to Jimmacle

namespace DBMassDriver
{
    public static class Data
    {
        public static List<MassDriverInfo> Drivers;
    }

    public class MassDriverInfo
    {
        public long entityId;
        public int barrelCount;
        public int compulsatorCount;

        public MassDriverInfo(long EntID, int bCount, int cCount)
        {
            entityId = EntID;
            barrelCount = bCount;
            compulsatorCount = cCount;
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

                //MassDriverInfo sourceDriver = Data.Drivers.Find(d => d.entityId == attacker);
                /*
                if (sourceDriver != null)
                {
                    info.Amount = (info.Amount + sourceDriver.barrelCount) * sourceDriver.compulsatorCount;
                    MyAPIGateway.Utilities.ShowMessage("", info.Amount.ToString());
                }*/

                MyAPIGateway.Utilities.ShowMessage("", info.Amount.ToString());
            }
            catch
            {

            }
        }

        public void Init()
        {
            //MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(1, DamageHandler);
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            Init();
            init = true;
        }
    }

    //MASS DRIVER BODY CLASS
    //
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallGatlingGun), new string[] { "MassDriverBody" })]
    public class MassDriverBody : MyGameLogicComponent
    {
        MyObjectBuilder_EntityBase ObjectBuilder; //object builder for block instance
        IMySmallGatlingGun MDBody;
        List<Sandbox.ModAPI.IMyBatteryBlock> compulsators;

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
            try
            {
                GetMultiblockConfig();
            }
            catch (Exception ex)
            { 
                //MyAPIGateway.Utilities.ShowMessage("", ex.Message);
            }

            if (EnoughPower()) //if conditions are met
            {
                MDBody.SetCustomName(MDBody.CustomName + ": READY");
				foreach (Sandbox.ModAPI.IMyBatteryBlock batt in compulsators)
				{
					batt.SetCurrentStoredPower(0f);
				}
            }
            else
            {
                MDBody.ApplyAction("OnOff_Off");
                MDBody.SetCustomName(MDBody.CustomName + ": CHARGE");
            }

            if (MDBody.IsShooting)
            {
                //drain 1.21GW from battery bank
                foreach (Sandbox.ModAPI.IMyBatteryBlock batt in compulsators)
                {
                    batt.Close();
                }
            }
            else
            {

            }

            base.UpdateBeforeSimulation();
        }

        public bool EnoughPower()
        {
            float charge = 0;
            foreach (Sandbox.ModAPI.IMyBatteryBlock batt in compulsators)
            {
                charge += batt.CurrentStoredPower;
            }
            MyAPIGateway.Utilities.ShowMessage("power", charge.ToString());
            return charge > 1210f;
        }

        public void GetMultiblockConfig()
        {
            //
            //TODO: Restrict barrel placement to directly in front of the main body
            //

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
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, 2)));  //front port
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, 0, -2))); //back port
                                searchQueue.Add(FindOffset(block.FatBlock.LocalMatrix, new Vector3I(0, -1, 1))); //bottom port
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
            compulsators = new List<Sandbox.ModAPI.IMyBatteryBlock>();
            foreach (var batt in connectedBlocks.FindAll(b => b.GetObjectBuilder().SubtypeId.ToString() == "MassDriverCapacitor"))
            {
                compulsators.Add(batt.FatBlock as Sandbox.ModAPI.IMyBatteryBlock);
            }

            //update custom name and info instance
            //
            MDBody.SetCustomName("Mass Driver (" + barrelCount.ToString() + "B|" + compulsatorCount.ToString() + "C)");

            if (Data.Drivers.Find(d => d.entityId == MDBody.EntityId) == null) //add entry for damage handler
            {
                Data.Drivers.Add(new MassDriverInfo(MDBody.EntityId, barrelCount, compulsatorCount));
            }
            else
            {
                MassDriverInfo existingEntry = Data.Drivers.Find(d => d.entityId == MDBody.EntityId);
                existingEntry.barrelCount = barrelCount;
                existingEntry.compulsatorCount = compulsatorCount;
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
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Game.Components;
using VRage.Game;
using Sandbox.Game.Entities;
using VRage.Utils;

/* NoScrapShrapnel
 * 
 * Removes scrap metal when a block is destroyed or a player drops it
 * Does not remove already existing scrap metal
 * 
 * Made by CZauX in a day for Tal Maru
 * Thanks to Rexxar for MyFloatingObject
 * Thanks to Jimmacle for believing in the me that believes in me
 * Pheonix84 best dev
 */
namespace Czaux.NoScrapShrapnel
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NoShrapnel : MySessionComponentBase
    {
        public static bool m_init { get; private set; }

        //Our hash for the "Scrap" subtype
        public static MyStringHash m_scraphash = MyStringHash.GetOrCompute("Scrap");

        //Just has to sit here and look pretty.
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            
        }

        public void init()
        {
            //Tell our loop below that init has executed.
            m_init = true;

            //Only on the server
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                //Bind function to the 'OnEntityAdd' session event.
                MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            }

        }

        //Do our actual removal.
        void Entities_OnEntityAdd(IMyEntity entity)
        {

            MyFloatingObject floaty = entity as MyFloatingObject;
            //Make sure its not null
            if(floaty?.Item != null)
            {
                //Also make sure its not null
                if (floaty.Item.Content?.SubtypeId != null)
                {
                    MyStringHash subtype = floaty.Item.Content.SubtypeId;
                    //Do a 2fast4u comparison
                    if (m_scraphash.Equals(subtype))
                    {
                        //Delete the entity
                        entity.Close();
                    }
                }
            }

        }

        public override void UpdateBeforeSimulation()
        {
            //We execute this as a 'one-shot', because the topmost
            //Init doesn't have the information for MyAPIGateway
            if (!m_init)
            {
                init();
            }
        }

        protected override void UnloadData()
        {
            //Remove our function
            MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
        }
    }
}

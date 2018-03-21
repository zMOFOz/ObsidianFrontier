using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Takeshi.HoverEngine
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
	public class HoverEngine_Core: MySessionComponentBase
	{

		public static Dictionary<long, hengine> HoverEngines = new Dictionary<long, hengine>();

		MyObjectBuilder_SessionComponent m_objectBuilder;

		bool m_init = false;
		public const ushort HandlerId = 45339;

		public override void Init (MyObjectBuilder_SessionComponent sessionComponent)
		{
			m_objectBuilder = sessionComponent;
		}

		public override void UpdateBeforeSimulation ()
		{
			if (!m_init)
			{
				init ();
			}
		}

		public override void UpdateAfterSimulation ()
		{
			if(MyAPIGateway.Multiplayer.IsServer)
			{
			// if necessary
			}
		}

		public override MyObjectBuilder_SessionComponent GetObjectBuilder ()
		{
			return m_objectBuilder;
		}

		protected override void UnloadData ()
		{
			if(m_init) 
			{
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(HandlerId, HoverEngineMessageHandler);
			}

			HoverEngine_Core.HoverEngines.Clear();
		}

		void init() 
		{
			MyAPIGateway.Multiplayer.RegisterMessageHandler(HandlerId, HoverEngineMessageHandler);
			m_init = true;
		}

		private void HoverEngineMessageHandler(byte[] message)
		{
			hengine.DetailData details = MyAPIGateway.Utilities.SerializeFromXML<hengine.DetailData>(ASCIIEncoding.ASCII.GetString(message));
			foreach(var h in HoverEngine_Core.HoverEngines) 
			{
				if(details.EntityId == h.Key)
				{
					h.Value.updateclients(details.Details);
				}
			}			
		}
	}
}


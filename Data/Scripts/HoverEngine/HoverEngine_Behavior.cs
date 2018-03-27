using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
//using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Utils;
using System.Text;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Sync;



namespace Takeshi.HoverEngine
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false, 
	                             "SmallAtmosphericHoverEngine_SmallBlock",
	                             "LargeAtmosphericHoverEngine_SmallBlock",
	                             "SmallAtmosphericHoverEngine_LargeBlock",
	                             "LargeAtmosphericHoverEngine_LargeBlock",
	                             "SmallAtmosphericHoverEngine_SmallBlockV2",
	                             "LargeAtmosphericHoverEngine_SmallBlockV2",
	                             "SmallAtmosphericHoverEngine_LargeBlockV2",
	                             "LargeAtmosphericHoverEngine_LargeBlockV2",
	                             
	                             "SmallAtmosphericHoverEngine_SmallBlock_Armored_Full",
	                             "LargeAtmosphericHoverEngine_SmallBlock_Armored_Full",
	                             "SmallAtmosphericHoverEngine_LargeBlock_Armored_Full",
	                             "LargeAtmosphericHoverEngine_LargeBlock_Armored_Full",
	                             "SmallAtmosphericHoverEngine_SmallBlockV2_Armored_Half",
	                             "LargeAtmosphericHoverEngine_SmallBlockV2_Armored_Half",
	                             "SmallAtmosphericHoverEngine_LargeBlockV2_Armored_Half",
	                             "LargeAtmosphericHoverEngine_LargeBlockV2_Armored_Half"

	                             
	                            )]
    public class hengine : MyGameLogicComponent
    {
    	bool m_isserver = false;
    	bool init = false;
    	bool initshowstate=false;
    	
    	bool showstatenextframe=true;
    	    	
    	public const ushort HandlerId = 45339;
		
		public long m_testvar = 0;
    	
		private MyObjectBuilder_EntityBase objectBuilder;
		private IMyThrust block;
		
		//private readonly Sync<float> height_target_min = 1.0f;
		public float height_target_min = 1.0f;
		public float height_target_min_smooth = 1.0f;
		public float height_target_range = 1.0f;
		public float height_target_regulationdistance = 1.0f;
		public bool aligntogravity = true;
		public bool debug = false; 
		public bool force_to_centerofmass = false;
		public Color m_color1 = new Color (0,255,0,255); // lighter green after Keens graphic overhaoul 2/2018
		
		public bool hit1_own_grid;
		public bool hit2_own_grid;
			
		float m_thrustmulti;
		
		float height_target_max;
		float height_target_recalc;
		
		float thrust_max;
		float dist;
		float dist_lt;
		float dist_delta_lt;
		float dist2;
		float dist2_lt;
		float dist2_delta_lt;
		
		bool collisionalert=false;	
		float forcemulti;	
		float gridsizespecial;
		float gravitymulti;
		
		Vector3D sensorpath_startpoint;
		Vector3D sensorpath_destinationpoint;
		Vector3D force_acting_position;
		
        public void BlockUpdate()
        {
            var grid = block.CubeGrid as MyCubeGrid;
           	if(grid.Physics == null || grid.Physics.IsStatic) return;
            
           	if(height_target_min > height_target_min_smooth)
           	{
           		height_target_min_smooth += 0.02f;
           	}
           	if(height_target_min < height_target_min_smooth)
           	{
           		height_target_min_smooth -= 0.01f;
           	}
           	
            height_target_max = height_target_min_smooth+height_target_range;
 
            IHitInfo hit;
            IHitInfo hit2;
            
            Vector3D vel = grid.Physics.GetVelocityAtPoint(grid.WorldMatrix.Translation);
    
            float speed = (float)vel.Length();

			Vector3D Forward = block.WorldMatrix.Forward;
			float speedforward = (float)Forward.Dot(vel); // Forward is down
			Vector3D force = new Vector3(0, 0, 0);
			Vector3D force2 = new Vector3(0, 0, 0);
			Vector3D torque = new Vector3(0, 0, 0);
									
			thrust_max=block.MaxThrust;
			//to prevent problems with speedmods
			if(speed>100f) 
			{
				vel = vel/speed*100f;
				speed = 100f;
			}
			height_target_recalc = height_target_min_smooth+(height_target_max-height_target_min_smooth)*speed/100f;
			
			m_thrustmulti=1f;
			
			hit1_own_grid=false;
			hit2_own_grid=false;
			
			//castray 1
			sensorpath_startpoint = block.GetPosition() + block.WorldMatrix.Forward* gridsizespecial;
			sensorpath_destinationpoint = grid.Physics.CenterOfMassWorld + block.WorldMatrix.Forward * (height_target_recalc *7f + height_target_regulationdistance+2f);
			
			if (MyAPIGateway.Physics.CastRay(sensorpath_startpoint,sensorpath_destinationpoint, out hit ))
			{//raycast pyramide

            
				dist = (float)(block.GetPosition()-hit.Position).Length();
				dist_delta_lt = dist - dist_lt;
				
				//check forward
				collisionalert=false;
				dist2=speed/2f;
				
			    //check if the hit is on its own		           
			    if(hit.HitEntity as IMyCubeGrid != null)  // in case it is Voxel
			    {
					if(MyAPIGateway.GridGroups.HasConnection(hit.HitEntity as IMyCubeGrid, block.CubeGrid as IMyCubeGrid, GridLinkTypeEnum.Logical))			    
					{
						//sensor path hits own grid, deactivate thruster and set dist to height target so there is no reason for push or pull
						m_thrustmulti=0f;
						dist = height_target_recalc;
						hit1_own_grid=true;					
					}
			    }
				//moved behind Hit to show path if hit the own grid
				if(debug || (hit1_own_grid && !MyAPIGateway.Utilities.IsDedicated)) { MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"),hit1_own_grid ? Color.Red : Color.Blue *0.5f  , sensorpath_startpoint, sensorpath_destinationpoint - sensorpath_startpoint, 10, 0.05f);}
				
				
				if(speed>10.0)
				{
					//castray 2
					sensorpath_startpoint = block.GetPosition() + block.WorldMatrix.Forward* dist/2;
					sensorpath_destinationpoint = block.GetPosition() + block.WorldMatrix.Forward* height_target_recalc + vel/1f;
						
					if (MyAPIGateway.Physics.CastRay(sensorpath_startpoint, sensorpath_destinationpoint , out hit2 ))
					{
						collisionalert=true;	
						dist2 = (float)(block.GetPosition()-hit2.Position).Length();
						dist2_delta_lt = dist2 - dist2_lt;		
						
			           	//check if the hit is on its own
			    		if(hit2.HitEntity as IMyCubeGrid != null) // on voxel is null
			    		{
							if(MyAPIGateway.GridGroups.HasConnection(hit2.HitEntity as IMyCubeGrid, block.CubeGrid as IMyCubeGrid, GridLinkTypeEnum.Logical))
							{
								hit2_own_grid=true;		
								collisionalert=false;
							}
			    		}					
						
						// still only visible in debug because flying up leads everytime to hit the own grid
						if(debug) { MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), hit2_own_grid ? Color.Red : Color.Blue *0.5f, sensorpath_startpoint, sensorpath_destinationpoint - sensorpath_startpoint , 10, 0.05f);}
						
						
						//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "castray true, forward collision warning: ");
					}					
				}
				//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "castray true, abstand: "+ dist);

				if(dist > height_target_recalc && !collisionalert)
				{
					forcemulti = (height_target_recalc+height_target_regulationdistance-dist)/height_target_regulationdistance;
					
					if(forcemulti>1f) forcemulti=1f;
					if(forcemulti<0.0f) forcemulti=0.0f;
					
					//reduce force if distance is decreasing
					if(dist_delta_lt < 0f)
					{
						forcemulti=forcemulti / (-1f* dist_delta_lt +1f);
					}
					force = (Forward * (1f-forcemulti) * thrust_max*0.4f); // * gravitymulti ;
					
					//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "dist > height_target: "+force.Length());
				}
				else
				{
					forcemulti = (height_target_recalc -dist)/height_target_regulationdistance;

					if(forcemulti>1f) forcemulti=1f;
					if(forcemulti<0f) forcemulti=0f;
					
					//reduce force if distance is increasing
					if(dist_delta_lt > 0f && !collisionalert)
					{
						forcemulti=forcemulti / (1f+dist_delta_lt);
					}
					else if(collisionalert && dist2_delta_lt < 0f)
					{
						//if a collision ahead, increase multi
						forcemulti=Math.Max(forcemulti,(1f - (dist2 / speed/1f)) *0.5f);
					}
					//yes, again!
					if(forcemulti>1f) forcemulti=1f;
					if(forcemulti<0f) forcemulti=0f;
					
					force = (-Forward * forcemulti * thrust_max*5f) ; //* gravitymulti ;
				}	

				//reduce default thrust if to high, expect collision iminent
				if(dist > height_target_max*2 && !collisionalert)
				{
					m_thrustmulti = height_target_max/(dist-height_target_max);
				}
				
				dist_lt=dist;
				dist2_lt=dist2;
				
				if(force_to_centerofmass)
					force_acting_position = grid.Physics.CenterOfMassWorld;
				else
					force_acting_position = block.GetPosition();
				
				grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE,force,force_acting_position,torque);	

				//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "forcemulti:"+forcemulti);
			}           
			else
			{
				//no coollision for first raycast
				m_thrustmulti=0f;
			}
			block.ThrustMultiplier=m_thrustmulti;
			
			
			if(aligntogravity)
			{
				if(grid.Physics.Gravity.Length() != 0f)
				{
					force2= (grid.Physics.CenterOfMassWorld+grid.Physics.Gravity)- (grid.Physics.CenterOfMassWorld + Forward * grid.Physics.Gravity.Length()) ; //*gravitystrength) ;
					//MyAPIGateway.Utilities.ShowMessage("HE", "force2: "+force2.Length());

					//limit force
					if(force2.Length() > 5)
					{
						force2=(force2/force2.Length()) * 5f;
					}
					force= force2 * thrust_max*0.001f;
					//top and botom to prevent from moving
					grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE,force/2,block.GetPosition()+Forward*10,torque);
					grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE,-force/2,block.GetPosition()-Forward*10,torque);
					
					//MyAPIGateway.Utilities.ShowMessage("HE", "neu: "+Math.Round(force.Length(),2)+"alt: "+Math.Round(forcealt.Length(),2)+" force neu/alt: "+Math.Round((force.Length()/forcealt.Length()),2));
				}
			}
        }
   
        // Gamelogic initialization
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "Init in Behavior:");

        	block = Entity as IMyThrust;
			this.objectBuilder = objectBuilder;		
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME ;
        	
            if (block == null) return;
            
            block.IsWorkingChanged += Block_IsWorkingChanged;
            block.EnabledChanged += BlockOnEnabledChanged;
                        
            if (MyAPIGateway.Session == null) return;

            init=true;
            
        	m_isserver = MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Multiplayer.IsServer;
//        	if (!m_isserver) return;
            
			switch (block.BlockDefinition.SubtypeId)
			{
				//Model V1 (Mil)
				case "SmallAtmosphericHoverEngine_SmallBlock":
					gridsizespecial =-0.15f;
					break;

				case "LargeAtmosphericHoverEngine_SmallBlock":
					gridsizespecial = 0.26f;
					break;					

				case "SmallAtmosphericHoverEngine_LargeBlock":
					gridsizespecial = -0.4f;
					break;
						
				case "LargeAtmosphericHoverEngine_LargeBlock":
					gridsizespecial = 1.3f;
					break;					
				
				//Model V2 (Civ)
				case "SmallAtmosphericHoverEngine_SmallBlockV2":
					gridsizespecial =-0.18f;
					break;

				case "LargeAtmosphericHoverEngine_SmallBlockV2":
					gridsizespecial = 0.01f;
					break;					

				case "SmallAtmosphericHoverEngine_LargeBlockV2":
					gridsizespecial = -0.8f;
					break;
						
				case "LargeAtmosphericHoverEngine_LargeBlockV2":
					gridsizespecial = 0.01f;
					break;					
					
//ARMORED
				//Model V1 (Mil) Armored FUll
				case "SmallAtmosphericHoverEngine_SmallBlock_Armored_Full":
					gridsizespecial =0.26f;
					break;

				case "LargeAtmosphericHoverEngine_SmallBlock_Armored_Full":
					gridsizespecial = 0.26f;
					break;					

				case "SmallAtmosphericHoverEngine_LargeBlock_Armored_Full":
					gridsizespecial = 1.3f;
					break;
						
				case "LargeAtmosphericHoverEngine_LargeBlock_Armored_Full":
					gridsizespecial = 1.3f;
					break;					
				
				//Model V2 (Civ) Armored Half
				case "SmallAtmosphericHoverEngine_SmallBlockV2_Armored_Half":
					gridsizespecial =0.01f;
					break;

				case "LargeAtmosphericHoverEngine_SmallBlockV2_Armored_Half":
					gridsizespecial = 0.01f;
					break;					

				case "SmallAtmosphericHoverEngine_LargeBlockV2_Armored_Half":
					gridsizespecial = 0.01f;
					break;
						
				case "LargeAtmosphericHoverEngine_LargeBlockV2_Armored_Half":
					gridsizespecial = 0.01f;
					break;					
			}				
//            MyAPIGateway.Multiplayer.RegisterMessageHandler(HandlerId, updateclients_Behavior);
            
            load_default(block);
            load_data(block);
            
			//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "init controls and first color");
        }

        //fix for missing initailizing on DS in special case, maybe a keen prob
        public override void UpdateAfterSimulation100()
        {
        	if(!MyAPIGateway.Utilities.IsDedicated) {showstatenextframe=true;}
        }
        
        // Gamelogic update (each frame after simulation)
        public override void UpdateAfterSimulation()
        {
        	
            if (block == null || block.MarkedForClose || block.Closed) return;
            
//MyAPIGateway.Utilities.ShowMessage("HE", "Na:"+block.CustomName+" Co:"+m_color1+" ID:"+block.EntityId+" lastmessage:"+messagecontent);
            
            if(!init) 
            { 
            	Init(null);
            	return;
            }
            if(!initshowstate)
            {
            	showstate();
            	initshowstate=true;
            }
            
       		//fix for not changing color after welding
        	if(showstatenextframe)
        	{
        		showstatenextframe=false;
        		showstate();
        	}
            
            if (!block.IsWorking) return; // no update if block down, off or damaged
						
            BlockUpdate();
        }
        
        //------------------------------------------ show state
        public void showstate()
        {
//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "showstate1: block:"+block+" b:"+b);
        	if(block == null) return;
        	
//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "showstate2: ");

			//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "showstate2, server: "+m_isserver);
            if(!block.Enabled || !block.IsWorking)
			{
            	block.SetEmissiveParts("emissive10",Color.Red, 0.0f);
            	block.SetEmissiveParts("emissive11",Color.Red, 0.0f);
			}
			else
			{
				block.SetEmissiveParts("emissive10",m_color1, 1.0f);
				block.SetEmissiveParts("emissive11",Color.DarkRed, 1.0f);
			}
        }
        
       private void BlockOnEnabledChanged(IMyTerminalBlock b)
        {
       		showstatenextframe=true;
        }
       
        private void Block_IsWorkingChanged(VRage.Game.ModAPI.IMyCubeBlock obj)
        {
        	showstatenextframe=true;
        }
       
        // Gamelogic object builder, leave it alone ;)
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
        
        //Communications
        public class DetailData
		{
			public long EntityId { get; set; }
		    public string Details { get; set; }
		}
        
        // Gamelogic close when the block gets deleted
        public override void Close()
        {
			//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "Close in Behavior:");
        	block.IsWorkingChanged -= Block_IsWorkingChanged;
            block.EnabledChanged -= BlockOnEnabledChanged;

			//removae from new messagehandler
			if(HoverEngine_Core.HoverEngines.ContainsKey(block.EntityId))
			{
				HoverEngine_Core.HoverEngines.Remove(block.EntityId);
			}
          
            block = null;
        }
        
//______________________________________________________________________________________________________
//||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
//                                      TerminalControls
//||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
//______________________________________________________________________________________________________

       	private static bool initterminals = false;


        
//        public const ushort HandlerId = 46334;
        
        DetailData details = new DetailData();
        
        float altitudemin_slider_min = 0f;
        float altitudemin_slider_max = 10f;
        float altitudemin_slider_default = 1.5f;
        
        float altituderange_slider_min = 0f;
        float altituderange_slider_max = 5f;
        float altituderange_slider_default = 2.5f;
        
        float altituderegulationdistance_slider_min = 1f;
        float altituderegulationdistance_slider_max = 7f;
        float altituderegulationdistance_slider_default = 4f;
        
        float scalemulti=3f; // for large grid

        void Set_altitude_min_inc_S(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_min +0.5f;
        	if(t > altitudemin_slider_max) { t = altitudemin_slider_max;}
        	b.GameLogic.GetAs<hengine>().height_target_min = t;
        	save_data(b);
        }
        void Set_altitude_min_inc_L(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_min +0.5f;
        	if(t > altitudemin_slider_max*scalemulti) { t = altitudemin_slider_max*scalemulti;}
        	b.GameLogic.GetAs<hengine>().height_target_min = t;
        	save_data(b);
        }

        void Set_altitude_min_dec_S(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_min -0.5f;
        	if(t < altitudemin_slider_min) { t = altitudemin_slider_min;}
        	b.GameLogic.GetAs<hengine>().height_target_min = t;
        	save_data(b);
        }
        void Set_altitude_min_dec_L(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_min -0.5f;
        	if(t < altitudemin_slider_min*scalemulti) { t = altitudemin_slider_min*scalemulti;}
        	b.GameLogic.GetAs<hengine>().height_target_min = t;
        	save_data(b);
        }

        void Set_altitude_range_inc_S(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_range +0.5f;
        	if(t > altituderange_slider_max) { t = altituderange_slider_max;}
        	b.GameLogic.GetAs<hengine>().height_target_range = t;
        	save_data(b);
        }
        void Set_altitude_range_inc_L(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_range +0.5f;
        	if(t > altituderange_slider_max*scalemulti) { t = altituderange_slider_max*scalemulti;}
        	b.GameLogic.GetAs<hengine>().height_target_range = t;
        	save_data(b);
        }
        void Set_altitude_range_dec_S(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_range -0.5f;
        	if(t < altituderange_slider_min) { t = altituderange_slider_min;}
        	b.GameLogic.GetAs<hengine>().height_target_range = t;
        	save_data(b);
        }
        void Set_altitude_range_dec_L(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_range -0.5f;
        	if(t < altituderange_slider_min*scalemulti) { t = altituderange_slider_min*scalemulti;}
        	b.GameLogic.GetAs<hengine>().height_target_range = t;
        	save_data(b);
        }
        void Set_altitude_regdist_inc_S(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_regulationdistance +0.5f;
        	if(t > altituderegulationdistance_slider_max) { t = altituderegulationdistance_slider_max;}
        	b.GameLogic.GetAs<hengine>().height_target_regulationdistance = t;
        	save_data(b);
        }
        void Set_altitude_regdist_inc_L(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_regulationdistance +0.5f;
        	if(t > altituderegulationdistance_slider_max*scalemulti) { t = altituderegulationdistance_slider_max*scalemulti;}
        	b.GameLogic.GetAs<hengine>().height_target_regulationdistance = t;
        	save_data(b);
        }
        void Set_altitude_regdist_dec_S(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_regulationdistance -0.5f;
        	if(t < altituderegulationdistance_slider_min) { t = altituderegulationdistance_slider_min;}
        	b.GameLogic.GetAs<hengine>().height_target_regulationdistance = t;
        	save_data(b);
        }
        void Set_altitude_regdist_dec_L(IMyTerminalBlock b)
        {
         	if(b == null) return;
        	float t = b.GameLogic.GetAs<hengine>().height_target_regulationdistance -0.5f;
        	if(t < altituderegulationdistance_slider_min*scalemulti) { t = altituderegulationdistance_slider_min*scalemulti;}
        	b.GameLogic.GetAs<hengine>().height_target_regulationdistance = t;
        	save_data(b);
        }
        
        void Set_altitudemin(IMyTerminalBlock b, float f)
        {
         	if(b == null) return;
        	b.GameLogic.GetAs<hengine>().height_target_min = f;
        	save_data(b);
        }
        void Set_altituderange(IMyTerminalBlock b, float f)
        {
         	if(b == null) return;
        	b.GameLogic.GetAs<hengine>().height_target_range = f;
        	save_data(b);
        }
        void Set_altituderegdist(IMyTerminalBlock b, float f)
        {
         	if(b == null) return;
        	b.GameLogic.GetAs<hengine>().height_target_regulationdistance = f;
        	save_data(b);
        }
		void Set_aligntogravity_btn(IMyTerminalBlock b, bool f)
        {
         	if(b == null) return;
        	b.GameLogic.GetAs<hengine>().aligntogravity = f;
        	save_data(b);
        }
		void Set_force_to_centerofmass_btn(IMyTerminalBlock b, bool f)
        {
         	if(b == null) return;
        	b.GameLogic.GetAs<hengine>().force_to_centerofmass = f;
        	save_data(b);
        }
		void Set_Color1(IMyTerminalBlock b, Color c)
        {
         	if(b == null) return;
        	b.GameLogic.GetAs<hengine>().m_color1 = c;
        	save_data(b);
        	b.GameLogic.GetAs<hengine>().showstatenextframe=true;

//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "set color, to save: Server? "+m_isserver);
		}
		
        public void Buttons_SmallBlock()
        {      	
        	// Altitude Min slider control and action---------------------------
            var S_altitudemin = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("altitudemin_slider_S");
            S_altitudemin.Title = MyStringId.GetOrCompute("Altitude Min");
            S_altitudemin.Tooltip = MyStringId.GetOrCompute(altitudemin_slider_min+"m - "+altitudemin_slider_max+"m, minimum altitude at 0m/s speed");
            S_altitudemin.Writer = (b, t) => t.AppendFormat("{0:N1}", S_altitudemin.Getter(b)).Append(" m");
            S_altitudemin.SetLimits(altitudemin_slider_min, altitudemin_slider_max);           
            S_altitudemin.Getter = (b) => b.GameLogic.GetAs<hengine>().height_target_min;
            S_altitudemin.Setter = Set_altitudemin;
            S_altitudemin.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
            S_altitudemin.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddControl<IMyThrust>(S_altitudemin);
        	
        	var S_altitudemin_inc_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altitudemin_slider_inc_S");
        	S_altitudemin_inc_action.Action = Set_altitude_min_inc_S;
        	S_altitudemin_inc_action.Name = new StringBuilder("increase altitude min");
        	S_altitudemin_inc_action.Writer = S_altitudemin.Writer;
        	S_altitudemin_inc_action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
        	S_altitudemin_inc_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(S_altitudemin_inc_action);
        	
        	var S_altitudemin_dec_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altitudemin_slider_dec_S");
        	S_altitudemin_dec_action.Action = Set_altitude_min_dec_S;
        	S_altitudemin_dec_action.Name = new StringBuilder("decrease altitude min");
        	S_altitudemin_dec_action.Writer = S_altitudemin.Writer;
        	S_altitudemin_dec_action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
        	S_altitudemin_dec_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(S_altitudemin_dec_action);
        	//---------------------------
        	
        	// Altitude Range slider control and action---------------------------
            var S_altituderange = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("altituderange_slider_S");
            S_altituderange.Title = MyStringId.GetOrCompute("Altitude Range");
            S_altituderange.Tooltip = MyStringId.GetOrCompute(altituderange_slider_min+"m - "+altituderange_slider_max+"m, altitude range between speed 0m/s and 100m/s");
            S_altituderange.Writer = (b, t) => t.AppendFormat("{0:N1}", S_altituderange.Getter(b)).Append(" m");
            S_altituderange.SetLimits(altituderange_slider_min, altituderange_slider_max);           
            S_altituderange.Getter = (b) => b.GameLogic.GetAs<hengine>().height_target_range;
            S_altituderange.Setter = Set_altituderange;
            S_altituderange.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
            S_altituderange.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddControl<IMyThrust>(S_altituderange);
        	
        	var S_altituderange_inc_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderange_slider_inc_S");
        	S_altituderange_inc_action.Action = Set_altitude_range_inc_S;
        	S_altituderange_inc_action.Name = new StringBuilder("increase altitude range");
        	S_altituderange_inc_action.Writer = S_altituderange.Writer;
        	S_altituderange_inc_action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
        	S_altituderange_inc_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(S_altituderange_inc_action);
        	
        	var S_altituderange_dec_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderange_slider_dec_S");
        	S_altituderange_dec_action.Action = Set_altitude_range_dec_S;
        	S_altituderange_dec_action.Name = new StringBuilder("decrease altitude range");
        	S_altituderange_dec_action.Writer = S_altituderange.Writer;
        	S_altituderange_dec_action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
        	S_altituderange_dec_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(S_altituderange_dec_action);
        	//---------------------------       	
        	
        	// Altitude regulation distance slider control and action---------------------------
            var S_altituderegdist = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("altituderegdist_slider_S");
            S_altituderegdist.Title = MyStringId.GetOrCompute("Altitude regulation distance");
            S_altituderegdist.Tooltip = MyStringId.GetOrCompute(altituderegulationdistance_slider_min+"m - "+altituderegulationdistance_slider_max+"m, altitude regulation distance (range of spring), low = hard, high = soft");
            S_altituderegdist.Writer = (b, t) => t.AppendFormat("{0:N1}", S_altituderegdist.Getter(b)).Append(" m");
            S_altituderegdist.SetLimits(altituderegulationdistance_slider_min, altituderegulationdistance_slider_max);           
            S_altituderegdist.Getter = (b) => b.GameLogic.GetAs<hengine>().height_target_regulationdistance;
            S_altituderegdist.Setter = Set_altituderegdist;
            S_altituderegdist.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
            S_altituderegdist.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddControl<IMyThrust>(S_altituderegdist);
        	
        	var S_altituderegdist_inc_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderegdist_slider_inc_S");
        	S_altituderegdist_inc_action.Action = Set_altitude_regdist_inc_S;
        	S_altituderegdist_inc_action.Name = new StringBuilder("increase altitude regulation distance");
        	S_altituderegdist_inc_action.Writer = S_altituderegdist.Writer;
        	S_altituderegdist_inc_action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
        	S_altituderegdist_inc_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(S_altituderegdist_inc_action);
        	
        	var S_altituderegdist_dec_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderegdist_slider_dec_S");
        	S_altituderegdist_dec_action.Action = Set_altitude_regdist_dec_S;
        	S_altituderegdist_dec_action.Name = new StringBuilder("decrease altitude regulation distance");
        	S_altituderegdist_dec_action.Writer = S_altituderegdist.Writer;
        	S_altituderegdist_dec_action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
        	S_altituderegdist_dec_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_S");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(S_altituderegdist_dec_action);           	
        }
 
        public void Buttons_LargeBlock()
        {          	
        	// Altitude Min slider control and action---------------------------
            var L_altitudemin = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("altitudemin_slider_L");
            L_altitudemin.Title = MyStringId.GetOrCompute("Altitude Min");
            L_altitudemin.Tooltip = MyStringId.GetOrCompute(altitudemin_slider_min * scalemulti+"m - "+altitudemin_slider_max * scalemulti+"m, minimum altitude at 0m/s speed");
            L_altitudemin.Writer = (b, t) => t.AppendFormat("{0:N1}", L_altitudemin.Getter(b)).Append(" m");
            L_altitudemin.SetLimits(altitudemin_slider_min * scalemulti, altitudemin_slider_max * scalemulti);           
            L_altitudemin.Getter = (b) => b.GameLogic.GetAs<hengine>().height_target_min;
            L_altitudemin.Setter = Set_altitudemin;
            L_altitudemin.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
            L_altitudemin.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddControl<IMyThrust>(L_altitudemin);
        	
        	var L_altitudemin_inc_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altitudemin_slider_inc_L");
        	L_altitudemin_inc_action.Action = Set_altitude_min_inc_L;
        	L_altitudemin_inc_action.Name = new StringBuilder("increase altitude min");
        	L_altitudemin_inc_action.Writer = L_altitudemin.Writer;
        	L_altitudemin_inc_action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
        	L_altitudemin_inc_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(L_altitudemin_inc_action);
        	
        	var L_altitudemin_dec_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altitudemin_slider_dec_L");
        	L_altitudemin_dec_action.Action = Set_altitude_min_dec_L;
        	L_altitudemin_dec_action.Name = new StringBuilder("decrease altitude min");
        	L_altitudemin_dec_action.Writer = L_altitudemin.Writer;
        	L_altitudemin_dec_action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
        	L_altitudemin_dec_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(L_altitudemin_dec_action);
        	//---------------------------
        	
        	// Altitude Range slider control and action---------------------------
            var L_altituderange = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("altituderange_slider_L");
            L_altituderange.Title = MyStringId.GetOrCompute("Altitude Range");
            L_altituderange.Tooltip = MyStringId.GetOrCompute(altituderange_slider_min * scalemulti+"m - "+altituderange_slider_max * scalemulti+"m, altitude range between speed 0m/s and 100m/s");
            L_altituderange.Writer = (b, t) => t.AppendFormat("{0:N1}", L_altituderange.Getter(b)).Append(" m");
            L_altituderange.SetLimits(altituderange_slider_min * scalemulti, altituderange_slider_max * scalemulti);           
            L_altituderange.Getter = (b) => b.GameLogic.GetAs<hengine>().height_target_range;
            L_altituderange.Setter = Set_altituderange;
            L_altituderange.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
            L_altituderange.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddControl<IMyThrust>(L_altituderange);
        	
        	var L_altituderange_inc_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderange_slider_inc_L");
        	L_altituderange_inc_action.Action = Set_altitude_range_inc_L;
        	L_altituderange_inc_action.Name = new StringBuilder("increase altitude range");
        	L_altituderange_inc_action.Writer = L_altituderange.Writer;
        	L_altituderange_inc_action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
        	L_altituderange_inc_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(L_altituderange_inc_action);
        	
        	var L_altituderange_dec_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderange_slider_dec_L");
        	L_altituderange_dec_action.Action = Set_altitude_range_dec_L;
        	L_altituderange_dec_action.Name = new StringBuilder("decrease altitude range");
        	L_altituderange_dec_action.Writer = L_altituderange.Writer;
        	L_altituderange_dec_action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
        	L_altituderange_dec_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(L_altituderange_dec_action);
        	//---------------------------       	
        	
        	// Altitude regulation distance slider control and action---------------------------
            var L_altituderegdist = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyThrust>("altituderegdist_slider_L");
            L_altituderegdist.Title = MyStringId.GetOrCompute("Altitude regulation distance");
            L_altituderegdist.Tooltip = MyStringId.GetOrCompute(altituderegulationdistance_slider_min * scalemulti+"m - "+altituderegulationdistance_slider_max * scalemulti+"m, altitude regulation distance (range of spring), low = hard, high = soft");
            L_altituderegdist.Writer = (b, t) => t.AppendFormat("{0:N1}", L_altituderegdist.Getter(b)).Append(" m");
            L_altituderegdist.SetLimits(altituderegulationdistance_slider_min * scalemulti, altituderegulationdistance_slider_max * scalemulti);           
            L_altituderegdist.Getter = (b) => b.GameLogic.GetAs<hengine>().height_target_regulationdistance;
            L_altituderegdist.Setter = Set_altituderegdist;
            L_altituderegdist.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
            L_altituderegdist.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddControl<IMyThrust>(L_altituderegdist);
        	
        	var L_altituderegdist_inc_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderegdist_slider_inc_L");
        	L_altituderegdist_inc_action.Action = Set_altitude_regdist_inc_L;
        	L_altituderegdist_inc_action.Name = new StringBuilder("increase altitude regulation distance");
        	L_altituderegdist_inc_action.Writer = L_altituderegdist.Writer;
        	L_altituderegdist_inc_action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
        	L_altituderegdist_inc_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(L_altituderegdist_inc_action);
        	
        	var L_altituderegdist_dec_action = MyAPIGateway.TerminalControls.CreateAction<Sandbox.ModAPI.Ingame.IMyThrust>("altituderegdist_slider_dec_L");
        	L_altituderegdist_dec_action.Action = Set_altitude_regdist_dec_L;
        	L_altituderegdist_dec_action.Name = new StringBuilder("decrease altitude regulation distance");
        	L_altituderegdist_dec_action.Writer = L_altituderegdist.Writer;
        	L_altituderegdist_dec_action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
        	L_altituderegdist_dec_action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine_L");
        	MyAPIGateway.TerminalControls.AddAction<Sandbox.ModAPI.Ingame.IMyThrust>(L_altituderegdist_dec_action);   	        	
        }
        
        public void Buttons_AllBlock()
        {  
        	// align to gravity checkbox control and action---------------------------
            var aligntogravity_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyThrust>("aligntogravity_checkbox");
            aligntogravity_btn.OnText = MyStringId.GetOrCompute("On");
            aligntogravity_btn.OffText = MyStringId.GetOrCompute("Off");
            aligntogravity_btn.Title = MyStringId.GetOrCompute("Align to gravity");
            aligntogravity_btn.Tooltip = MyStringId.GetOrCompute("align to gravity, use it only for Up direction");
            aligntogravity_btn.Getter = (b) => b.GameLogic.GetAs<hengine>().aligntogravity;
            aligntogravity_btn.Setter = Set_aligntogravity_btn;
            aligntogravity_btn.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            aligntogravity_btn.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
			MyAPIGateway.TerminalControls.AddControl<IMyThrust>(aligntogravity_btn);
        	
			//toggle
            var aligntogravity_btn_toggle_Action = MyAPIGateway.TerminalControls.CreateAction<IMyThrust>("aligntogravity_btn_toggle_Action");
            aligntogravity_btn_toggle_Action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            aligntogravity_btn_toggle_Action.Name = new StringBuilder("Align to Gravity Toggle On/Off");
            aligntogravity_btn_toggle_Action.Action = (b) => aligntogravity_btn.Setter(b, !aligntogravity_btn.Getter(b));
            aligntogravity_btn_toggle_Action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<hengine>().aligntogravity ? aligntogravity_btn.OnText : aligntogravity_btn.OffText);
            aligntogravity_btn_toggle_Action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddAction<IMyThrust>(aligntogravity_btn_toggle_Action);

            // On
            var aligntogravity_btn_on_Action = MyAPIGateway.TerminalControls.CreateAction<IMyThrust>("aligntogravity_btn_on_Action");
            aligntogravity_btn_on_Action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            aligntogravity_btn_on_Action.Name = new StringBuilder("Align to Gravity On");
            aligntogravity_btn_on_Action.Action = (b) => aligntogravity_btn.Setter(b, true);
            aligntogravity_btn_on_Action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<hengine>().aligntogravity ? aligntogravity_btn.OnText : aligntogravity_btn.OffText);
            aligntogravity_btn_on_Action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddAction<IMyThrust>(aligntogravity_btn_on_Action);

            // Off
            var aligntogravity_btn_off_Action = MyAPIGateway.TerminalControls.CreateAction<IMyThrust>("aligntogravity_btn_off_Action");
            aligntogravity_btn_off_Action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            aligntogravity_btn_off_Action.Name = new StringBuilder("Align to Gravity Off");
            aligntogravity_btn_off_Action.Action = (b) => aligntogravity_btn.Setter(b, false);
            aligntogravity_btn_off_Action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<hengine>().aligntogravity ? aligntogravity_btn.OnText : aligntogravity_btn.OffText);
            aligntogravity_btn_off_Action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddAction<IMyThrust>(aligntogravity_btn_off_Action);
            
        	// force to center of mass checkbox control and action---------------------------
            var force_to_centerofmass_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyThrust>("force_to_centerofmass_checkbox");
            force_to_centerofmass_btn.OnText = MyStringId.GetOrCompute("On");
            force_to_centerofmass_btn.OffText = MyStringId.GetOrCompute("Off");
            force_to_centerofmass_btn.Title = MyStringId.GetOrCompute("Experts: Apply force to center");
            force_to_centerofmass_btn.Tooltip = MyStringId.GetOrCompute("apply force to center of mass (not to engine position, default: Off)");
            force_to_centerofmass_btn.Getter = (b) => b.GameLogic.GetAs<hengine>().force_to_centerofmass;
            force_to_centerofmass_btn.Setter = Set_force_to_centerofmass_btn;
            force_to_centerofmass_btn.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            force_to_centerofmass_btn.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
			MyAPIGateway.TerminalControls.AddControl<IMyThrust>(force_to_centerofmass_btn);
        	
			//toggle
            var force_to_centerofmass_btn_toggle_Action = MyAPIGateway.TerminalControls.CreateAction<IMyThrust>("force_to_centerofmass_btn_toggle_Action");
            force_to_centerofmass_btn_toggle_Action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            force_to_centerofmass_btn_toggle_Action.Name = new StringBuilder("Apply force to center Toggle On/Off");
            force_to_centerofmass_btn_toggle_Action.Action = (b) => force_to_centerofmass_btn.Setter(b, !force_to_centerofmass_btn.Getter(b));
            force_to_centerofmass_btn_toggle_Action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<hengine>().force_to_centerofmass ? force_to_centerofmass_btn.OnText : force_to_centerofmass_btn.OffText);
            force_to_centerofmass_btn_toggle_Action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddAction<IMyThrust>(force_to_centerofmass_btn_toggle_Action);

            // On
            var force_to_centerofmass_btn_on_Action = MyAPIGateway.TerminalControls.CreateAction<IMyThrust>("force_to_centerofmass_btn_on_Action");
            force_to_centerofmass_btn_on_Action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            force_to_centerofmass_btn_on_Action.Name = new StringBuilder("Apply force to center On");
            force_to_centerofmass_btn_on_Action.Action = (b) => force_to_centerofmass_btn.Setter(b, true);
            force_to_centerofmass_btn_on_Action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<hengine>().force_to_centerofmass ? force_to_centerofmass_btn.OnText : force_to_centerofmass_btn.OffText);
            force_to_centerofmass_btn_on_Action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddAction<IMyThrust>(force_to_centerofmass_btn_on_Action);

            // Off
            var force_to_centerofmass_btn_off_Action = MyAPIGateway.TerminalControls.CreateAction<IMyThrust>("force_to_centerofmass_btn_off_Action");
            force_to_centerofmass_btn_off_Action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            force_to_centerofmass_btn_off_Action.Name = new StringBuilder("Apply force to center Off");
            force_to_centerofmass_btn_off_Action.Action = (b) => force_to_centerofmass_btn.Setter(b, false);
            force_to_centerofmass_btn_off_Action.Writer = (b, t) => t.Append(b.GameLogic.GetAs<hengine>().force_to_centerofmass ? force_to_centerofmass_btn.OnText : force_to_centerofmass_btn.OffText);
            force_to_centerofmass_btn_off_Action.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddAction<IMyThrust>(force_to_centerofmass_btn_off_Action);
            
            // color 
			var color1 =MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyThrust>("emissive color1");
            color1.Title = MyStringId.GetOrCompute("Color");
            color1.Tooltip = MyStringId.GetOrCompute("Emissive Color");
            color1.Getter = (b) => b.GameLogic.GetAs<hengine>().m_color1;
            color1.Setter = Set_Color1;
            color1.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            color1.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            MyAPIGateway.TerminalControls.AddControl<IMyThrust>(color1);
            
        	// debug checkbox control, no action, did not safe !! ---------------------------
            var debug_btn = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyThrust>("debug_checkbox");
            debug_btn.OnText = MyStringId.GetOrCompute("On");
            debug_btn.OffText = MyStringId.GetOrCompute("Off");
            debug_btn.Title = MyStringId.GetOrCompute("debug, show hit");
            debug_btn.Tooltip = MyStringId.GetOrCompute("show scanline if hit for debug (temp)");
            debug_btn.Getter = (b) => b.GameLogic.GetAs<hengine>().debug;
            debug_btn.Setter = (b,t) => b.GameLogic.GetAs<hengine>().debug = t;
            debug_btn.Enabled = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
            debug_btn.Visible = (b) => b.BlockDefinition.SubtypeId.Contains("AtmosphericHoverEngine");
			MyAPIGateway.TerminalControls.AddControl<IMyThrust>(debug_btn);
        }
        	
         public void save_data (IMyTerminalBlock b)
        {
         	if(b == null) return;
         	
        	var data ="dont_change_this_please  : |";
        	data += "height_target_min:"+b.GameLogic.GetAs<hengine>().height_target_min.ToString(CultureInfo.InvariantCulture.NumberFormat)+"|";
        	data += "altituderange:"+b.GameLogic.GetAs<hengine>().height_target_range.ToString(CultureInfo.InvariantCulture.NumberFormat)+"|";
        	data += "altituderegdist:"+b.GameLogic.GetAs<hengine>().height_target_regulationdistance.ToString(CultureInfo.InvariantCulture.NumberFormat)+"|";
        	data += "aligntogravity_btn:"+b.GameLogic.GetAs<hengine>().aligntogravity.ToString(CultureInfo.InvariantCulture.NumberFormat)+"|";
        	data += "force_to_centerofmass_btn:"+b.GameLogic.GetAs<hengine>().force_to_centerofmass.ToString(CultureInfo.InvariantCulture.NumberFormat)+"|";
        	data += "color1:"+b.GameLogic.GetAs<hengine>().m_color1.R.ToString()+":"+b.GameLogic.GetAs<hengine>().m_color1.G.ToString()+":"+b.GameLogic.GetAs<hengine>().m_color1.B.ToString()+"|";
			b.CustomData = data;

			//send to other clients
			//DetailData details = new DetailData();
			details.EntityId=b.EntityId;
			details.Details=data;
			MyAPIGateway.Multiplayer.SendMessageToOthers(HandlerId, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(details)));

//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "Saved");
        }
        
        public void load_data (IMyTerminalBlock b)
        {
         	if(b == null) return;
         	
        	var data = "";
        	data = b.CustomData;
        	if(data !="")
        	{
        		//load data from customdata
        		char[] x = {'|'};
        		string[] datafull = data.Split(x);
        		foreach(string s in datafull)
        		{
        			char[] y = {':'};
        			string[] datapart = s.Split(y);
        			if(datapart[0]== "height_target_min") 
        			{
        				height_target_min= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat);
        				height_target_min_smooth= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat); //only at load
        			}
        			if(datapart[0]== "altituderange") {height_target_range= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat);}
        			if(datapart[0]== "altituderegdist") {height_target_regulationdistance= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat);}
        			if(datapart[0]== "aligntogravity_btn") {aligntogravity=Convert.ToBoolean(datapart[1])  ;}
        			if(datapart[0]== "force_to_centerofmass_btn") {force_to_centerofmass=Convert.ToBoolean(datapart[1])  ;}
        			if(datapart[0]== "color1") {m_color1 = conv_to_color(datapart);}
        		}		
        	}
        	else
        	{
        		//load default
        		height_target_min = altitudemin_slider_default;
        		height_target_min_smooth = altitudemin_slider_default;  //only at load
        		height_target_range = altituderange_slider_default;
        		height_target_regulationdistance = altituderegulationdistance_slider_default;
        		force_to_centerofmass = false;
        		aligntogravity	= true;
        		//m_color1	= Color.Green;
        		m_color1 = new Color (0,255,0,255); // lighter green after Keens graphic overhaoul 2/2018
        		
        		save_data(b);
        	}
			//update emissive after first load
			showstatenextframe = true;
        }
          
        public void load_default(IMyTerminalBlock b)
        {
         	if(b == null) return;
         	
        	if(b.BlockDefinition.SubtypeId.Contains("LargeBlock"))
        	{
	        	altitudemin_slider_default = altitudemin_slider_default * scalemulti;        		
	        	altituderange_slider_default = altituderange_slider_default * scalemulti;        		
	        	altituderegulationdistance_slider_default = altituderegulationdistance_slider_default * scalemulti;    
        	}
			//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "blocktype: "+b.BlockDefinition.SubtypeId);         	
        }
    
        public override void UpdateOnceBeforeFrame()
        {
            //Do init and control creation here
            if (!initterminals)
            {
            	//after Init for all engines (only once)
                Buttons_SmallBlock();
                Buttons_LargeBlock();
                Buttons_AllBlock();
                initterminals = true;
//MyAPIGateway.Utilities.ShowMessage("HoverEngine", "UpdateOnceBeforeFrame");
            }

			//registration for new messagehandler           
            if(block==null) return;
			if(!HoverEngine_Core.HoverEngines.ContainsKey(block.EntityId))
			{
				HoverEngine_Core.HoverEngines.Add(block.EntityId, this);
			}

			
			
        }

        public void updateclients(string Details)
        {
//MyAPIGateway.Utilities.ShowMessage("HE", "Message to update data, bytes:"+bytes.ToString());
         	if(block == null) return;
         	
          try
            {
                if (MyAPIGateway.Session == null || block==null)
                    return;
                
	        	if(Details !="")
	        	{
	        		// copy from save (b = block, ...)
	        		char[] x = {'|'};
	        		string[] datafull = Details.Split(x);
	        		foreach(string s in datafull)
	        		{
	        			char[] y = {':'};
	        			string[] datapart = s.Split(y);
	        			if(datapart[0]== "height_target_min") {block.GameLogic.GetAs<hengine>().height_target_min= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat);}
	        			if(datapart[0]== "altituderange") {block.GameLogic.GetAs<hengine>().height_target_range= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat);}
	        			if(datapart[0]== "altituderegdist") {block.GameLogic.GetAs<hengine>().height_target_regulationdistance= float.Parse(datapart[1],CultureInfo.InvariantCulture.NumberFormat);}
	        			if(datapart[0]== "aligntogravity_btn") {block.GameLogic.GetAs<hengine>().aligntogravity=Convert.ToBoolean(datapart[1])  ;}
        				if(datapart[0]== "force_to_centerofmass_btn") {block.GameLogic.GetAs<hengine>().force_to_centerofmass=Convert.ToBoolean(datapart[1])  ;}
						if(datapart[0]== "color1") {block.GameLogic.GetAs<hengine>().m_color1 = conv_to_color(datapart);}
						
						showstatenextframe=true;
	        		}		
	        	}
            }
            catch (Exception ex)
            {
//                MyAPIGateway.Utilities.ShowMessage("HoverEngine", "error: "+ex.ToString());
            }
        }
    
        Color conv_to_color(string[] s)
        {
        	try
            {
            	int r = MathHelper.Clamp(Convert.ToInt32(s[1]), 0, 255);
                int g = MathHelper.Clamp(Convert.ToInt32(s[2]), 0, 255);
                int b = MathHelper.Clamp(Convert.ToInt32(s[3]), 0, 255);
                return new Color(r, g, b, 255);
            }
            catch
            {
              	return Color.Blue;
            }
        }
    }
}
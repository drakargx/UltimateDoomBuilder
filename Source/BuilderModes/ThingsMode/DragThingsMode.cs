
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Interface;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using System.Drawing;
using CodeImp.DoomBuilder.Editing;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes.Editing
{
	// No action or button for this mode, it is automatic.
	// The EditMode attribute does not have to be specified unless the
	// mode must be activated by class name rather than direct instance.
	// In that case, just specifying the attribute like this is enough:
	[EditMode]

	public sealed class DragThingsMode : ClassicMode
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		// Mode to return to
		private EditMode basemode;
		
		// Mouse position on map where dragging started
		private Vector2D dragstartmappos;

		// Item used as reference for snapping to the grid
		protected Thing dragitem;
		private Vector2D dragitemposition;

		// List of old thing positions
		private List<Vector2D> oldpositions;

		// List of selected items
		private ICollection<Thing> selectedthings;

		// List of non-selected items
		private ICollection<Thing> unselectedthings;
		
		// Keep track of view changes
		private float lastoffsetx;
		private float lastoffsety;
		private float lastscale;
		
		// Options
		private bool snaptogrid;		// SHIFT to toggle
		private bool snaptonearest;		// CTRL to enable

		#endregion

		#region ================== Properties

		// Just keep the base mode button checked
		public override string EditModeButtonName { get { return basemode.GetType().Name; } }
		
		#endregion

		#region ================== Constructor / Disposer

		// Constructor to start dragging immediately
		public DragThingsMode(EditMode basemode, Vector2D dragstartmappos)
		{
			// Initialize
			this.dragstartmappos = dragstartmappos;
			this.basemode = basemode;

			Cursor.Current = Cursors.AppStarting;

			// Get the nearest thing for snapping
			dragitem = MapSet.NearestThing(General.Map.Map.GetThingsSelection(true), dragstartmappos);
			
			// Get selected things
			selectedthings = General.Map.Map.GetThingsSelection(true);
			unselectedthings = General.Map.Map.GetThingsSelection(false);

			// Make old positions list
			// We will use this as reference to move the vertices, or to move them back on cancel
			oldpositions = new List<Vector2D>(selectedthings.Count);
			foreach(Thing t in selectedthings) oldpositions.Add(t.Position);

			// Also keep old position of the dragged item
			dragitemposition = dragitem.Position;

			// Keep view information
			lastoffsetx = renderer.OffsetX;
			lastoffsety = renderer.OffsetY;
			lastscale = renderer.Scale;
			
			Cursor.Current = Cursors.Default;
			
			// We have no destructor
			GC.SuppressFinalize(this);
		}

		// Disposer
		public override void Dispose()
		{
			// Not already disposed?
			if(!isdisposed)
			{
				// Clean up

				// Done
				base.Dispose();
			}
		}

		#endregion

		#region ================== Methods
		
		// This moves the selected things relatively
		// Returns true when things has actually moved
		private bool MoveThingsRelative(Vector2D offset, bool snapgrid, bool snapnearest)
		{
			Vector2D oldpos = dragitem.Position;
			Thing nearest;
			int i = 0;
			
			// Snap to nearest?
			if(snapnearest)
			{
				// Find nearest unselected item within selection range
				nearest = MapSet.NearestThingSquareRange(unselectedthings, mousemappos, ThingsMode.THING_HIGHLIGHT_RANGE / renderer.Scale);
				if(nearest != null)
				{
					// Move the dragged item
					dragitem.Move((Vector2D)nearest.Position);

					// Adjust the offset
					offset = (Vector2D)nearest.Position - dragitemposition;

					// Do not snap to grid!
					snapgrid = false;
				}
			}

			// Snap to grid?
			if(snapgrid)
			{
				// Move the dragged item
				dragitem.Move(dragitemposition + offset);

				// Snap item to grid
				dragitem.SnapToGrid();

				// Adjust the offset
				offset += (Vector2D)dragitem.Position - (dragitemposition + offset);
			}

			// Drag item moved?
			if(!snapgrid || ((Vector2D)dragitem.Position != oldpos))
			{
				// Move selected geometry
				foreach(Thing t in selectedthings)
				{
					// Move vertex from old position relative to the
					// mouse position change since drag start
					t.Move(oldpositions[i] + offset);

					// Next
					i++;
				}

				// Moved
				return true;
			}
			else
			{
				// No changes
				return false;
			}
		}

		// This redraws the display
		public unsafe override void RedrawDisplay()
		{
			bool viewchanged = CheckViewChanged();

			if(viewchanged)
			{
				// Render lines and vertices
				if(renderer.StartPlotter(true))
				{
					renderer.PlotLinedefSet(General.Map.Map.Linedefs);
					renderer.PlotVerticesSet(General.Map.Map.Vertices);
					renderer.Finish();
				}
			}
			
			// Render things
			if(renderer.StartThings(true))
			{
				// Render things
				renderer.SetThingsRenderOrder(true);
				renderer.RenderThingSet(unselectedthings);
				renderer.RenderThingSet(selectedthings);

				// Draw the dragged item highlighted
				// This is important to know, because this item is used
				// for snapping to the grid and snapping to nearest items
				renderer.RenderThing(dragitem, General.Colors.Highlight);

				// Done
				renderer.Finish();
			}

			renderer.Present();
		}
		
		// Cancelled
		public override void Cancel()
		{
			// Move geometry back to original position
			MoveThingsRelative(new Vector2D(0f, 0f), false, false);

			// If only a single vertex was selected, deselect it now
			if(selectedthings.Count == 1) General.Map.Map.ClearSelectedThings();
			
			// Update cached values
			General.Map.Map.Update();
			
			// Cancel base class
			base.Cancel();
			
			// Return to vertices mode
			General.Map.ChangeMode(basemode);
		}

		// Mode engages
		public override void Engage()
		{
			base.Engage();
		}
		
		// Disenagaging
		public override void Disengage()
		{
			base.Disengage();
			Cursor.Current = Cursors.AppStarting;
			
			// When not cancelled
			if(!cancelled)
			{
				// Move geometry back to original position
				MoveThingsRelative(new Vector2D(0f, 0f), false, false);

				// Make undo for the dragging
				General.Map.UndoRedo.CreateUndo("drag things", UndoGroup.None, 0);

				// Move selected geometry to final position
				MoveThingsRelative(mousemappos - dragstartmappos, snaptogrid, snaptonearest);
				
				// Update cached values
				General.Map.Map.Update();

				// Map is changed
				General.Map.IsChanged = true;
			}

			// Hide highlight info
			General.Interface.HideInfo();

			// Done
			Cursor.Current = Cursors.Default;
		}

		// This checks if the view offset/zoom changed and updates the check
		protected bool CheckViewChanged()
		{
			bool viewchanged = false;
			
			// View changed?
			if(renderer.OffsetX != lastoffsetx) viewchanged = true;
			if(renderer.OffsetY != lastoffsety) viewchanged = true;
			if(renderer.Scale != lastscale) viewchanged = true;

			// Keep view information
			lastoffsetx = renderer.OffsetX;
			lastoffsety = renderer.OffsetY;
			lastscale = renderer.Scale;

			// Return result
			return viewchanged;
		}

		// This updates the dragging
		private void Update()
		{
			snaptogrid = General.Interface.ShiftState ^ General.Interface.SnapToGrid;
			snaptonearest = General.Interface.CtrlState;
			
			// Move selected geometry
			if(MoveThingsRelative(mousemappos - dragstartmappos, snaptogrid, snaptonearest))
			{
				// Update cached values
				//General.Map.Map.Update(true, false);
				General.Map.Map.Update();

				// Redraw
				General.Interface.RedrawDisplay();
			}
		}

		// Mouse moving
		public override void MouseMove(MouseEventArgs e)
		{
			base.MouseMove(e);
			Update();
		}

		// Mouse button released
		public override void MouseUp(MouseEventArgs e)
		{
			base.MouseUp(e);
			
			// Is the editing button released?
			if(e.Button == EditMode.EDIT_BUTTON)
			{
				// Just return to vertices mode, geometry will be merged on disengage.
				General.Map.ChangeMode(basemode);
			}
		}

		// When a key is released
		public override void KeyUp(KeyEventArgs e)
		{
			base.KeyUp(e);
			if(snaptogrid != General.Interface.ShiftState ^ General.Interface.SnapToGrid) Update();
			if(snaptonearest != General.Interface.CtrlState) Update();
		}

		// When a key is pressed
		public override void KeyDown(KeyEventArgs e)
		{
			base.KeyDown(e);
			if(snaptogrid != General.Interface.ShiftState ^ General.Interface.SnapToGrid) Update();
			if(snaptonearest != General.Interface.CtrlState) Update();
		}
		
		#endregion
	}
}

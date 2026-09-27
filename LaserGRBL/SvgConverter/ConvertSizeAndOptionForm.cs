//Copyright (c) 2016-2021 Diego Settimi - https://github.com/arkypita/

// This program is free software; you can redistribute it and/or modify  it under the terms of the GPLv3 General Public License as published by  the Free Software Foundation; either version 3 of the License, or (at  your option) any later version.
// This program is distributed in the hope that it will be useful, but  WITHOUT ANY WARRANTY; without even the implied warranty of  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GPLv3  General Public License for more details.
// You should have received a copy of the GPLv3 General Public License  along with this program; if not, write to the Free Software  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307,  USA. using System;

using LaserGRBL.PSHelper;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LaserGRBL.RasterConverter;
using Svg;
using LaserGRBL.Icons;
using LaserGRBL.UserControls;

namespace LaserGRBL.SvgConverter
{
	/// <summary>
	/// Description of ConvertSizeAndOptionForm.
	/// </summary>
	public partial class SvgToGCodeForm : Form
	{
		GrblCore mCore;
		bool supportPWM = Settings.GetObject("Support Hardware PWM", true);

		public ComboboxItem[] LaserOptions = new ComboboxItem[] { new ComboboxItem("M3 - Constant Power", "M3"), new ComboboxItem("M4 - Dynamic Power", "M4") };

		private List<SvgColorLayer> mLayers;
		private DataGridView DgvLayers;
		public class ComboboxItem
		{
			public string Text { get; set; }
			public object Value { get; set; }

			public ComboboxItem(string text, object value)
			{ Text = text; Value = value; }

			public override string ToString()
			{
				return Text;
			}
		}

		internal static void CreateAndShowDialog(GrblCore core, string filename, bool append)
		{
			CreateAndShowDialog(core, filename, null, append);
		}

		// svg: the document already loaded (i.e. converted from dxf), null to read it from filename
		internal static void CreateAndShowDialog(GrblCore core, string filename, System.Xml.Linq.XElement svg, bool append)
        {
            List<SvgColorLayer> layers;
            try
            {
                layers = SvgColorLayer.Scan(svg != null ? svg : GCodeFromSVG.ParseSvgFile(filename));
            }
            catch (Exception ex)
            {
                Logger.LogException("SvgColorLayer", ex);
                layers = new List<SvgColorLayer>();
            }

            using (SvgToGCodeForm f = new SvgToGCodeForm(core, layers))
            {
                f.ShowDialogForm();
                if (f.DialogResult == DialogResult.OK)
                {
                    Settings.SetObject("GrayScaleConversion.VectorizeOptions.BorderSpeed", f.IIBorderTracing.CurrentValue);
                    Settings.SetObject("GrayScaleConversion.Gcode.LaserOptions.PowerMax", f.IIMaxPower.CurrentValue);
					Settings.SetObject("GrayScaleConversion.Gcode.LaserOptions.PowerMin", f.IIMinPower.CurrentValue);
					Settings.SetObject("GrayScaleConversion.Gcode.LaserOptions.LaserOn", (f.CBLaserON.SelectedItem as ComboboxItem).Value);

					f.DgvLayers.EndEdit();
					foreach (SvgColorLayer layer in layers)
						layer.SaveSettings();

					core.LoadedFile.LoadImportedSVG(filename, svg, append, core, layers);
                }
            }
        }

        private SvgToGCodeForm(GrblCore core, List<SvgColorLayer> layers)
		{
			InitializeComponent();
			ThemeMgr.SetTheme(this);
            IconsMgr.PrepareButton(BtnCreate, "mdi-checkbox-marked");
            IconsMgr.PrepareButton(BtnCancel, "mdi-close-box");
			IconsMgr.PrepareButton(BtnOnOffInfo, "mdi-information-slab-box", new Size(16, 16));
            IconsMgr.PrepareButton(BtnModulationInfo, "mdi-information-slab-box", new Size(16, 16));
            IconsMgr.PrepareButton(BtnPSHelper, "mdi-information-slab-box", new Size(16, 16));
            IconsMgr.PrepareButton(BtnColorFilter, "mdi-information-slab-box", new Size(16, 16));
            mCore = core;
            mLayers = layers;

			BackColor = ColorScheme.FormBackColor;
			GbLaser.ForeColor = GbSpeed.ForeColor = ForeColor = ColorScheme.FormForeColor;
			BtnCancel.BackColor = BtnCreate.BackColor = ColorScheme.FormButtonsColor;

			LblSmin.Visible = LblSmax.Visible = IIMaxPower.Visible = IIMinPower.Visible = BtnModulationInfo.Visible = supportPWM;
			AssignMinMaxLimit();

			CBLaserON.Items.Add(LaserOptions[0]);
			CBLaserON.Items.Add(LaserOptions[1]);

		}

		#region Color layers

		private const int ColColor = 0, ColMode = 1, ColSpeed = 2, ColPower = 3, ColPasses = 4, ColPattern = 5, ColLinesPerMM = 6;

		// the grid replaces the old single color filter, so it is placed in the filter groupbox
		private void CreateLayersGrid()
		{
			tableLayoutPanel2.Visible = false;
			gbFilter.Text = Strings.SvgLayersTitle;
			gbFilter.ForeColor = ColorScheme.FormForeColor;

			DgvLayers = new DataGridView();
			DgvLayers.AllowUserToAddRows = false;
			DgvLayers.AllowUserToDeleteRows = false;
			DgvLayers.AllowUserToResizeRows = false;
			DgvLayers.RowHeadersVisible = false;
			DgvLayers.MultiSelect = false;
			DgvLayers.SelectionMode = DataGridViewSelectionMode.CellSelect;
			DgvLayers.EditMode = DataGridViewEditMode.EditOnEnter;
			DgvLayers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
			DgvLayers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			DgvLayers.BorderStyle = BorderStyle.None;
			DgvLayers.BackgroundColor = ColorScheme.FormBackColor;
			DgvLayers.DefaultCellStyle.BackColor = ColorScheme.FormBackColor;
			DgvLayers.DefaultCellStyle.ForeColor = ColorScheme.FormForeColor;
			DgvLayers.EnableHeadersVisualStyles = false;
			DgvLayers.ColumnHeadersDefaultCellStyle.BackColor = ColorScheme.FormButtonsColor;
			DgvLayers.ColumnHeadersDefaultCellStyle.ForeColor = ColorScheme.FormForeColor;

			DataGridViewTextBoxColumn color = new DataGridViewTextBoxColumn();
			color.HeaderText = Strings.SvgLayerColor;
			color.ReadOnly = true;
			color.FillWeight = 20;
			color.SortMode = DataGridViewColumnSortMode.NotSortable;

			DataGridViewComboBoxColumn mode = new DataGridViewComboBoxColumn();
			mode.HeaderText = Strings.SvgLayerMode;
			mode.DisplayMember = "Text";
			mode.ValueMember = "Value";
			mode.DataSource = new ComboboxItem[] {
				new ComboboxItem(Strings.SvgLayerModeLine, SvgLayerMode.Line),
				new ComboboxItem(Strings.SvgLayerModeFill, SvgLayerMode.Fill),
				new ComboboxItem(Strings.SvgLayerModeFillLine, SvgLayerMode.FillAndLine),
				new ComboboxItem(Strings.SvgLayerModeCut, SvgLayerMode.Cut),
				new ComboboxItem(Strings.SvgLayerModeIgnore, SvgLayerMode.Ignore) };
			mode.ValueType = typeof(SvgLayerMode);
			mode.FlatStyle = FlatStyle.Flat;
			mode.FillWeight = 40;

			List<ComboboxItem> patterns = new List<ComboboxItem>();
			foreach (ImageProcessor.Direction direction in Enum.GetValues(typeof(ImageProcessor.Direction)))
				if (GrblFile.VectorFilling(direction))
					patterns.Add(new ComboboxItem(GrblCore.TranslateEnum(direction), direction));

			DataGridViewComboBoxColumn pattern = new DataGridViewComboBoxColumn();
			pattern.HeaderText = Strings.SvgLayerFillPattern;
			pattern.DisplayMember = "Text";
			pattern.ValueMember = "Value";
			pattern.DataSource = patterns.ToArray();
			pattern.ValueType = typeof(ImageProcessor.Direction);
			pattern.FlatStyle = FlatStyle.Flat;
			pattern.FillWeight = 45;

			DataGridViewTextBoxColumn linesPerMM = new DataGridViewTextBoxColumn();
			linesPerMM.HeaderText = Strings.SvgLayerLinesPerMM;
			linesPerMM.ValueType = typeof(double);
			linesPerMM.FillWeight = 20;
			linesPerMM.SortMode = DataGridViewColumnSortMode.NotSortable;
			linesPerMM.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
			linesPerMM.DefaultCellStyle.Format = "0.##";

			DgvLayers.Columns.AddRange(new DataGridViewColumn[] {
				color, mode,
				CreateIntColumn(Strings.SvgLayerSpeed, 22),
				CreateIntColumn(Strings.SvgLayerPower, 18),
				CreateIntColumn(Strings.SvgLayerPasses, 16),
				pattern, linesPerMM });

			foreach (SvgColorLayer layer in mLayers)
			{
				if (!layer.LoadSettings())
				{
					layer.Speed = IIBorderTracing.CurrentValue;
					layer.Power = IIMaxPower.CurrentValue;
				}

				int index = DgvLayers.Rows.Add(null, layer.Mode, layer.Speed, layer.Power, layer.Passes, layer.FillDirection, layer.LinesPerMM);
				DataGridViewRow row = DgvLayers.Rows[index];
				row.Tag = layer;
				row.Cells[ColColor].Style.BackColor = row.Cells[ColColor].Style.SelectionBackColor = layer.DrawingColor;
				row.Cells[ColColor].ToolTipText = layer.Color + " - " + string.Format(Strings.SvgLayerElements, layer.ElementCount);
				RefreshFillCells(row);
			}

			DgvLayers.CellParsing += DgvLayers_CellParsing;
			DgvLayers.CellValueChanged += DgvLayers_CellValueChanged;
			DgvLayers.DataError += (sender, e) => { e.ThrowException = false; };
			DgvLayers.CurrentCellDirtyStateChanged += (sender, e) => { if (DgvLayers.CurrentCell is DataGridViewComboBoxCell) DgvLayers.CommitEdit(DataGridViewDataErrorContexts.Commit); };

			int visibleRows = Math.Max(1, Math.Min(mLayers.Count, 8));
			DgvLayers.Size = new Size(GbLaser.Width * 5 / 3 /* wider than the other groups: 7 columns */ - gbFilter.Padding.Horizontal - 6, DgvLayers.ColumnHeadersHeight + visibleRows * DgvLayers.RowTemplate.Height + 3);
			DgvLayers.Location = new Point(gbFilter.DisplayRectangle.Left + 3, gbFilter.DisplayRectangle.Top);
			gbFilter.Controls.Add(DgvLayers);
		}

		private static DataGridViewTextBoxColumn CreateIntColumn(string header, float weight)
		{
			DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn();
			col.HeaderText = header;
			col.ValueType = typeof(int);
			col.FillWeight = weight;
			col.SortMode = DataGridViewColumnSortMode.NotSortable;
			col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
			return col;
		}

		// fill pattern and line density are meaningful only for fill modes
		private void RefreshFillCells(DataGridViewRow row)
		{
			bool fill = ((SvgColorLayer)row.Tag).HasFill;
			foreach (int col in new int[] { ColPattern, ColLinesPerMM })
			{
				row.Cells[col].ReadOnly = !fill;
				row.Cells[col].Style.ForeColor = fill ? ColorScheme.FormForeColor : SystemColors.GrayText;
			}
		}

		private int MaxValueForColumn(int column)
		{
			switch (column)
			{
				case ColSpeed: return (int)GrblCore.Configuration.MaxRateX;
				case ColPower: return (int)GrblCore.Configuration.MaxPWM;
				default: return 100;
			}
		}

		// clamp numeric input to the allowed range, keep previous value if not a number
		private void DgvLayers_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
		{
			if (e.ColumnIndex == ColLinesPerMM)
			{
				double lines;
				if (double.TryParse(Convert.ToString(e.Value).Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lines))
					e.Value = Math.Max(0.5, Math.Min(50, lines));
				else
					e.Value = DgvLayers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

				e.ParsingApplied = true;
				return;
			}

			if (e.ColumnIndex < ColSpeed || e.ColumnIndex > ColPasses)
				return;

			int value;
			if (int.TryParse(Convert.ToString(e.Value), out value))
				e.Value = Math.Max(e.ColumnIndex == ColPower ? 0 : 1, Math.Min(MaxValueForColumn(e.ColumnIndex), value));
			else
				e.Value = DgvLayers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

			e.ParsingApplied = true;
		}

		private void DgvLayers_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0)
				return;

			DataGridViewRow row = DgvLayers.Rows[e.RowIndex];
			SvgColorLayer layer = (SvgColorLayer)row.Tag;
			object value = row.Cells[e.ColumnIndex].Value;

			switch (e.ColumnIndex)
			{
				case ColMode: layer.Mode = (SvgLayerMode)value; RefreshFillCells(row); break;
				case ColSpeed: layer.Speed = (int)value; break;
				case ColPower: layer.Power = (int)value; break;
				case ColPasses: layer.Passes = (int)value; break;
				case ColPattern: layer.FillDirection = (ImageProcessor.Direction)value; break;
				case ColLinesPerMM: layer.LinesPerMM = (double)value; break;
			}
		}

		// global speed/power act as default: propagate the change to the layers that still use the previous value
		private void PropagateToLayers(int column, int oldValue, int newValue)
		{
			if (DgvLayers == null)
				return;

			foreach (DataGridViewRow row in DgvLayers.Rows)
				if (row.Cells[column].Value is int && (int)row.Cells[column].Value == oldValue)
					row.Cells[column].Value = newValue;
		}

		#endregion

		private void AssignMinMaxLimit()
        { 
			IIBorderTracing.MaxValue = (int)GrblCore.Configuration.MaxRateX;
			IIMaxPower.MaxValue = (int)GrblCore.Configuration.MaxPWM;
		}

		public void ShowDialogForm()
        {
			IIBorderTracing.CurrentValue = Settings.GetObject("GrayScaleConversion.VectorizeOptions.BorderSpeed", 1000);

			string LaserOn = Settings.GetObject("GrayScaleConversion.Gcode.LaserOptions.LaserOn", "M3");

			if (LaserOn == "M3" || !GrblCore.Configuration.LaserMode)
				CBLaserON.SelectedItem = LaserOptions[0];
			else
				CBLaserON.SelectedItem = LaserOptions[1];

			string LaserOff = "M5"; //Settings.GetObject("GrayScaleConversion.Gcode.LaserOptions.LaserOff", "M5");

			IIMinPower.CurrentValue = Settings.GetObject("GrayScaleConversion.Gcode.LaserOptions.PowerMin", 0);
			IIMaxPower.CurrentValue = Settings.GetObject("GrayScaleConversion.Gcode.LaserOptions.PowerMax", (int)GrblCore.Configuration.MaxPWM);

			IIBorderTracing.Visible = LblBorderTracing.Visible = LblBorderTracingmm.Visible = true;

			RefreshPerc();

			CreateLayersGrid(); //after speed/power initialization: they are the default for new colors

			ShowDialog(FormsHelper.MainForm);
		}


		void IIBorderTracingCurrentValueChanged(object sender, int OldValue, int NewValue, bool ByUser)
		{
			PropagateToLayers(ColSpeed, OldValue, NewValue);
		}

	
		void IIMinPowerCurrentValueChanged(object sender, int OldValue, int NewValue, bool ByUser)
		{
			if (ByUser && IIMaxPower.CurrentValue <= NewValue)
				IIMaxPower.CurrentValue = NewValue + 1;

			RefreshPerc();
		}
		void IIMaxPowerCurrentValueChanged(object sender, int OldValue, int NewValue, bool ByUser)
		{
			if (ByUser && IIMinPower.CurrentValue >= NewValue)
				IIMinPower.CurrentValue = NewValue - 1;

			PropagateToLayers(ColPower, OldValue, NewValue);
			RefreshPerc();
		}

		private void RefreshPerc()
		{
			decimal maxpwm = GrblCore.Configuration != null ? GrblCore.Configuration.MaxPWM : -1;

			if (maxpwm > 0)
			{
				LblMaxPerc.Text = (IIMaxPower.CurrentValue / GrblCore.Configuration.MaxPWM).ToString("P1");
				LblMinPerc.Text = (IIMinPower.CurrentValue / GrblCore.Configuration.MaxPWM).ToString("P1");
			}
			else
			{
				LblMaxPerc.Text = "";
				LblMinPerc.Text = "";
			}
		}

		private void BtnOnOffInfo_Click(object sender, EventArgs e)
		{Tools.Utils.OpenLink(@"https://lasergrbl.com/usage/raster-image-import/target-image-size-and-laser-options/#laser-modes");}

		private void BtnModulationInfo_Click(object sender, EventArgs e)
		{Tools.Utils.OpenLink(@"https://lasergrbl.com/usage/raster-image-import/target-image-size-and-laser-options/#power-modulation");}

		private void CBLaserON_SelectedIndexChanged(object sender, EventArgs e)
		{
			ComboboxItem mode = CBLaserON.SelectedItem as ComboboxItem;

			if (mode != null)
			{
				if (!GrblCore.Configuration.LaserMode && (mode.Value as string) == "M4")
					MessageBox.Show(Strings.WarnWrongLaserMode, Strings.WarnWrongLaserModeTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);//warning!!
			}

		}



		private void BtnPSHelper_Click(object sender, EventArgs e)
		{
			MaterialDB.MaterialsRow row = PSHelperForm.CreateAndShowDialog(this);
			if (row != null)
			{
				if (IIBorderTracing.Visible)
					IIBorderTracing.CurrentValue = row.Speed;
				//if (IILinearFilling.Visible)
				//	IILinearFilling.CurrentValue = row.Speed;

				IIMaxPower.CurrentValue = IIMaxPower.MaxValue * row.Power / 100;
			}
		}
		private void BtnColorFilter_Click(object sender, EventArgs e)
		{Tools.Utils.OpenLink(@"https://lasergrbl.com/usage/raster-image-import/target-image-size-and-laser-options/#color-filter");}
		//private void IISizeW_OnTheFlyValueChanged(object sender, int OldValue, int NewValue, bool ByUser)
		//{
		//	if (ByUser)
		//		IISizeH.CurrentValue = IP.WidthToHeight(NewValue);
		//}

		//private void IISizeH_OnTheFlyValueChanged(object sender, int OldValue, int NewValue, bool ByUser)
		//{
		//	if (ByUser) IISizeW.CurrentValue = IP.HeightToWidht(NewValue);
		//}
	}
}

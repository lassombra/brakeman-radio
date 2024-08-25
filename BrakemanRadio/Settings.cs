using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

namespace BrakemanRadio
{
	public class Settings : UnityModManager.ModSettings, IDrawable
	{
		[Draw(DrawType.Toggle, Label = "Debug Logging")] public bool DebugLogging = false;
		[Draw(DrawType.Toggle, Label = "Require License")] public bool RequireLicense = true;
		public void OnChange()
		{
		}

		public override void Save(UnityModManager.ModEntry modEntry)
		{
			Save(this, modEntry);
		}
	}
}

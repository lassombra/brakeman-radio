using DV.CabControls;
using DV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BrakemanRadio
{
	public class BrakemanSwitchChecker : MonoBehaviour
	{
		private BrakeManSwitch remote;
		private ItemBase item;
		private CommsJunctionSwitcher switcher;

		private void Awake()
		{
			this.remote = base.GetComponent<BrakeManSwitch>();
			this.item = base.GetComponentInParent<ItemBase>();
			this.switcher = base.GetComponent<CommsJunctionSwitcher>();
		}

		private void Update()
		{
			if (!this.remote.enabled || !this.item.IsGrabbed() || !InteractionTextControllerNonVr.Instance)
			{
				return;
			}
			if (this.switcher.PointedSwitch != null)
			{
				InteractionTextControllerNonVr.Instance.DisplayText(InteractionInfoType.JunctionRemoteSwitchUse);
			}
		}
	}
}

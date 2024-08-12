using DV.VRTK_Extensions;
using DV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRTK;
using UnityEngine;

namespace BrakemanRadio
{
	public class BrakemanRemoteHaptics : MonoBehaviour
	{
		// Token: 0x060038E9 RID: 14569 RVA: 0x0010D94C File Offset: 0x0010BB4C
		private void Start()
		{
			this.remoteLogic = base.GetComponent<BrakeManSwitch>();
			this.interactable = base.GetComponentInParent<VRTK_InteractableObject>();
			this.switcher = base.GetComponent<CommsJunctionSwitcher>();
			this.SetupListeners(true);
		}

		// Token: 0x060038EA RID: 14570 RVA: 0x0010D9B4 File Offset: 0x0010BBB4
		private void SetupListeners(bool on)
		{
			if (on)
			{
				this.interactable.InteractableObjectUsed += this.OnUsed;
				this.switcher.JunctionHovered += this.OnHovered;
				return;
			}
			this.interactable.InteractableObjectUsed -= this.OnUsed;
			this.switcher.JunctionHovered -= this.OnHovered;
		}

		// Token: 0x060038EB RID: 14571 RVA: 0x0010DA21 File Offset: 0x0010BC21
		private void OnHovered(JunctionSwitchRemoteControllable junction)
		{
			HapticUtils.DoHapticPulse(VRTK_ControllerReference.GetControllerReference(this.interactable.GetGrabbingObject()), HapticIntensityType.Normal);
		}

		// Token: 0x060038EC RID: 14572 RVA: 0x0010DA39 File Offset: 0x0010BC39
		private void OnUsed(object sender, InteractableObjectEventArgs e)
		{
			if (this.switcher.PointedSwitch)
			{
				HapticUtils.DoHapticPulse(VRTK_ControllerReference.GetControllerReference(e.interactingObject), HapticIntensityType.Normal);
			}
		}

		// Token: 0x04003206 RID: 12806
		private const float USE_HAPTIC_STRENGTH = 0.4f;

		// Token: 0x04003207 RID: 12807
		private BrakeManSwitch remoteLogic;

		// Token: 0x04003208 RID: 12808
		private VRTK_InteractableObject interactable;

		// Token: 0x04003209 RID: 12809
		private CommsJunctionSwitcher switcher;
	}
}
